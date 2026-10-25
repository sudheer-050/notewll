using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
namespace Notewll
{
    static class HistoryVerification
    {
        static object Field(object target, string name) { return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target); }
        static void Snapshot(Form form, string path) { Application.DoEvents(); using (var image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(path); } }
        public static void Run(string root)
        {
            var today = DateTime.Today;
            var examples = Enumerable.Range(0, 3).Select(i => new NoteDocument { Kind = "Dictation", Status = "Complete", CreatedUtc = today.AddDays(-i).ToUniversalTime().ToString("o") }).ToList();
            foreach (var example in examples) example.Segments.Add(new NoteSegment { Complete = true, RawText = "hello world", Text = "Hello world." });
            examples[0].Segments[0].Text = "Hello everyone.";
            var progress = SpeakingProgress.From(examples, today);
            if (progress.Sessions != 3 || progress.Changed != 1 || progress.Streak != 3 || progress.Level != 2 || progress.Next != 7) throw new Exception("Speaking progress calculation failed");
            if (SpeakingProgress.From(examples, today.AddDays(1)).Streak != 3 || SpeakingProgress.From(examples, today.AddDays(2)).Streak != 0 || SpeakingProgress.From(new NoteDocument[0], today).Level != 1) throw new Exception("Speaking streak boundary failed");
            Directory.CreateDirectory(root); Config.DiagnosticDataRoot(Path.Combine(root, "settings"));
            var store = new NoteStore(Path.Combine(root, "notes"));
            ActivityHistory.Record(store, "Draft the release notes", "Draft the release notes.", "chrome", DateTime.UtcNow.AddMinutes(-20), "Planning draft - Google Chrome");
            var meeting = store.Create("Product review", "Computer", ""); meeting.Status = "Saved"; meeting.Segments.Add(new NoteSegment { Source = "Computer", Complete = true, Frames = 16000, RawText = "Review the release plan and agree on the next steps.", Text = "Review the release plan and agree on the next steps." }); store.Save(meeting);
            var note = store.Create("Ideas for tomorrow", "Microphone", ""); note.Status = "Saved"; note.WrittenNotes = "Keep the planning checklist in one place."; note.CreatedUtc = DateTime.UtcNow.AddDays(-2).ToString("o"); store.Save(note);
            if (ActivityHistory.Match(store.List(), new[] { "Dictation", "Meeting" }, DateTime.Now.AddHours(-1), DateTime.Now, 10).Count != 2) throw new Exception("Combined filtering failed");
            if (Math.Abs(VoiceKpis.SavedMinutes(600, 40) - 11) > .001 || ActivityHistory.SentenceCount("Hello. Another sentence! Last thought") != 3) throw new Exception("KPI calculation failed");
            Application.EnableVisualStyles();
            using (var hub = new SettingsForm(new Config(), "", "", store))
            {
                hub.Show(); Application.DoEvents(); var pages = (Panel[])Field(hub, "pages"); var home = pages[0];
                var modes = (ComboBox)Field(hub, "correctionMode");
                var testRaw = (TextBox)Field(hub, "raw"); var testCleaned = (TextBox)Field(hub, "cleaned");
                modes.SelectedIndex = 0; hub.UpdateTranscript("um I I has a meeting", "");
                if (testCleaned.Text != "um I I has a meeting") throw new Exception("Verbatim mode selector changed words");
                modes.SelectedIndex = 1;
                if (testCleaned.Text != "I has a meeting.") throw new Exception("Filler mode selector rewrote grammar");
                Config modeSaved = null; hub.OnSaved = c => modeSaved = c; hub.SavePreferences();
                if (modeSaved == null || !modeSaved.CleanupEnabled || modeSaved.SmartCleanupEnabled) throw new Exception("Correction mode did not persist");
                if (modes.Items.Count != 2 || (string)modes.Items[0] != "Raw text" || (string)modes.Items[1] != "Modified text") throw new Exception("Unexpected text mode");
                modes.SelectedIndex = 0; hub.SavePreferences();
                if (modeSaved.CleanupEnabled || modeSaved.SmartCleanupEnabled) throw new Exception("Raw text mode did not persist");
                modes.SelectedIndex = 1; hub.SavePreferences();
                hub.UpdateTranscript("", ""); hub.ShowPage("Preferences"); hub.SelectSettingsCategory("Writing"); Application.DoEvents();
                Snapshot(hub, Path.Combine(root, "correction-modes.png")); hub.ShowPage("Home");
                var progressCard = ((Label)Field(hub, "speakingMetrics")).Parent;
                home.ScrollControlIntoView(progressCard); Application.DoEvents(); Snapshot(hub, Path.Combine(root, "speaking-progress.png"));
                hub.Size = hub.MinimumSize; Application.DoEvents(); home.ScrollControlIntoView(progressCard); Application.DoEvents(); Snapshot(hub, Path.Combine(root, "speaking-progress-compact.png"));
                home.AutoScrollPosition = Point.Empty;
                if (home.Controls.OfType<Surface>().Count() != 4 || home.Controls.OfType<Surface>().SelectMany(card => card.Controls.OfType<Label>()).Any(label => label.Text == "Your history")) throw new Exception("Home still contains history");
                var metrics = (VoiceKpis)Field(hub, "voiceKpis"); if (metrics.Right > metrics.Parent.ClientSize.Width || metrics.Bottom > metrics.Parent.ClientSize.Height) throw new Exception("KPI layout clipped after first show"); if (metrics.Words != 4 || metrics.Sentences != 1) throw new Exception("Home KPIs changed");
                var welcome = (Label)Field(hub, "homeWelcome");
                if (!welcome.Text.StartsWith("You've saved about ") || VoiceKpis.WelcomeHeadline(600, 40) != "You've saved about 11 minutes." || VoiceKpis.WelcomeHeadline(0, 40).Contains("saved") || VoiceKpis.WelcomeHeadline(600, 200).Contains("saved")) throw new Exception("Welcome savings message is missing or misleading");
                var typing = (NumericUpDown)Field(hub, "typingSpeed"); typing.Value = 80;
                if (welcome.Text != VoiceKpis.WelcomeHeadline(metrics.Words, 80) || metrics.TypingWpm != 80 || Config.Load().TypingWordsPerMinute != 80) throw new Exception("Typing preference lost"); typing.Value = 40;
                var navigation = (HubButton[])Field(hub, "navigation"); var practice = (Surface)Field(hub, "homePractice"); var toggle = (HubButton)Field(hub, "practiceToggle");
                var speech = (TextBox)Field(hub, "raw");
                if (navigation[1].Visible || speech.Visible || speech.Parent != practice || practice.Parent != home || pages[1].Controls.Count != 0) throw new Exception("Practice remains a separate page or is not collapsed on Home");
                toggle.PerformClick(); Application.DoEvents();
                if (hub.CurrentPage != "Home" || !speech.Visible || toggle.Text != "Hide test") throw new Exception("Home practice did not expand inline");
                hub.UpdateTranscript("a quick practice sentence", "A quick practice sentence.");
                if (store.List().Count != 3) throw new Exception("Practice changed history");
                Snapshot(hub, Path.Combine(root, "home-practice-expanded.png"));
                toggle.PerformClick(); if (speech.Visible || toggle.Text != "Try it") throw new Exception("Home practice did not collapse");
                hub.ShowPage("Playground"); if (hub.CurrentPage != "Home" || !speech.Visible) throw new Exception("Setup practice link did not route to Home");
                toggle.PerformClick(); hub.ShowPage("Home");
                Snapshot(hub, Path.Combine(root, "home.png"));
                hub.ShowPage("Notes");
                if (hub.Notes.VisibleNotes != 3 || hub.Notes.ClearHistory == null) throw new Exception("Notes lost history or bulk cleanup");
                var browser = (Panel)Field(hub.Notes, "notesBrowser"); var filters = (LibraryFilter[])Field(hub.Notes, "browserFilters");
                if (!browser.Visible || hub.Notes.Controls.OfType<SplitContainer>().Any()) throw new Exception("Notes browser is not full-width or retains split sidebar");
                for (int i = 1; i < filters.Length; i++) { filters[i].PerformClick(); if (hub.Notes.VisibleNotes != 1) throw new Exception("Category filter failed: " + filters[i].Text); }
                filters[0].PerformClick(); if (hub.Notes.VisibleNotes != 3) throw new Exception("All filter lost entries");
                if (browser.Controls.OfType<Panel>().SelectMany(panel => panel.Controls.OfType<HubButton>()).Any(button => new[] { "New note", "Record note", "Record meeting" }.Contains(button.Text))) throw new Exception("Creation buttons remain in library");
                if (ActivityHistory.Kind(new NoteDocument { Kind = "Meeting", Mode = "Microphone" }) != "Meeting" || ActivityHistory.Kind(new NoteDocument { Kind = "Note", Mode = "Computer" }) != "Note") throw new Exception("Categories depend on audio source rather than capture type");
                var content = (Panel)Field(hub.Notes, "browserContent");
                if (content.Controls.OfType<Label>().Count() != 2 || content.Controls.OfType<Surface>().Count() != 3) throw new Exception("Date-grouped browser rows missing");
                var order = content.Controls.OfType<Surface>().Select(row => (NoteDocument)row.Tag).ToArray();
                if (order[0].Id != meeting.Id || order[2].Id != note.Id) throw new Exception("Notes browser ignored date/time sort");
                hub.Notes.SearchFor("planning checklist"); if (hub.Notes.VisibleNotes != 1) throw new Exception("Search failed to find standalone note body"); hub.Notes.SearchFor("");
                hub.Notes.SelectNote(note.Id);
                var paper = (Surface)Field(hub.Notes, "personalSheet"); var noteTitle = (TextBox)Field(hub.Notes, "personalTitle"); var body = (TextBox)Field(hub.Notes, "written"); var backButton = (HubButton)Field(hub.Notes, "backToLibrary");
                if (!paper.Visible || body.ReadOnly || paper.Top < backButton.Bottom || noteTitle.Height < noteTitle.Font.Height) throw new Exception("Personal note is read-only or its heading is cropped");
                noteTitle.Text = "Editable planning note"; body.Text = "First line remains visible.\r\nSecond line stays editable.";
                var deadline = DateTime.UtcNow.AddSeconds(2);
                while (DateTime.UtcNow < deadline && store.List().Single(entry => entry.Id == note.Id).WrittenNotes != body.Text) { Application.DoEvents(); System.Threading.Thread.Sleep(15); }
                var savedNote = store.List().Single(entry => entry.Id == note.Id);
                if (savedNote.Title != noteTitle.Text || savedNote.WrittenNotes != body.Text) throw new Exception("Note title/body autosave failed");
                Snapshot(hub, Path.Combine(root, "personal-note-editor.png"));
                hub.Notes.ShowLibrary(); hub.Notes.SelectNote(note.Id);
                if (body.Text != savedNote.WrittenNotes || noteTitle.Text != savedNote.Title) throw new Exception("Personal edits did not survive reopening");
                hub.ClientSize = new Size(960, 740); Application.DoEvents();
                if (paper.Top < backButton.Bottom || noteTitle.Height < noteTitle.Font.Height || body.Height < body.Font.Height * 3) throw new Exception("Personal note clipped at compact size");
                Snapshot(hub, Path.Combine(root, "personal-note-compact.png"));
                hub.ClientSize = new Size(1080, 740); hub.Notes.ShowLibrary();
                var dictation = store.List().Single(entry => entry.Kind == "Dictation");
                if (dictation.TargetWindowTitle != "Planning draft - Google Chrome" || Native.WindowTitle(hub.Handle) != hub.Text) throw new Exception("Window title capture/persistence failed");
                var details = NotesEntryInfo.Details(store, dictation);
                if (details.Single(pair => pair.Key == "Written in").Value != "Google Chrome" || details.Single(pair => pair.Key == "Window").Value != dictation.TargetWindowTitle) throw new Exception("Info lost app or window context");
                dictation.TargetWindowTitle = null;
                if (!NotesEntryInfo.Details(store, dictation).Single(pair => pair.Key == "Window").Value.Contains("not recorded")) throw new Exception("Info fabricated a window title for old data");
                dictation.TargetWindowTitle = "Planning draft - Google Chrome";
                bool infoSeen = false; Exception infoFailure = null;
                using (var timer = new Timer { Interval = 200 })
                {
                    timer.Tick += (sender, args) => { var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(form => form != hub && form.Visible); if (dialog == null) return; timer.Stop();
                        try { if (dialog.Text != "Info") throw new Exception("Info menu opened wrong dialog"); var card = dialog.Controls.OfType<Surface>().Single(); var grid = card.Controls.OfType<BufferedScrollPanel>().Single().Controls.OfType<TableLayoutPanel>().Single(); if (grid.Controls.OfType<Label>().Single(label => label.AccessibleName == "Written in").Text != "Google Chrome") throw new Exception("Info dialog did not render app name"); infoSeen = true; Snapshot(dialog, Path.Combine(root, "entry-info.png")); }
                        catch (Exception ex) { infoFailure = ex; } finally { dialog.Close(); }
                    };
                    timer.Start();
                    var anchor = ((Panel)Field(hub.Notes, "browserContent")).Controls.OfType<Surface>().First().Controls.OfType<TableLayoutPanel>().Single().Controls.OfType<HubButton>().Single();
                    var menu = hub.Notes.ShowBrowserEntryMenu(dictation, anchor); menu.Items.Cast<ToolStripItem>().Single(item => item.Text == "Info").PerformClick(); if (!menu.IsDisposed) menu.Close(ToolStripDropDownCloseReason.ItemClicked); Application.DoEvents();
                }
                if (infoFailure != null) throw infoFailure; if (!infoSeen) throw new Exception("Info menu did not open");
                for (int pass = 0; pass < 24; pass++)
                {
                    var anchor = ((Panel)Field(hub.Notes, "browserContent")).Controls.OfType<Surface>().First().Controls.OfType<TableLayoutPanel>().Single().Controls.OfType<HubButton>().Single();
                    var popup = hub.Notes.ShowBrowserEntryMenu(note, anchor); bool disposedDuringClose = false;
                    popup.Closed += (s, e) => disposedDuringClose = popup.IsDisposed;
                    if (pass == 0)
                    {
                        var click = Message.Create(hub.Handle, 0x0201, new IntPtr(1), new IntPtr((20 << 16) | 300));
                        Application.FilterMessage(ref click);
                        if (popup.Visible) throw new Exception("Outside click did not dismiss options menu");
                    }
                    else popup.Close(pass % 2 == 0 ? ToolStripDropDownCloseReason.AppClicked : ToolStripDropDownCloseReason.Keyboard);
                    if (disposedDuringClose) throw new Exception("Menu was disposed inside its close event while Windows was still dismissing it");
                    Application.DoEvents(); if (!popup.IsDisposed || hub.IsDisposed || !browser.Visible || store.List().Count != 3) throw new Exception("Outside dismissal leaked menu, closed the app or changed data");
                }
                Snapshot(hub, Path.Combine(root, "notes-history.png"));
                hub.ClientSize = new Size(960, 740); Application.DoEvents(); Snapshot(hub, Path.Combine(root, "notes-history-compact.png"));
                hub.ClientSize = new Size(1080, 740); Application.DoEvents();
                for (int category = 1; category < filters.Length; category++) { filters[category].PerformClick(); Snapshot(hub, Path.Combine(root, "notes-category-" + category + ".png")); }
                filters[0].PerformClick();
                hub.Notes.SelectNote(store.List().Single(n => n.Kind == "Dictation").Id); hub.Notes.VerifyReviewLayout(); if (browser.Visible) throw new Exception("Entry did not open editor"); Snapshot(hub, Path.Combine(root, "notes.png"));
                ((HubButton)Field(hub.Notes, "backToLibrary")).PerformClick(); if (!browser.Visible) throw new Exception("Back to history did not work");
                bool cleanupOpened = false;
                using (var timer = new Timer { Interval = 200 })
                {
                    timer.Tick += (sender, args) => { var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(form => form != hub && form.Visible); if (dialog == null) return; timer.Stop(); cleanupOpened = dialog.Text == "Clear history"; dialog.Close(); };
                    timer.Start(); hub.Notes.ClearHistory();
                }
                if (!cleanupOpened || store.List().Count != 3) throw new Exception("Cleanup unavailable or cancel changed data");
                ActivityHistory.Record(store, "A new dictation", "A new dictation.", "test", DateTime.UtcNow);
                hub.ShowPage("Home"); hub.ShowPage("Notes"); if (hub.Notes.VisibleNotes != 4) throw new Exception("Notes did not refresh new dictation");
                hub.ShowPage("Home"); hub.ClientSize = new Size(960, 740); Snapshot(hub, Path.Combine(root, "home-compact.png"));
                for (int i = 0; i < 24; i++) ActivityHistory.Record(store, "Scroll sample " + i, "Scroll sample " + i + ". Check that history remains aligned while scrolling.", "test", DateTime.UtcNow.AddMinutes(-i));
                hub.ShowPage("Notes"); var viewport = (Panel)Field(hub.Notes, "browserList"); var contentPanel = (Panel)Field(hub.Notes, "browserContent"); var header = (Panel)Field(hub.Notes, "browserHeader"); var headerBounds = header.Bounds;
                for (int step = 0; step < 30; step++) { Native.SendMessage(viewport.Handle, 0x0115, new IntPtr(1), IntPtr.Zero); Application.DoEvents(); }
                if (viewport.AutoScrollPosition.Y >= 0 || viewport.HorizontalScroll.Visible || header.Bounds != headerBounds) throw new Exception("Library scrolling shifted header or created horizontal scrolling");
                for (int step = 0; step < 30; step++) { Native.SendMessage(viewport.Handle, 0x0115, IntPtr.Zero, IntPtr.Zero); Application.DoEvents(); }
                if (contentPanel.Controls.OfType<Surface>().Select(row => row.Width).Distinct().Count() != 1) throw new Exception("History card widths drifted while scrolling");
                Snapshot(hub, Path.Combine(root, "notes-after-scrolling.png")); hub.Close();
            }
            var voiceStore = new NoteStore(Path.Combine(root, "voice-notes")); var voice = voiceStore.Create("Voice note", "Microphone", ""); voice.Status = "Saved"; voice.Kind = "Note";
            voice.Segments.Add(new NoteSegment { Source = "Microphone", Complete = true, Frames = 16000, RawText = "Keep these original words.", Text = "Keep these original words." }); voiceStore.Save(voice);
            using (var voiceHub = new SettingsForm(new Config(), "", "", voiceStore))
            {
                voiceHub.Show(); voiceHub.ShowPage("Notes"); voiceHub.Notes.SelectNote(voice.Id);
                var voiceBody = (TextBox)Field(voiceHub.Notes, "written");
                if (voiceBody.ReadOnly || voiceBody.Text != "Keep these original words.") throw new Exception("Recorded personal note did not receive an editable copy");
                voiceBody.Clear(); typeof(NotesPane).GetMethod("SaveEdits", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(voiceHub.Notes, null);
                voiceHub.Notes.ShowLibrary(); voiceHub.Notes.SelectNote(voice.Id);
                if (voiceBody.Text != "" || voiceStore.List().Single().Segments[0].RawText != "Keep these original words.") throw new Exception("Original transcript changed or erased edits were refilled");
                voiceHub.Notes.NewWrittenNote(); ((TextBox)Field(voiceHub.Notes, "personalTitle")).Text = "Title-only personal note";
                typeof(NotesPane).GetMethod("SaveEdits", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(voiceHub.Notes, null);
                if (!voiceStore.List().Any(entry => entry.Title == "Title-only personal note" && entry.Kind == "Note")) throw new Exception("Title-only note was not saved");
                voiceHub.Close();
            }
            File.WriteAllText(Path.Combine(root, "result.txt"), "PASS: Home has actions, KPIs and collapsed inline practice; no Practice sidebar entry; setup alias, expand/collapse and history isolation verified; all history remains in Notes; original text preserved; Notes refreshes new dictations; bulk cleanup opens from Notes and cancellation preserves data; typing speed and KPI calculations pass; startup savings headline; Info menu/dialog, app/window context persistence and honest old-entry fallback pass; personal title/body editing, timed autosave, reopen and no header overlap at normal/compact sizes pass; outside-click message plus 23 dismissal/reopen cycles pass without disposal errors; full-width Notes browser without sidebar; All/Dictations/Notes/Meetings filters, date/time grouping and editor/back navigation verified.");
        }
    }
}
