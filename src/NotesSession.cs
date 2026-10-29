using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Notewll
{
    sealed class NotesSession : IDisposable
    {
        readonly NoteStore store;
        readonly Config config;
        readonly Func<byte[], string> transcribe;
        readonly bool nativeRecognition;
        readonly BlockingCollection<NoteSegment> jobs = new BlockingCollection<NoteSegment>();
        readonly ManualResetEvent ready = new ManualResetEvent(false);
        Thread audioThread, textThread;
        volatile bool capturing, busy, disposed, paused;
        long resumeFrame;
        long recordingStarted;
        public double ElapsedSeconds { get { return capturing ? resumeFrame / 16000.0 + Math.Max(0, (Stopwatch.GetTimestamp() - recordingStarted) / (double)Stopwatch.Frequency) : Document == null ? 0 : Document.DurationSeconds; } }
        public bool Paused { get { return paused; } }
        Exception startError;
        public NoteDocument Document { get; private set; }
        public bool Busy { get { return busy; } }
        public bool Recording { get { return capturing; } }
        public int MicrophoneLevel, ComputerLevel, MicrophoneSound, ComputerSound;
        public Action Changed;
        public NotesSession(NoteStore store, Config config, Func<byte[], string> transcribe = null)
        {
            this.store = store; this.config = config.Copy(); nativeRecognition = transcribe == null && config.SpeechLanguage == "en" && !SpeechQuality.Available(config);
            this.transcribe = transcribe ?? (wav => SpeechEngine.TranscribeEnsured(this.config, wav));
        }
        void Notify() { var callback = Changed; if (!disposed && callback != null) callback(); }
        public void Start(string title, string mode, string name, string microphoneId, string outputId, string kind = "Note")
        {
            if (busy || Document != null) throw new InvalidOperationException("Finish the current note first.");
            Document = store.Create(title, mode, name); Document.Kind = kind; Document.VoiceDraft = kind == "Note";
            if (Document.AutoTitle) Document.Title = (kind == "Meeting" ? "Online meeting" : kind == "Conversation" ? "Conversation" : "Voice note") + " \u00b7 " + DateTime.Now.ToString("MMM d, HH:mm");
            store.Save(Document); capturing = busy = true;
            recordingStarted = Stopwatch.GetTimestamp();
            audioThread = new Thread(() => Capture(mode, microphoneId, outputId)) { IsBackground = true, Name = "Notes audio" };
            audioThread.SetApartmentState(ApartmentState.STA); audioThread.Start();
            if (!ready.WaitOne(10000)) { capturing = false; throw new IOException("Audio devices took too long to open. No audio was captured."); }
            if (startError != null) throw new IOException("Could not start note recording: " + startError.Message, startError);
            StartTranscriber();
        }
        public void Resume(NoteDocument document, string microphoneId, string outputId)
        {
            if (busy || Document != null) throw new InvalidOperationException("Finish the active note first.");
            Document = document; Document.CaptureError = null; resumeFrame = (long)Math.Round(document.DurationSeconds * 16000); capturing = busy = true;
            recordingStarted = Stopwatch.GetTimestamp();
            Document.Status = "Recording"; store.Save(Document);
            audioThread = new Thread(() => Capture(document.Mode, microphoneId, outputId)) { IsBackground = true, Name = "Notes audio" };
            audioThread.SetApartmentState(ApartmentState.STA); audioThread.Start();
            if (!ready.WaitOne(10000)) { capturing = false; throw new IOException("Audio devices took too long to open."); }
            if (startError != null) throw new IOException(startError.Message); StartTranscriber();
        }
        public void TogglePause() { if (!capturing) return; paused = !paused; lock (Document) { Document.Status = paused ? "Paused" : "Recording"; store.Save(Document); } Notify(); }
        void StartTranscriber()
        {
            textThread = new Thread(ProcessTranscripts) { IsBackground = true, Name = "Notes transcription" }; textThread.Start();
        }
        public void Retry(NoteDocument document)
        {
            if (busy || Document != null) throw new InvalidOperationException("Finish the current note first.");
            Document = document; busy = true;
            lock (Document)
            {
                Document.Status = "Transcribing";
                foreach (var segment in Document.Segments.Where(s => !s.Complete || !String.IsNullOrEmpty(s.Error))) { segment.Error = null; segment.Complete = false; jobs.Add(segment); }
                store.Save(Document);
            }
            jobs.CompleteAdding(); StartTranscriber(); Notify();
        }
        void Capture(string mode, string microphoneId, string outputId)
        {
            var captures = new Dictionary<string, NotesCapture>(); var tracks = new Dictionary<string, WaveTrack>();
            var queuedFrames = new Dictionary<string, long>();
            long epoch = Stopwatch.GetTimestamp(); bool started = false;
            var defaultIds = new Dictionary<string, string>(); long lastDeviceCheck = epoch;
            try
            {
                foreach (string source in mode == "Both" ? new[] { "Microphone", "Computer" } : new[] { mode })
                {
                    if (String.IsNullOrEmpty(source == "Computer" ? outputId : microphoneId)) defaultIds[source] = CoreAudio.DefaultId(source == "Computer");
                    captures.Add(source, new NotesCapture(source == "Computer", source == "Computer" ? outputId : microphoneId, epoch));
                    tracks.Add(source, new WaveTrack(store.AudioPath(Document, source), resumeFrame > 0)); queuedFrames[source] = tracks[source].Frames;
                }
                // Each NotesCapture uses the shared epoch created before device initialization; keep it stable.
                // Opening devices can take a moment; their timestamps preserve the corresponding initial silence.
                foreach (var capture in captures.Values) capture.Start(); started = true; ready.Set(); Notify();
                while (capturing)
                {
                    if (Stopwatch.GetTimestamp() - lastDeviceCheck >= Stopwatch.Frequency)
                    {
                        foreach (var source in defaultIds.Keys) if (CoreAudio.DefaultId(source == "Computer") != defaultIds[source]) throw new IOException(source + " default device changed. Audio was kept. Choose the new device and resume this recording.");
                        lastDeviceCheck = Stopwatch.GetTimestamp();
                    }
                    MicrophoneLevel = ComputerLevel = MicrophoneSound = ComputerSound = 0;
                    foreach (var source in captures.Keys)
                    {
                        string current = source;
                        captures[source].Drain((frame, pcm) =>
                        {
                            if (!paused) tracks[current].WriteAt(frame + resumeFrame, pcm);
                            int peak = 0; double squares = 0; for (int i = 0; i + 1 < pcm.Length; i += 2) { int value = BitConverter.ToInt16(pcm, i); peak = Math.Max(peak, Math.Abs(value)); squares += (double)value * value; }
                            int rms = (int)Math.Sqrt(squares / Math.Max(1, pcm.Length / 2));
                            if (current == "Microphone") { MicrophoneLevel = peak; MicrophoneSound = rms; } else { ComputerLevel = peak; ComputerSound = rms; }
                        });
                        // Loopback supplies no packets during silence. Retain wall-clock time for both sources.
                        long safeFrame = Math.Max(0, (long)((Stopwatch.GetTimestamp() - epoch) * 16000.0 / Stopwatch.Frequency) - 3200) + resumeFrame;
                        tracks[source].PadTo(safeFrame);
                        long available = tracks[source].Frames - queuedFrames[source];
                        if (available >= 16000)
                        {
                            tracks[source].Flush();
                            var tail = WaveTrack.ReadPcm(store.AudioPath(Document, source), tracks[source].Frames - 6400, 6400);
                            if (!QuietAudio.HasSignal(tail) || available >= 320000)
                            { int count = (int)Math.Min(available, 320000); QueueSegment(source, queuedFrames[source], count); queuedFrames[source] += count; }
                        }
                    }
                    Thread.Sleep(20);
                }
            }
            catch (Exception ex)
            {
                if (!started) startError = ex;
                lock (Document) { Document.CaptureError = ex.Message; Document.Status = "Recording stopped - " + ex.Message; }
            }
            finally
            {
                capturing = false;
                foreach (var source in captures.Keys)
                {
                    try { captures[source].Drain((frame, pcm) => { if (tracks.ContainsKey(source) && !paused) tracks[source].WriteAt(frame + resumeFrame, pcm); }); } catch { }
                    captures[source].Dispose();
                }
                foreach (var source in tracks.Keys)
                {
                    try
                    {
                        if (started) tracks[source].PadTo((long)((Stopwatch.GetTimestamp() - epoch) * 16000.0 / Stopwatch.Frequency) + resumeFrame);
                        tracks[source].Dispose();
                        while (tracks[source].Frames > queuedFrames[source])
                        {
                            int frames = (int)Math.Min(64000, tracks[source].Frames - queuedFrames[source]);
                            QueueSegment(source, queuedFrames[source], frames); queuedFrames[source] += frames;
                        }
                        Document.DurationSeconds = Math.Max(Document.DurationSeconds, tracks[source].Frames / 16000.0);
                    }
                    catch (Exception ex) { lock (Document) Document.CaptureError = ex.Message; try { tracks[source].Dispose(); } catch { } }
                }
                lock (Document)
                {
                    Document.Status = startError != null ? "Could not record - no audio captured" : "Transcribing";
                    try { store.Save(Document); } catch (Exception ex) { Document.CaptureError = "Saving note failed: " + ex.Message; }
                }
                jobs.CompleteAdding();
                if (!started) busy = false;
                ready.Set(); Notify();
            }
        }
        void QueueSegment(string source, long start, int frames)
        {
            var segment = new NoteSegment { Source = source, StartFrame = start, Frames = frames };
            lock (Document) { Document.Segments.Add(segment); store.Save(Document); }
            jobs.Add(segment);
        }
        void ProcessTranscripts()
        {
            var streams = new Dictionary<string, NativeSpeechStream>(); var pendingStarts = new Dictionary<string, long>(); var failed = new HashSet<string>();
            try
            {
                foreach (var segment in jobs.GetConsumingEnumerable())
                {
                    if (disposed) return;
                    try
                    {
                        byte[] pcm = WaveTrack.ReadPcm(store.AudioPath(Document, segment.Source), segment.StartFrame, segment.Frames);
                        if (config.ReduceNoise && !SpeechQuality.Available(config)) pcm = QuietAudio.Filter(pcm);
                        if (SpeechQuality.Available(config))
                        {
                            var result = SpeechQuality.Recognize(config, pcm, true);
                            lock (Document)
                            {
                                foreach (var voice in result.turns)
                                {
                                    var turn = new NoteSegment { Source = segment.Source, StartFrame = segment.StartFrame + (long)(voice.start * 16000), Frames = Math.Max(0, (int)((voice.end - voice.start) * 16000)), RawText = voice.text, Text = voice.text, Complete = true, VoiceEmbedding = voice.embedding };
                                    MeetingVoices.Assign(Document, turn); Document.Segments.Add(turn);
                                }
                                segment.Complete = true; segment.Error = null; segment.RawText = segment.Text = "";
                                NoteMetadata.UpdateTitle(Document); store.Save(Document);
                            }
                            Notify(); continue;
                        }
                        string text;
                        if (nativeRecognition)
                        {
                            if (failed.Contains(segment.Source)) throw new IOException("This audio source needs a full retry.");
                            NativeSpeechStream stream;
                            if (!streams.TryGetValue(segment.Source, out stream)) { stream = SpeechEngine.CreateStream(); streams[segment.Source] = stream; pendingStarts[segment.Source] = segment.StartFrame; }
                            for (int position = 0; position < pcm.Length; position += 8000) { var chunk = new byte[Math.Min(8000, pcm.Length - position)]; Buffer.BlockCopy(pcm, position, chunk, 0, chunk.Length); stream.Feed(chunk); }
                            text = stream.DrainCompleted();
                            if (text.Length > 0) { segment.TranscriptStartFrame = pendingStarts[segment.Source]; pendingStarts[segment.Source] = segment.StartFrame + segment.Frames; }
                        }
                        else text = QuietAudio.HasSignal(pcm) ? transcribe(MicRecorder.ToWav(pcm)) : "";
                        lock (Document)
                        {
                            segment.Text = text; segment.RawText = text; segment.Error = null; segment.Complete = true;
                            var names = Introductions.Find(text); segment.Introduction = String.Join(", ", names);
                            foreach (string name in names) if (!Document.Participants.Contains(name)) Document.Participants.Add(name);
                            if (segment.Source == "Microphone" && names.Count == 1 && !Document.NameEdited)
                            {
                                Document.UserName = names[0]; store.SavedName = names[0];
                            }
                            NoteMetadata.UpdateTitle(Document); store.Save(Document);
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (Document)
                        {
                            segment.Error = ex.Message; segment.Complete = false;
                            if (nativeRecognition) { failed.Add(segment.Source); foreach (var item in Document.Segments.Where(s => s.Source == segment.Source)) { item.Complete = false; item.Error = "Source transcription interrupted. Retry saved audio."; } }
                            store.Save(Document);
                        }
                    }
                    Notify();
                }
                foreach (var source in streams.Keys.Where(s => !failed.Contains(s)))
                {
                    string tail = streams[source].Finish();
                    if (tail.Length > 0) lock (Document)
                    {
                        var last = Document.Segments.Last(s => s.Source == source); last.RawText = ((last.RawText ?? last.Text ?? "") + " " + tail).Trim(); last.Text = last.RawText;
                        if (!last.TranscriptStartFrame.HasValue) last.TranscriptStartFrame = pendingStarts[source];
                        foreach (string name in Introductions.Find(tail)) if (!Document.Participants.Contains(name)) Document.Participants.Add(name);
                        var names = Introductions.Find(tail); if (source == "Microphone" && names.Count == 1 && !Document.NameEdited) { Document.UserName = names[0]; store.SavedName = names[0]; }
                        NoteMetadata.UpdateTitle(Document);
                    }
                }
                lock (Document)
                {
                    // Conversations retain recognized words; dictation cleanup does not rewrite this record.
                    Document.Status = !String.IsNullOrEmpty(Document.CaptureError) ? "Audio stopped - " + Document.CaptureError : Document.Segments.Any(s => !s.Complete) ? "Saved - transcription needs retry" : "Saved";
                    store.Save(Document);
                }
            }
            catch (Exception ex)
            {
                lock (Document) { if (nativeRecognition) foreach (var item in Document.Segments) { item.Complete = false; item.Error = "Transcription interrupted; retry saved audio."; } Document.CaptureError = ex.Message; Document.Status = "Note error - audio kept: " + ex.Message; try { store.Save(Document); } catch { } }
            }
            finally { foreach (var stream in streams.Values) stream.Dispose(); busy = false; Notify(); }
        }
        public void Stop() { capturing = false; Notify(); }
        public void Dispose()
        {
            disposed = true; Changed = null; capturing = false;
            if (audioThread != null && audioThread != Thread.CurrentThread) audioThread.Join(4000);
            if (textThread != null && textThread != Thread.CurrentThread) textThread.Join(4000);
            // Transcription jobs are persisted before submission; unfinished jobs can be retried on reopening.
            // Never terminate a thread while it is writing a note or its audio file.
        }
    }
}
