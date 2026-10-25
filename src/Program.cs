using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Notewll
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            bool background = args.Contains("--background");
            if (args.Length > 0 && args[0].StartsWith("--") && !background) return Cli(args);
            bool created;
            using (var mutex = new Mutex(true, "LocalDictation.SingleInstance", out created))
            {
                if (!created)
                {
                    try { using (var signal = EventWaitHandle.OpenExisting("LocalDictation.ShowHub")) signal.Set(); }
                    catch (WaitHandleCannotBeOpenedException) { }
                    return 0;
                }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                try { Application.Run(new DictationApp(!background)); return 0; }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Notewll could not start", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
            }
        }

        // Headless entry points used by the test suite and by manual verification. They run the same
        // classes as the tray app; none of them write audio or transcripts to disk.
        static int Cli(string[] args)
        {
            try
            {
                switch (args[0])
                {
                    case "--roadmap-smoke": RoadmapVerification.Run(args[1]); return 0;
                    case "--handsfree-worker-smoke": HandsFreeVerification.Worker(args[1], args[2], args[3], args.Skip(4).ToArray()); return 0;
                    case "--handsfree-preview":
                        {
                            System.IO.Directory.CreateDirectory(args[1]); Config.DiagnosticDataRoot(System.IO.Path.Combine(args[1], "data"));
                            Application.EnableVisualStyles();
                            using (var form = new SettingsForm(new Config(), "", "", new NoteStore(System.IO.Path.Combine(args[1], "notes")))) { form.ShowPage("Preferences"); form.SelectSettingsCategory("Hands-free"); form.Show(); Application.DoEvents(); CaptureHub(form, System.IO.Path.Combine(args[1], "handsfree.png")); form.CloseToTray = false; form.Close(); }
                            Console.Write("handsfree-preview-ok"); return 0;
                        }
                    case "--handsfree-smoke": HandsFreeVerification.Run(args[1]); return 0;
                    case "--ball-preferences-smoke":
                        BallVerification.Run(args[1]); return 0;
                    case "--pronunciation-smoke":
                        VoiceDictionaryVerification.Run(args[1], args.Length > 2 ? args[2] : null); return 0;
                    case "--personalization-smoke":
                        {
                            var config = new Config();
                            config.Personalization.Add(new PersonalRule { Kind = "Word", Trigger = "sue dear", Replacement = "Sudheer" });
                            config.Personalization.Add(new PersonalRule { Kind = "Snippet", Trigger = "my intro", Replacement = "Hello everyone, welcome to our meeting." });
                            var json = new System.Web.Script.Serialization.JavaScriptSerializer(); config = json.Deserialize<Config>(json.Serialize(config));
                            if (String.Join(",", PersonalRule.Vocabulary(config)) != "Sudheer") throw new Exception("Speech vocabulary includes snippets or lost a saved spelling.");
                            if (PersonalRule.Apply(config, "sue dear said my intro", true) != "Sudheer said my intro") throw new Exception("Pre-grammar corrections expanded a snippet.");
                            if (PersonalRule.Apply(config, "sue dearest", true) != "sue dearest") throw new Exception("Corrections ignored word boundaries.");
                            Console.Write("personalization-ok"); return 0;
                        }
                    case "--voice-smoke":
                        {
                            var store = new NoteStore(args[1]); var note = store.Create("Voice check", "Both", "");
                            var a = new double[256]; a[0] = 1; var b = new double[256]; b[1] = 1;
                            var first = new NoteSegment { Source = "Computer", RawText = "We should review the report.", Complete = true, VoiceEmbedding = a }; MeetingVoices.Assign(note, first); note.Segments.Add(first);
                            var other = new NoteSegment { Source = "Computer", RawText = "I agree with that decision.", Complete = true, VoiceEmbedding = b }; MeetingVoices.Assign(note, other); note.Segments.Add(other);
                            var intro = new NoteSegment { Source = "Computer", RawText = "Hi, I am Sudheer.", Complete = true, VoiceEmbedding = a }; MeetingVoices.Assign(note, intro); note.Segments.Add(intro);
                            if (first.VoiceId != intro.VoiceId || other.VoiceId == first.VoiceId || first.SpeakerLabel != "Sudheer" || !String.IsNullOrEmpty(other.SpeakerLabel)) throw new Exception("Voice clustering or name linking failed.");
                            store.Save(note); var saved = store.List()[0]; if (saved.Segments[0].SpeakerLabel != "Sudheer" || !NoteStore.Transcript(saved).StartsWith("Speakers -")) throw new Exception("Voice review persistence failed.");
                            if (NoteStore.Transcript(NoteExport.Scope(saved, 0, false)).Contains("Speakers -")) throw new Exception("Export leaked the speaker index.");
                            Console.Write("voices-ok"); return 0;
                        }
                    case "--quality-transcribe":
                        Console.Write(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(SpeechQuality.Recognize(new Config { QualityEnabled = true, QualityPython = args[1] }, SpeechEngine.PcmFromWav(System.IO.File.ReadAllBytes(args[2])), true))); SpeechQuality.Shutdown(); return 0;
                    case "--quality-refine":
                        Console.Write(SpeechQuality.Refine(new Config { QualityEnabled = true, QualityPython = args[1] }, args[2])); SpeechQuality.Shutdown(); return 0;
                    case "--accurate-batch":
                    case "--accurate-stream-batch":
                        {
                            var cfg = new Config { AccurateDictationEnabled = true, QualityPython = args[1] };
                            var warm = Stopwatch.StartNew();
                            try
                            {
                                AccurateSpeech.Warmup(cfg); warm.Stop();
                                Console.WriteLine(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new { Ready = AccurateSpeech.Ready, WarmupMs = warm.ElapsedMilliseconds, Backend = "Parakeet CPU + Silero speech detection" }));
                                bool grammar = false; // Accurate shortcuts use ASR punctuation and conservative local cleanup.
                                var grammarConfig = cfg.Copy(); grammarConfig.QualityEnabled = grammar;
                                if (grammar) SpeechQuality.TryRefine(grammarConfig, "Hello.");
                                foreach (string file in args.Skip(2).Where(value => value != "--grammar"))
                                {
                                    byte[] pcm = SpeechEngine.PcmFromWav(System.IO.File.ReadAllBytes(file));
                                    if (args[0] == "--accurate-stream-batch")
                                    {
                                        var live = new LiveGrammar(rawText => SpeechQuality.TryRefine(grammarConfig, rawText));
                                        var replay = FastDictationChecks.Replay(cfg, live, pcm, true, grammar);
                                        Console.WriteLine(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new { File = System.IO.Path.GetFileName(file), Replay = replay }));
                                        continue;
                                    }
                                    var timer = Stopwatch.StartNew();
                                    string raw = AccurateSpeech.Transcribe(cfg, pcm);
                                    string text = WritingFormat.Compose(cfg, raw);
                                    Console.WriteLine(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new { File = System.IO.Path.GetFileName(file), InferenceMs = timer.ElapsedMilliseconds, Raw = raw, Text = text }));
                                }
                            }
                            finally { SpeechQuality.Shutdown(); AccurateSpeech.Shutdown(); }
                            return 0;
                        }
                    case "--history-smoke":
                        HistoryVerification.Run(args[1]); return 0;
                    case "--ux-smoke":
                        UxVerification.Run(args[1]); return 0;
                    case "--smart-cleanup-smoke": SmartCleanupVerification.Run(); return 0;
                    case "--smart-cleanup-batch":
                        {
                            var cfg = new Config(); SmartCleanup.Warm();
                            foreach (string raw in args.Skip(1)) { var timer = Stopwatch.StartNew(); string baseline = WritingFormat.Compose(cfg, raw); string text = SmartCleanup.Refine(cfg, baseline) ?? baseline; Console.WriteLine(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new { Raw = raw, Text = text, Candidate = SmartCleanup.LastCandidate, Status = SmartCleanup.Status, InferenceMs = timer.ElapsedMilliseconds })); }
                            return 0;
                        }
                    case "--smart-cleanup":
                        {
                            var cfg = new Config(); var timer = Stopwatch.StartNew(); SmartCleanup.Warm(); string raw = String.Join(" ", args.Skip(1)); string baseline = WritingFormat.Compose(cfg, raw); string text = SmartCleanup.Refine(cfg, baseline) ?? baseline;
                            Console.Write(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new { Raw = raw, Text = text, Status = SmartCleanup.Status, ElapsedMs = timer.ElapsedMilliseconds })); return 0;
                        }
                    case "--format-text":
                        Console.Write(WritingFormat.Compose(new Config { WritingStyle = args[1] }, args[2])); return 0;
                    case "--validate-update":
                        ReleaseUpdates.ParseManifest(System.IO.File.ReadAllText(args[1])); Console.Write("valid"); return 0;
                    case "--verify-update-signature":
                        Console.Write(ReleaseUpdates.Trusted(args[1], args[2]) ? "valid" : "invalid"); return 0;
                    case "--engine-stress":
                    case "--native-notes-smoke":
                    case "--diagnostic-transcribe":
                    case "--playback-smoke":
                    case "--review-smoke":
                        return Diagnostics.Run(args);
                    case "--toolbar-preview":
                    case "--toolbar-smoke":
                        {
                            Application.EnableVisualStyles();
                            using (var toolbar = new FloatingDock())
                            {
                                int writes = 0, notes = 0, meetings = 0, cancellations = 0;
                                ((System.Windows.Forms.Timer)typeof(FloatingDock).GetField("hover", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(toolbar)).Stop();
                                toolbar.CancelWrite = () => cancellations++; toolbar.Configure(true); toolbar.ToggleWrite = () => writes++; toolbar.ToggleNote = () => notes++; toolbar.ToggleMeeting = () => meetings++;
                                IntPtr foreground = Native.GetForegroundWindow(); toolbar.RestorePosition(Int32.MinValue, Int32.MinValue); toolbar.Show(); Application.DoEvents();
                                if (Native.GetForegroundWindow() != foreground) throw new Exception("Toolbar took focus.");
                                if (Math.Max(toolbar.Width, toolbar.Height) >= 120 || Math.Min(toolbar.Width, toolbar.Height) > 80) throw new Exception("Resting toolbar is too large.");
                                if (args[0] == "--toolbar-preview") CaptureDock(toolbar, System.IO.Path.ChangeExtension(args[1], ".collapsed.png"));
                                toolbar.SetExpanded(true, true); Application.DoEvents();
                                foreach (var button in toolbar.Controls.OfType<DockButton>().Where(b => b.AccessibleName != "Open Notewll"))
                                {
                                    IntPtr point = (IntPtr)((button.Height / 2 << 16) | button.Width / 2);
                                    Native.SendMessage(button.Handle, 0x201, (IntPtr)1, point); Native.SendMessage(button.Handle, 0x202, IntPtr.Zero, point); Application.DoEvents();
                                }
                                if (Native.GetForegroundWindow() != foreground) throw new Exception("Clicking the toolbar took focus.");
                                if (writes != 1 || notes != 1 || meetings != 1) throw new Exception("Toolbar actions are not separate.");
                                if (toolbar.Controls.OfType<DockButton>().Count() != 3 || toolbar.Controls.OfType<DockButton>().Any(button => button.AccessibleName == "Open Notewll")) throw new Exception("Toolbar still has an Open app button");
                                toolbar.UpdateState(false, true, false, false, false, "Processing");
                                var cancelPoint = new IntPtr((toolbar.Height / 2 << 16) | toolbar.Width / 2); Native.SendMessage(toolbar.Handle, 0x201, new IntPtr(1), cancelPoint); Native.SendMessage(toolbar.Handle, 0x202, IntPtr.Zero, cancelPoint); if (cancellations != 1 || writes != 1) throw new Exception("Processing cancellation is unavailable without the fourth button");
                                toolbar.UpdateState(false, false, false, false, false, "Ready");
                                var area = Screen.FromControl(toolbar).WorkingArea;
                                foreach (var point in new[] { area.Location, new System.Drawing.Point(area.Right - 1, area.Top), new System.Drawing.Point(area.Left, area.Bottom - 1), new System.Drawing.Point(area.Right - 1, area.Bottom - 1), new System.Drawing.Point(area.Left, area.Top + area.Height / 2), new System.Drawing.Point(area.Right - 1, area.Top + area.Height / 2), new System.Drawing.Point(area.Left + area.Width / 2, area.Top), new System.Drawing.Point(area.Left + area.Width / 2, area.Bottom - 1) })
                                {
                                    toolbar.SetExpanded(false, true); toolbar.RestorePosition(point.X, point.Y);
                                    bool side = point.Y == area.Top + area.Height / 2;
                                    if (toolbar.Width != toolbar.Height) throw new Exception("Resting toolbar must be a full circle.");
                                    toolbar.SetExpanded(true, true);
                                    if (Math.Abs(Math.Max(toolbar.Height, toolbar.Width) - 2 * Math.Min(toolbar.Height, toolbar.Width)) > 1) throw new Exception("Expanded toolbar must be a half circle.");
                                    if (!area.Contains(toolbar.Bounds)) throw new Exception("Expanded toolbar leaves the screen.");
                                }
                                if (args[0] == "--toolbar-preview")
                                {
                                    Action<string> capture = path => { for (int frame = 0; frame < 3; frame++) { Application.DoEvents(); toolbar.Refresh(); foreach (Control child in toolbar.Controls) child.Refresh(); Thread.Sleep(20); } CaptureDock(toolbar, path); };
                                    toolbar.SetExpanded(true, true); capture(args[1]);
                                    toolbar.UpdateState(false, false, true, true, true, "Recording meeting 02:18"); capture(System.IO.Path.ChangeExtension(args[1], ".recording.png"));
                                    toolbar.RestorePosition(area.Left, area.Top + area.Height / 2); toolbar.SetExpanded(false, true); capture(System.IO.Path.ChangeExtension(args[1], ".left-idle.png"));
                                    toolbar.SetExpanded(true, true); capture(System.IO.Path.ChangeExtension(args[1], ".left.png"));
                                    toolbar.RestorePosition(area.Right - 1, area.Top + area.Height / 2); toolbar.SetExpanded(true, true); capture(System.IO.Path.ChangeExtension(args[1], ".right.png"));
                                }

                                toolbar.UpdateState(false, false, false, false, false, "Ready");
                                var dpiField = typeof(FloatingDock).GetField("dpi", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                                foreach (float scale in new[] { 1f, 1.75f, 2f })
                                {
                                    dpiField.SetValue(toolbar, scale); toolbar.RestorePosition(area.Left + area.Width / 2, area.Bottom - 1); toolbar.SetExpanded(true, true);
                                    foreach (var button in toolbar.Controls.OfType<DockButton>()) if (button.Left < 0 || button.Top < 0 || button.Right > toolbar.ClientSize.Width || button.Bottom > toolbar.ClientSize.Height) throw new Exception("Toolbar button clipped at " + scale + " DPI scale");
                                    if (args[0] == "--toolbar-preview") CaptureDock(toolbar, System.IO.Path.ChangeExtension(args[1], ".dpi-" + (int)(scale * 100) + ".png"));
                                }
                                toolbar.Close(); Console.Write("toolbar-ok|buttons=3|no-focus-steal|edge-snap"); return 0;
                            }
                        }
                    case "--note-metadata-smoke":
                        {
                            var note = new NoteDocument { AutoTitle = true, Title = "Voice note" };
                            note.Segments.Add(new NoteSegment { Complete = true, RawText = "Hi, I am Sudheer. Remember to send the report tomorrow." }); NoteMetadata.UpdateTitle(note);
                            if (note.Title != "Remember to send the report tomorrow") throw new Exception("Automatic note title is incorrect: " + note.Title);
                            note.AutoTitle = false; note.Title = "My edited title"; NoteMetadata.UpdateTitle(note); if (note.Title != "My edited title") throw new Exception("Automatic details overwrote an edit.");
                            var typed = new NoteDocument { AutoTitle = true, WrittenNotes = "Follow up with Alex about the design review" }; NoteMetadata.UpdateTitle(typed); if (typed.Title != "Follow up with Alex about the design review") throw new Exception("Written-note title was not inferred.");
                            Console.Write("metadata-ok|automatic-titles|manual-edit-preserved|written-notes"); return 0;
                        }
                    case "--note-preferences":
                        var preferences = Config.Load(args[1]); Console.Write(preferences.NotesHotkey + "|" + preferences.NotesSource + "|" + preferences.MeetingSource + "|" + preferences.ToolbarX + "|" + preferences.ToolbarY); return 0;
                    case "--live-grammar-smoke":
                    case "--recognition-cancel-smoke":
                    case "--streaming-dictation-benchmark": return FastDictationChecks.Run(args);
                    case "--request-exit":
                        try { using (var signal = EventWaitHandle.OpenExisting("LocalDictation.Exit")) signal.Set(); return 0; }
                        catch (WaitHandleCannotBeOpenedException) { Console.Error.Write("Close the existing app from its tray menu first."); return 1; }
                    case "--paste-policy": Console.Write(PastePolicy.Resolve(args[1], args[2])); return 0;
                    case "--live-edit":
                        var edit = LiveEdit.Between(args[1], args[2]); Console.Write(edit.Remove + "|" + edit.Append); return 0;
                    case "--live-writing-smoke":
                        {
                            Application.EnableVisualStyles();
                            using (var form = new Form { Text = "Notewll input test" })
                            using (var field = new TextBox { Dock = DockStyle.Fill, Text = "Keep this. ", Multiline = true })
                            {
                                form.Controls.Add(field); form.Show(); form.Activate(); field.Focus(); field.SelectionStart = field.TextLength; Application.DoEvents();
                                if (!Native.WriteLive(form.Handle, LiveEdit.Between("", "um hello"))) throw new Exception("Live insertion was blocked.");
                                Application.DoEvents();
                                if (field.Text != "Keep this. um hello") throw new Exception("Raw speech did not appear.");
                                if (!Native.WriteLive(form.Handle, LiveEdit.Between("um hello", "Hello."))) throw new Exception("Refinement was blocked.");
                                Application.DoEvents(); if (field.Text != "Keep this. Hello.") throw new Exception("Refinement changed surrounding text: " + field.Text);
                                form.Close(); Console.Write("live-ok|raw-inserted|refined|surrounding-preserved"); return 0;
                            }
                        }
                    case "--notes-tone-probe":
                        {
                            var samples = new byte[32000 * 2];
                            for (int i = 0; i < samples.Length / 2; i++) BitConverter.GetBytes((short)(6000 * Math.Sin(2 * Math.PI * 440 * i / 16000))).CopyTo(samples, i * 2);
                            using (var sound = new System.Media.SoundPlayer(new System.IO.MemoryStream(MicRecorder.ToWav(samples))))
                            using (var session = new NotesSession(new NoteStore(args[1]), new Config(), wav => ""))
                            {
                                var store = new NoteStore(args[1]); session.Start("Controlled loopback probe", "Computer", "", "", ""); sound.PlaySync(); Thread.Sleep(250); session.Stop();
                                var until = DateTime.UtcNow.AddSeconds(10); while (session.Busy && DateTime.UtcNow < until) Thread.Sleep(50);
                                byte[] pcm = WaveTrack.ReadPcm(store.AudioPath(session.Document, "Computer"), 0, 160000);
                                int peak = 0; for (int i = 0; i + 1 < pcm.Length; i += 2) peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(pcm, i)));
                                if (session.Busy || peak < 500 || !String.IsNullOrEmpty(session.Document.CaptureError)) throw new Exception("Loopback did not capture the test tone: " + peak);
                                Console.Write("loopback-ok|peak=" + peak); return 0;
                            }
                        }
                    case "--notes-layout":
                        using (var f = new SettingsForm(new Config(), "", "", new NoteStore(args[1])))
                        {
                            f.Show(); f.ShowPage("Notes"); f.Size = f.MinimumSize; Application.DoEvents(); DumpControls(f, ""); f.Close(); return 0;
                        }
                    case "--notes-devices":
                        foreach (var endpoint in CoreAudio.List(false)) Console.WriteLine("Microphone|" + endpoint.Name);
                        foreach (var endpoint in CoreAudio.List(true)) Console.WriteLine("Computer|" + endpoint.Name);
                        return 0;
                    case "--notes-audio-probe":
                        {
                            var store = new NoteStore(args[1]);
                            using (var session = new NotesSession(store, new Config(), wav => ""))
                            {
                                session.Start("Audio capture probe", args[2], "", "", "");
                                Thread.Sleep(Int32.Parse(args[3])); session.Stop();
                                var until = DateTime.UtcNow.AddSeconds(20);
                                while (session.Busy && DateTime.UtcNow < until) Thread.Sleep(50);
                                if (session.Busy || !String.IsNullOrEmpty(session.Document.CaptureError)) throw new Exception(session.Document.CaptureError ?? "Capture did not finish.");
                                foreach (string source in args[2] == "Both" ? new[] { "Microphone", "Computer" } : new[] { args[2] })
                                {
                                    var pcm = WaveTrack.ReadPcm(store.AudioPath(session.Document, source), 0, Int32.MaxValue / 2);
                                    int peak = 0; for (int i = 0; i + 1 < pcm.Length; i += 2) peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(pcm, i)));
                                    Console.Write(source + "|frames=" + pcm.Length / 2 + "|peak=" + peak + ";");
                                }
                                return 0;
                            }
                        }
                    case "--restore-backup":
                        Console.Write(NotesData.Restore(new NoteStore(args[1]), args[2])); return 0;
                    case "--engine-lifetime-smoke":
                        {
                            SpeechEngine.DiagnosticRoot(args[1]);
                            using (var stream = SpeechEngine.CreateStream())
                            {
                                stream.Feed(new byte[32000]); SpeechEngine.Shutdown();
                                if (SpeechEngine.ActiveStreams != 1) throw new Exception("An active native stream was released prematurely.");
                                stream.Finish();
                            }
                            if (SpeechEngine.ActiveStreams != 0) throw new Exception("Native stream was leaked.");
                            Console.Write("engine-ok|deferred-shutdown|active-stream-retained|released"); return 0;
                        }
                    case "--release-smoke":
                        {
                            var store = new NoteStore(System.IO.Path.Combine(args[1], "notes"));
                            var note = store.Create("Design review", "Microphone", "Sudheer"); note.Status = "Saved"; note.WrittenNotes = "The critical road map needs review.";
                            note.Segments.Add(new NoteSegment { Source = "Microphone", Complete = true, Frames = 16000, RawText = "We should send the report tomorrow.", Text = "We should send the report tomorrow." }); store.Save(note);
                            using (var track = new WaveTrack(store.AudioPath(note, "Microphone"))) track.WriteAt(0, new byte[32000]);
                            using (var track = new WaveTrack(store.AudioPath(note, "Microphone"), true)) { track.WriteAt(16000, new byte[32000]); if (track.Frames != 32000) throw new Exception("Resumed audio overwrote earlier audio."); }
                            string backup = System.IO.Path.Combine(args[1], "backup.zip"); NotesData.Backup(store, backup);
                            var restored = new NoteStore(System.IO.Path.Combine(args[1], "restored")); if (NotesData.Restore(restored, backup) != 1 || NotesData.Restore(restored, backup) != 0) throw new Exception("Backup restore lost data or overwrote identifiers.");
                            string original = NoteStore.Transcript(note); note.Summary = NotesSummary.Generate(note); if (!note.Summary.Contains("send the report tomorrow") || original != NoteStore.Transcript(note)) throw new Exception("Excerpts changed the conversation.");
                            var config = new Config(); config.Theme = "Dark"; config.Personalization.Add(new PersonalRule { Kind = "Word", Trigger = "sue deer", Replacement = "Sudheer" }); config.Personalization.Add(new PersonalRule { Kind = "Snippet", Trigger = "my sign off", Replacement = "Thanks,\nSudheer" }); config.Validate();
                            if (config.Theme != "System") throw new Exception("Legacy appearance was not migrated.");
                            ThemeColors.Mode = "Dark"; if (ThemeColors.Mode != "System") throw new Exception("Application theme must remain fixed.");
                            if (PersonalRule.Apply(config.Copy(), "hello sue deer my sign off") != "hello Sudheer Thanks,\nSudheer") throw new Exception("Dictionary or snippets failed.");
                            string rules = System.IO.Path.Combine(args[1], "rules.json"); NotesData.WriteRules(rules, config.Personalization); if (NotesData.ReadRules(rules).Count != 2) throw new Exception("Rule import/export failed.");
                            Application.EnableVisualStyles();
                            using (var hub = new SettingsForm(config, "", "", store))
                            {
                                hub.Show(); Application.DoEvents(); hub.ShowPage("Notes"); hub.Notes.SearchFor("road map"); if (hub.Notes.VisibleNotes != 1) throw new Exception("Body search failed."); hub.Notes.SearchFor("nonexistent-query"); if (hub.Notes.VisibleNotes != 0) throw new Exception("Search returned unrelated notes.");
                                foreach (var category in new[] { "Recording", "Writing", "Notes", "App", "Storage", "About" }) { hub.ShowPage("Preferences"); hub.SelectSettingsCategory(category); Application.DoEvents(); }
                                hub.CloseToTray = false;
                            }
                            var recovery = new DictationRecovery { Audio = new byte[32000], Text = "recover me" }; recovery.Discard(); if (recovery.HasWork || recovery.Revision != 1) throw new Exception("Discard retained speech.");
                            store.Delete(note); if (store.CleanupDeleted(0) != 0 || store.List().Count != 0) throw new Exception("Retention default erased or retained the wrong notes.");
                            Console.Write("release-ok|append|backup|restore|search|rules|excerpts|themes|discard"); return 0;
                        }
                    case "--release-preview":
                        {
                            System.IO.Directory.CreateDirectory(args[1]); var store = new NoteStore(System.IO.Path.Combine(args[1], "sample-notes"));
                            var note = store.Create("Product planning", "Both", "Sudheer"); note.Kind = "Meeting"; note.Status = "Saved"; note.WrittenNotes = "Prepare a demo by Friday."; note.Segments.Add(new NoteSegment { Source = "Computer", Complete = true, RawText = "We should make the controls easier to find.", Text = "We should make the controls easier to find.", Frames = 64000 }); store.Save(note);
                            Application.EnableVisualStyles();
                            foreach (var theme in new[] { "System" }) using (var hub = new SettingsForm(new Config { Theme = theme }, "", "", store))
                            {
                                hub.Show(); Application.DoEvents(); hub.Size = hub.MinimumSize;
                                foreach (var page in new[] { "Home", "Notes", "Playground", "Help" }) { hub.ShowPage(page); if (page == "Notes") hub.Notes.SelectNote(note.Id); hub.Refresh(); Application.DoEvents(); if (page == "Notes") hub.Notes.VerifyReviewLayout(); CaptureHub(hub, System.IO.Path.Combine(args[1], theme + "-" + page + ".png")); }
                                hub.ShowPage("Preferences");
                                foreach (var category in new[] { "Recording", "Writing", "Notes", "App", "Storage", "About" }) { hub.SelectSettingsCategory(category); hub.Refresh(); Application.DoEvents(); CaptureHub(hub, System.IO.Path.Combine(args[1], theme + "-Settings-" + category + ".png")); }
                                hub.CloseToTray = false;
                            }
                            Console.Write("release-preview-ok"); return 0;
                        }
                    case "--note-export-smoke":
                        {
                            var store = new NoteStore(System.IO.Path.Combine(args[1], "store"));
                            var note = store.Create("Research & planning", "Both", "Sudheer"); note.Status = "Saved";
                            note.WrittenNotes = "Separate personal annotations.";
                            note.Segments.Add(new NoteSegment { Source = "Computer", StartFrame = 16000, Complete = true, RawText = "Hello Alex. The budget is 42,000.", Text = "This must never replace the original conversation." });
                            note.Segments.Add(new NoteSegment { Source = "Microphone", StartFrame = 32000, Complete = true, Text = "" });
                            note.Segments.Add(new NoteSegment { Source = "Computer", StartFrame = 48000, Complete = false, Error = "test failure" });
                            if (NoteStore.Transcript(note).Contains("speech recognized") || NoteStore.Transcript(note).Contains("test failure") || NoteStore.Transcript(note).Contains("never replace")) throw new Exception("Non-speech or rewritten text leaked into the conversation.");
                            foreach (string extension in new[] { "xlsx", "csv", "docx", "pdf" }) NoteExport.Save(note, System.IO.Path.Combine(args[1], "conversation." + extension));
                            note.WrittenNotes = "=2+3"; NoteExport.Save(note, System.IO.Path.Combine(args[1], "formula.csv"));
                            note.WrittenNotes = "Separate personal annotations.";
                            note.Title = "Unicode: Jos\u00e9 & \u674e";
                            for (int i = 0; i < 100; i++) note.Segments.Add(new NoteSegment { Source = "Computer", Complete = true, RawText = "Conversation line " + i + ": Check the meeting notes and send the report tomorrow." });
                            NoteExport.Save(note, System.IO.Path.Combine(args[1], "multipage.pdf"));
                            var quiet = new byte[32000]; for (int i = 0; i < quiet.Length; i += 2) BitConverter.GetBytes((short)40).CopyTo(quiet, i);
                            if (QuietAudio.Filter(quiet).Any(b => b != 0) || QuietAudio.HasSignal(QuietAudio.Filter(quiet))) throw new Exception("Quiet noise was retained.");
                            var speech = new byte[32000]; for (int i = 8000; i < 24000; i += 2) BitConverter.GetBytes((short)(1000 * Math.Sin(i * 0.03))).CopyTo(speech, i);
                            if (!QuietAudio.HasSignal(QuietAudio.Filter(speech))) throw new Exception("Audible signal was discarded.");
                            store.Save(note); store.Delete(note);
                            if (store.List().Count != 0 || !System.IO.Directory.GetDirectories(System.IO.Path.Combine(store.Root, ".deleted")).Any()) throw new Exception("Deletion was not recoverable.");
                            Console.Write("exports-ok|four-formats|raw-conversation|quiet-filter|recoverable-delete"); return 0;
                        }
                    case "--notes-smoke":
                        {
                            var store = new NoteStore(args[1]);
                            var note = store.Create("Meeting", "Both", "");
                            var audio = new byte[32000]; for (int i = 0; i < audio.Length; i += 2) BitConverter.GetBytes((short)1000).CopyTo(audio, i);
                            using (var track = new WaveTrack(store.AudioPath(note, "Microphone"))) { track.WriteAt(0, audio); track.WriteAt(32000, audio); }
                            using (var track = new WaveTrack(store.AudioPath(note, "Computer"))) { track.WriteAt(16000, audio); }
                            var restored = store.List().Single();
                            if (restored.Segments.Count != 2 || restored.DurationSeconds != 3) throw new Exception("Interrupted audio was not recovered.");
                            using (var retry = new NotesSession(store, new Config(), wav => "Hi, I am Sudheer. Remember to send the report."))
                            {
                                retry.Retry(restored); var until = DateTime.UtcNow.AddSeconds(10);
                                while (retry.Busy && DateTime.UtcNow < until) Thread.Sleep(20);
                                if (retry.Busy || restored.Status != "Saved" || restored.UserName != "Sudheer" || store.SavedName != "Sudheer") throw new Exception("Name detection or retry failed.");
                            }
                            restored.WrittenNotes = "Follow up tomorrow."; store.Save(restored);
                            var loaded = store.List().Single();
                            if (loaded.WrittenNotes != "Follow up tomorrow." || !NoteStore.Transcript(loaded).Contains("Computer audio (introduction: Sudheer)")) throw new Exception("Note persistence failed.");
                            var silence = WaveTrack.ReadPcm(store.AudioPath(note, "Microphone"), 16000, 16000);
                            if (silence.Any(b => b != 0)) throw new Exception("Silence gaps were compressed.");
                            if (Introductions.Find("I am working. I'm happy. My name is Alex. I am Sudheer.").Count != 2) throw new Exception("Introduction extraction failed.");
                            loaded.NameEdited = true; loaded.AutoTitle = false; loaded.Title = "Edited meeting title"; loaded.Segments[0].Complete = false;
                            using (var edited = new NotesSession(store, new Config(), wav => "My name is Alex. We should send the report."))
                            {
                                edited.Retry(loaded); var until = DateTime.UtcNow.AddSeconds(10); while (edited.Busy && DateTime.UtcNow < until) Thread.Sleep(20);
                                if (edited.Busy || loaded.UserName != "Sudheer" || loaded.Title != "Edited meeting title") throw new Exception("Automatic metadata overwrote manual details.");
                            }
                            // A failed recognizer must retain both the source audio and a retryable segment.
                            loaded.Segments[0].Complete = false;
                            using (var retry = new NotesSession(store, new Config(), wav => { throw new Exception("Engine offline"); }))
                            {
                                retry.Retry(loaded); var until = DateTime.UtcNow.AddSeconds(10);
                                while (retry.Busy && DateTime.UtcNow < until) Thread.Sleep(20);
                                if (retry.Busy || loaded.Segments[0].Error != "Engine offline" || !System.IO.File.Exists(store.AudioPath(loaded, "Microphone"))) throw new Exception("Failed transcription lost audio.");
                            }
                            Console.Write("notes-ok|recovery|timeline|names|persistence|retry"); return 0;
                        }
                    case "--shortcut-sequence":
                        {
                            var binding = Shortcut.Parse(args[1]); var tracker = new ShortcutTracker();
                            foreach (var item in args.Skip(2))
                            {
                                int transition; int key = Shortcut.Parse(item.Substring(1)).Single();
                                bool consumed = tracker.Feed(binding, key, item[0] == '+', out transition);
                                Console.Write(transition + ":" + consumed + "|");
                            }
                            return 0;
                        }
                    case "--shortcut-recorder-smoke":
                        using (var form = new SettingsForm(new Config(), "", "", new NoteStore(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "preview-notes"))))
                        {
                            Config chosen = null; form.OnSaved = value => chosen = value;
                            form.BeginShortcutCapture();
                            form.CaptureShortcutKey(162, true); form.CaptureShortcutKey(160, true); form.CaptureShortcutKey(32, true);
                            form.CaptureShortcutKey(32, false); form.SavePreferences();
                            if (chosen == null || chosen.Hotkey != "Ctrl+Shift+Space" || form.IsCapturingShortcut) throw new Exception("Shortcut capture failed.");
                            form.BeginShortcutCapture(); form.CaptureShortcutKey(27, true); form.SavePreferences();
                            if (chosen.Hotkey != "Ctrl+Shift+Space" || form.IsCapturingShortcut) throw new Exception("Cancel changed shortcut.");
                            Console.Write("recorder-ok|Ctrl+Shift+Space|cancel-preserved"); return 0;
                        }
                    case "--cleanup": Console.Write(TextCleanup.Clean(String.Join(" ", args.Skip(1)))); return 0;
                    case "--validate-config":
                        try { var c = Config.Load(args[1]); Console.Write(c.Hotkey + "|" + c.Microphone + "|" + c.CleanupEnabled + "|" + c.AutoPaste + "|" + c.StartWithWindows); return 0; }
                        catch (Exception ex) { Console.Error.Write(ex.Message); return 2; }
                    case "--speech-preferences":
                        var speechPreferences = Config.Load(args[1]); Console.Write(speechPreferences.AccurateDictationEnabled + "|" + speechPreferences.QualityEnabled); return 0;
                    case "--state-sequence":
                        try { var s = new StateMachine(); foreach (var name in args.Skip(1)) s.Move((AppState)Enum.Parse(typeof(AppState), name, true)); Console.Write(s.Current); return 0; }
                        catch (Exception ex) { Console.Error.Write(ex.Message); return 3; }
                    case "--resolve-device":
                        {
                            bool fell; int index = DeviceSelector.Resolve(args[1], args.Skip(2).ToList(), out fell);
                            Console.Write(index + "|" + fell); return 0;
                        }
                    case "--list-devices":
                        foreach (var n in Mics.List()) Console.WriteLine(n);
                        return 0;
                    case "--capture-test": return CaptureTest(args[1], Int32.Parse(args[2]));
                    case "--stt-health":
                        {
                            string detail; var cfg = args.Length > 1 ? new Config { SttUrl = args[1] } : Config.Load();
                            bool ok = SpeechEngine.Healthy(cfg, out detail); Console.Write((ok ? "healthy" : "down") + "|" + detail); return ok ? 0 : 4;
                        }
                    case "--installed-speech-check":
                    {
                        string problem = SpeechEngine.EnsureRunning(new Config(), 60);
                        Console.Write(problem ?? "speech-ready|offline=true");
                        return problem == null ? 0 : 4;
                    }
                    case "--ensure-stt":
                        {
                            string problem = SpeechEngine.EnsureRunning(Config.Load(), 150, m => Console.Error.WriteLine(m));
                            Console.Write(problem ?? "running"); return problem == null ? 0 : 4;
                        }
                    case "--transcribe-wav":
                        Console.Write(SpeechEngine.TranscribeEnsured(Config.Load(), System.IO.File.ReadAllBytes(args[1]))); return 0;
                    case "--dictate-test": return DictateTest(args);
                    case "--settings-smoke":
                        {
                            Application.EnableVisualStyles();
                            using (var f = new SettingsForm(Config.Load(), "um hello hello there", ""))
                            {
                                var t = new System.Windows.Forms.Timer { Interval = 800 };
                                t.Tick += (s, e) => { t.Stop(); Console.Write("settings-ok|" + f.Summary); f.Close(); };
                                t.Start(); f.ShowDialog();
                            }
                            return 0;
                        }
                    case "--hub-preview":
                    case "--hub-smoke":
                        {
                            Application.EnableVisualStyles();
                            using (var f = new SettingsForm(new Config(), "", "", new NoteStore(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "preview-notes"))))
                            {
                                f.Show(); Application.DoEvents();
                                foreach (var page in new[] { "Home", "Notes", "Dictionary", "Snippets", "Style", "Preferences", "Help" })
                                {
                                    f.Controls.OfType<Panel>().SelectMany(p => p.Controls.OfType<Button>()).Single(b => b.AccessibleName == page).PerformClick();
                                    f.Refresh(); Application.DoEvents();
                                    if (f.CurrentPage != page) throw new InvalidOperationException("Navigation failed");
                                    if (args[0] == "--hub-preview")
                                    {
                                        System.IO.Directory.CreateDirectory(args[1]);
                                        CaptureHub(f, System.IO.Path.Combine(args[1], page + ".png"));
                                        var fullSize = f.Size; f.Size = f.MinimumSize; f.Refresh(); Application.DoEvents();
                                        CaptureHub(f, System.IO.Path.Combine(args[1], page + "-minimum.png"));
                                        f.Size = fullSize;
                                    }
                                }
                                f.UpdateTranscript("um please send the the draft", "");
                                if (!f.Summary.EndsWith("preview=Please send the draft.")) throw new InvalidOperationException("Cleanup preview failed");
                                Config saved = null; f.OnSaved = value => saved = value; f.SavePreferences();
                                if (saved == null || saved.Hotkey != "F8") throw new InvalidOperationException("Preference apply failed");
                                f.CloseToTray = true; f.Close(); Application.DoEvents();
                                if (f.Visible || f.IsDisposed) throw new InvalidOperationException("Close-to-tray failed");
                                f.CloseToTray = false;
                                Console.Write("hub-ok|pages=7|preferences=applied|preview=working|close=tray");
                            }
                            return 0;
                        }
                    default: Console.Error.Write("Unknown option " + args[0]); return 64;
                }
            }
            catch (Exception ex) { Console.Error.Write(ex.ToString()); return 5; }
        }

        static void DumpControls(Control parent, string indent)
        {
            foreach (Control c in parent.Controls) { if (!c.Visible) continue; Console.WriteLine(indent + c.GetType().Name + " " + c.Bounds + " client=" + c.ClientSize + " " + (c.Text.Length < 30 ? c.Text : "")); DumpControls(c, indent + " "); }
        }
        static void RefreshPreview(Control control) { control.Refresh(); foreach (Control child in control.Controls) if (child.Visible) RefreshPreview(child); }
        static void CaptureDock(Form form, string path)
        {
            Application.DoEvents();
            using (var bitmap = new System.Drawing.Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(System.Drawing.Point.Empty, form.Size)); bitmap.Save(path); }
        }
        static void CaptureHub(Form form, string path)
        {
            Application.DoEvents(); RefreshPreview(form); Application.DoEvents(); RefreshPreview(form);
            using (var image = new System.Drawing.Bitmap(form.Width, form.Height))
            using (var graphics = System.Drawing.Graphics.FromImage(image))
            {
                var dc = graphics.GetHdc();
                try { if (!Native.PrintWindow(form.Handle, dc, 0u)) throw new InvalidOperationException("Window preview capture failed"); }
                finally { graphics.ReleaseHdc(dc); }
                image.Save(path);
            }
        }

        static int CaptureTest(string mic, int ms)
        {
            using (var rec = new MicRecorder(mic))
            {
                rec.Start();
                Thread.Sleep(ms);
                int peak = rec.Peak; byte[] pcm = rec.Stop();
                Console.Write("device=" + rec.DeviceName + "|fellBack=" + rec.FellBack + "|bytes=" + pcm.Length + "|seconds=" + (pcm.Length / 32000.0).ToString("0.00") + "|peak=" + peak);
                return pcm.Length > 0 ? 0 : 6;
            }
        }

        // dictate-test <mic|wav:path> <ms> [windowTitleSubstring|-] : capture (or read a WAV) -> the bundled speech engine -> cleanup -> clipboard -> paste.
        static int DictateTest(string[] args)
        {
            var cfg = Config.Load(); cfg.Microphone = args[1];
            string titlePart = args.Length > 3 ? args[3] : "-";
            byte[] wav;
            if (args[1].StartsWith("wav:", StringComparison.OrdinalIgnoreCase)) wav = System.IO.File.ReadAllBytes(args[1].Substring(4));
            else
            {
                using (var rec = new MicRecorder(cfg.Microphone))
                {
                    rec.Start(); Thread.Sleep(Int32.Parse(args[2]));
                    int peak = rec.Peak; byte[] pcm = rec.Stop();
                    Console.WriteLine("device=" + rec.DeviceName + " bytes=" + pcm.Length + " peak=" + peak);
                    wav = MicRecorder.ToWav(MicRecorder.Normalize(pcm, peak));
                }
            }
            string raw = SpeechEngine.TranscribeEnsured(cfg, wav);
            string cleaned = TextCleanup.Clean(raw);
            Console.WriteLine("raw=" + raw); Console.WriteLine("cleaned=" + cleaned);
            if (titlePart == "-" || cleaned.Length == 0) return cleaned.Length == 0 ? 7 : 0;
            IntPtr hwnd = Native.FindWindowByTitle(titlePart);
            Console.WriteLine("window=" + hwnd);
            Native.Tap(0x12, true); Native.Tap(0x12, false); // lets a headless process take foreground
            var t = new Thread(() => System.Windows.Forms.Clipboard.SetDataObject(cleaned, true, 5, 100)); t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
            bool inserted = Native.Paste(hwnd);
            Console.WriteLine("target=" + titlePart + " inserted=" + inserted);
            return inserted ? 0 : 8;
        }
    }
}
