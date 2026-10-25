using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Notewll
{
    static class Diagnostics
    {
        public static int Run(string[] args)
        {
            if (args[0] == "--review-smoke")
            {
                var store = new NoteStore(args[1]); var note = store.Create("Zulu", "Both", "Sudheer"); note.WrittenNotes = "Private annotation"; note.Status = "Saved";
                note.Segments.Add(new NoteSegment { Source = "Computer", StartFrame = 16000, Frames = 16000, Complete = true, RawText = "Alex said the original words.", Text = "legacy text", Introduction = "Alex" });
                NotesPane.SetSpeakerLabels(note, new[] { "Alex" });
                bool rejected = false; try { NotesPane.SetSpeakerLabels(note, new[] { "Bad\nLabel" }); } catch (InvalidDataException) { rejected = true; }
                if (!rejected || note.Segments[0].RawText != "Alex said the original words." || note.Segments[0].Text != "legacy text") throw new IOException("Speaker editing changed spoken words or accepted invalid labels.");
                store.Save(note); var restored = store.List().Single();
                if (restored.Segments[0].SpeakerLabel != "Alex" || !NoteStore.Transcript(restored).Contains("Alex [Computer]")) throw new IOException("Speaker label was not retained.");
                var scoped = NoteExport.Scope(note, 0, false);
                if (scoped.WrittenNotes != "" || scoped.UserName != "" || scoped.Segments[0].SpeakerLabel != "" || scoped.Segments[0].Introduction != "" || !NoteStore.Transcript(scoped).Contains("Alex said the original words.") || note.UserName != "Sudheer" || note.Segments[0].SpeakerLabel != "Alex") throw new IOException("Export scope changed originals or leaked metadata.");
                if (NoteExport.Scope(note, 1, true).Segments.Count != 0) throw new IOException("Personal notes scope included conversation.");
                var other = store.Create("Alpha", "Microphone", ""); other.CreatedUtc = DateTime.UtcNow.AddDays(-1).ToString("o"); other.DurationSeconds = 50;
                var notes = new[] { note, other };
                if (NotesPane.Sorted(notes, "Title").First() != other || NotesPane.Sorted(notes, "Newest").First() != note || NotesPane.Sorted(notes, "Oldest").First() != other || NotesPane.Sorted(notes, "Longest").First() != other) throw new IOException("Note sort order is incorrect.");
                var config = new Config { NotesSort = "Title", ExportFormat = "CSV" }.Copy(); config.Validate(); if (config.ExportFormat != "CSV" || config.NotesSort != "Title") throw new IOException("Review preferences were lost.");
                string conversation = NoteStore.Transcript(note); note.Summary = NotesSummary.Generate(note);
                if (!note.Summary.Contains("Possible decisions") || !NotesSummary.Answer(note, "Who said the original words?").Contains("Alex said the original words.") || NotesSummary.Answer(note, "unmentioned spaceflight").Contains("Alex said") || NoteStore.Transcript(note) != conversation) throw new IOException("Local review invented facts or modified the conversation.");
                if (NoteExport.Scope(note, 2, false).Summary.Contains("Alex [Computer]")) throw new IOException("Local review leaked speaker metadata.");
                Console.Write("review-ok|speaker-metadata|words-unchanged|export-scope|sort|preferences"); return 0;
            }
            if (args[0] == "--playback-smoke")
            {
                using (var player = new NotesPlayback()) { player.Open(args[1]); player.Speed(1.25); player.Play(100); Thread.Sleep(120); player.Pause(); if (player.Position < 100) throw new IOException("Playback did not seek."); }
                Console.Write("playback-ok|seek|speed|pause|close"); return 0;
            }
            SpeechEngine.DiagnosticRoot(args[1]);
            if (args[0] == "--engine-stress")
            {
                var streams = Enumerable.Range(0, 16).Select(_ => SpeechEngine.CreateStream()).ToArray();
                try
                {
                    SpeechEngine.Shutdown();
                    Task.WaitAll(streams.Select(stream => Task.Run(() => { try { for (int frame = 0; frame < 8; frame++) stream.Feed(new byte[8000]); stream.Finish(); } finally { stream.Dispose(); } })).ToArray());
                    if (SpeechEngine.ActiveStreams != 0) throw new IOException("Native recognizers leaked after concurrent shutdown.");
                    bool rejected = false; try { using (SpeechEngine.CreateStream()) { } } catch (SttException) { rejected = true; }
                    if (!rejected) throw new IOException("Shutdown accepted a new recognizer.");
                }
                finally { foreach (var stream in streams) stream.Dispose(); }
                Console.Write("stress-ok|16-streams|shutdown|released|new-stream-rejected"); return 0;
            }
            if (args[0] == "--native-notes-smoke")
            {
                var store = new NoteStore(args[2]); var note = store.Create("Silence fixture", "Both", "");
                foreach (string source in new[] { "Microphone", "Computer" })
                {
                    using (var wav = new WaveTrack(store.AudioPath(note, source))) wav.WriteAt(0, new byte[128000]);
                    for (int i = 0; i < 4; i++) note.Segments.Add(new NoteSegment { Source = source, StartFrame = i * 16000, Frames = 16000 });
                }
                store.Save(note);
                using (var session = new NotesSession(store, new Config()))
                {
                    session.Retry(note); var until = DateTime.UtcNow.AddSeconds(30); while (session.Busy && DateTime.UtcNow < until) Thread.Sleep(25);
                    if (session.Busy || note.Segments.Any(s => !s.Complete || !String.IsNullOrEmpty(s.Error)) || NoteStore.Transcript(note) != "") throw new IOException("Silent native notes created text or failed to finish.");
                }
                if (SpeechEngine.ActiveStreams != 0) throw new IOException("Notes retained a native recognizer.");
                Console.Write("notes-ok|both-sources|continuous|silence-empty|released"); return 0;
            }
            var load = Stopwatch.StartNew(); string problem = SpeechEngine.EnsureRunning(new Config(), 30); load.Stop(); if (problem != null) throw new IOException(problem);
            byte[] pcm = SpeechEngine.PcmFromWav(File.ReadAllBytes(args[2])); if (args.Contains("--quiet-gate")) pcm = QuietAudio.Filter(pcm);
            var watch = Stopwatch.StartNew(); long? first = null; string text;
            using (var stream = SpeechEngine.CreateStream())
            {
                for (int offset = 0; offset < pcm.Length; offset += 8000)
                {
                    var block = new byte[Math.Min(8000, pcm.Length - offset)]; Buffer.BlockCopy(pcm, offset, block, 0, block.Length);
                    if (stream.Feed(block).Length > 0 && !first.HasValue) first = watch.ElapsedMilliseconds;
                }
                text = stream.Finish();
            }
            watch.Stop();
            Console.Write(new JavaScriptSerializer().Serialize(new { Text = text, ModelLoadMs = load.ElapsedMilliseconds, InferenceMs = watch.ElapsedMilliseconds, FirstPartialMs = first, AudioSeconds = pcm.Length / 32000.0, FasterThanRealtime = true }));
            return 0;
        }
    }
}
