using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Notewll
{
    static class NotesSummary
    {
        public static string Generate(NoteDocument note)
        {
            var speech = note.Segments.Where(s => s.Complete && String.IsNullOrEmpty(s.Error) && !String.IsNullOrWhiteSpace(s.RawText ?? s.Text)).OrderBy(s => s.StartFrame).ToList();
            if (speech.Count == 0) return "No recognized speech to excerpt yet.";
            Func<NoteSegment, string> quote = s => Quote(note, s);
            var decisions = speech.Where(s => Regex.IsMatch(s.RawText ?? s.Text, @"(?i)\b(?:agree\w*|decid\w*|decision|approved?|chosen|choose)\b")).Take(8).ToList();
            var actions = speech.Where(s => Regex.IsMatch(s.RawText ?? s.Text, @"(?i)\b(?:will|should|need|tomorrow|deadline|action|next|follow up)\b")).Take(8).ToList();
            return "Local review - original source excerpts, not an AI summary. Check the recording; categories use keywords.\r\n\r\nOverview\r\n" + String.Join("\r\n\r\n", ConversationTools.Highlights(note).Select(hit => quote(note.Segments[hit.Segment]))) +
                "\r\n\r\nPossible decisions\r\n" + (decisions.Count == 0 ? "No matching statement found." : String.Join("\r\n\r\n", decisions.Select(quote))) +
                "\r\n\r\nPossible follow-ups\r\n" + (actions.Count == 0 ? "No matching statement found." : String.Join("\r\n\r\n", actions.Select(quote)));
        }
        static string Quote(NoteDocument note, NoteSegment s) { return "[" + TimeSpan.FromSeconds((s.TranscriptStartFrame ?? s.StartFrame) / 16000.0).ToString(@"hh\:mm\:ss") + "] " + NoteStore.Speaker(note, s) + "\r\n" + (s.RawText ?? s.Text); }
        public static string Answer(NoteDocument note, string query)
        {
            // ponytail: lexical retrieval quotes evidence; no semantic inference or invented owners/deadlines.
            var stop = new[] { "what", "when", "where", "who", "why", "how", "the", "did", "does", "was", "were", "are", "and", "for", "about", "with", "this", "that", "have", "will" };
            var terms = Regex.Matches(query ?? "", @"[\p{L}\p{N}]+").Cast<Match>().Select(m => m.Value.ToLowerInvariant()).Where(w => w.Length > 2 && !stop.Contains(w)).Distinct().ToArray();
            if (terms.Length == 0) return "Use a specific topic, name or phrase to find original statements.";
            var ranked = note.Segments.Where(s => s.Complete && String.IsNullOrEmpty(s.Error) && !String.IsNullOrWhiteSpace(s.RawText ?? s.Text)).Select(s => new { Segment = s, Score = terms.Count(term => Regex.IsMatch(s.RawText ?? s.Text, @"\b" + Regex.Escape(term) + @"\b", RegexOptions.IgnoreCase)) }).Where(x => x.Score > 0).OrderByDescending(x => x.Score).ThenBy(x => x.Segment.StartFrame).Take(5).ToList();
            return ranked.Count == 0 ? "No matching recognized speech found. Try different keywords or review the audio." : "Matching source statements - search results, not inferred answers.\r\n\r\n" + String.Join("\r\n\r\n", ranked.Select(x => Quote(note, x.Segment)));
        }
    }
    sealed partial class NotesPane
    {
        readonly HubButton pauseNote = new HubButton { Text = "Pause", Dock = DockStyle.Fill };
        readonly NotesPlayback player = new NotesPlayback();
        readonly TextBox findText = new ThemedTextBox { Width = 160, AccessibleName = "Find in conversation" };
        readonly TextBox excerptText = new ThemedTextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical };
        readonly TrackBar playPosition = new ThemedTrackBar { Width = 120, Minimum = 0, Maximum = 1, TickStyle = TickStyle.None, Height = 28 };
        readonly ComboBox audioSource = Choice(), speed = Choice(), sort = Choice(), filter = Choice();
        readonly Label playTime = new Label { AutoSize = true, Padding = new Padding(0, 5, 0, 0) };
        Button playButton, pausePlayback;
        TableLayoutPanel sourceMeters, reviewWorkspace;
        readonly Label captureDevices = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
        readonly ToolTip reviewHints = new ToolTip();
        string libraryQuery = "";
        bool playerOpen;
        TextBox librarySearch;
        readonly FlowLayoutPanel emptyState = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(12) };
        bool writingFresh;
        internal System.Collections.Generic.IEnumerable<NoteDocument> RecentEntries { get { return documents.OrderByDescending(n => n.CreatedUtc).ToArray(); } }
        internal int VisibleNotes { get { return history.Items.Count; } }
        internal void SearchFor(string query) { librarySearch.Text = query; }
        internal void SelectNote(string id) { browsingNotes = false; Select(documents.FirstOrDefault(n => n.Id == id)); }
        internal void VerifyReviewLayout()
        {
            if (selected == null || written.Text != selected.WrittenNotes || transcript.Text.Replace("\r", "") != NoteStore.Transcript(selected).Replace("\r", "") || !transcript.ReadOnly) throw new InvalidOperationException("Review fields lost content or read-only state.");
            if (!written.Visible || tabs.Height < 100 || Recording && (record.Bottom > record.Parent.ClientSize.Height || mode.Bottom > mode.Parent.ClientSize.Height || record.Height < 20 || mode.Height < 18)) throw new InvalidOperationException("Review controls are clipped or hidden.");
        }
        public void NewWrittenNote() { browsingNotes = false; writingFresh = true; Select(null); tabs.SelectedIndex = 0; written.Focus(); }
        static void SelectEndpoint(ComboBox box, string id)
        {
            for (int i = 0; i < box.Items.Count; i++) if (((AudioEndpoint)box.Items[i]).Id == id) { box.SelectedIndex = i; return; }
        }
        internal void PrepareHistoryDelete()
        {
            SaveEdits(); saveTimer.Stop(); player.Dispose();
            populating = true; selected = null; written.Clear(); title.Clear(); DisplayConversation(""); populating = false;
        }
        public void ReloadLibrary()
        {
            if (Busy) return;
            SaveEdits(); string selectedId = selected == null ? null : selected.Id;
            documents.Clear(); documents.AddRange(store.List());
            // Clear the old editor before clearing selection so refiltering cannot save it as a new note.
            populating = true; selected = null; written.Clear(); title.Clear(); populating = false;
            Refilter(); Select(documents.FirstOrDefault(note => note.Id == selectedId) ?? documents.FirstOrDefault());
        }
        void Refilter()
        {
            SaveEdits(); populating = true; history.Items.Clear();
            foreach (var note in Sorted(documents, config.NotesSort))
            {
                if (browserKind > 0 && ActivityHistory.Kind(note) != new[] { "", "Dictation", "Note", "Conversation", "Meeting" }[browserKind]) continue;
                if (filter.SelectedIndex == 1 && ActivityHistory.Kind(note) != "Note" || filter.SelectedIndex == 2 && note.Kind != "Meeting" || filter.SelectedIndex == 3 && note.Kind != "Conversation") continue;
                string searchable; lock (note) searchable = note.Title + " " + note.UserName + " " + note.Kind + " " + note.WrittenNotes + " " + note.Tags + " " + String.Join(" ", note.Participants) + " " + NoteStore.Transcript(note);
                if (ConversationTools.Matches(note, libraryQuery, searchable)) history.Items.Add(note);
            }
            history.SelectedItem = selected; populating = false; RenderNotesBrowser();
        }
        internal static System.Collections.Generic.IEnumerable<NoteDocument> Sorted(System.Collections.Generic.IEnumerable<NoteDocument> notes, string order)
        {
            if (order == "Title") return notes.OrderBy(n => n.Title, StringComparer.CurrentCultureIgnoreCase).ThenByDescending(n => n.CreatedUtc);
            if (order == "Oldest") return notes.OrderBy(n => n.CreatedUtc);
            if (order == "Longest") return notes.OrderByDescending(n => n.DurationSeconds).ThenByDescending(n => n.CreatedUtc);
            return notes.OrderByDescending(n => n.CreatedUtc);
        }
        void BuildReviewTools(TableLayoutPanel library, TableLayoutPanel workspace, TabPage noteTab, TabPage transcriptTab, TextBox search, HubButton copy, HubButton export)
        {
            librarySearch = search;
            emptyState.Controls.Add(new Label { Text = "Capture what matters. Choose a starting point.", AutoSize = true, MaximumSize = new Size(260, 0), Margin = new Padding(0, 0, 0, 18) });
            Action<string, Action> start = (label, action) => { var button = new HubButton { Text = label, Width = 220, Height = 38 }; button.Click += (s, e) => action(); emptyState.Controls.Add(button); };
            start("New voice note", NewVoiceNote); start("New conversation", NewConversation); start("Record online meeting", RecordOnlineMeeting); start("Write a note", NewWrittenNote);
            noteTab.Controls.Add(emptyState); emptyState.BringToFront();
            library.RowCount = 6; library.RowStyles.Clear(); foreach (int height in new[] { 28, 34, 34, 34, -1, 42 }) library.RowStyles.Add(new RowStyle(height < 0 ? SizeType.Percent : SizeType.Absolute, height < 0 ? 100 : height));
            var fresh = library.GetControlFromPosition(0, 3); library.SetRow(fresh, 5); library.SetRow(history, 4);
            filter.Items.AddRange(new object[] { "All recordings and notes", "Personal notes", "Meetings", "Conversations" }); filter.SelectedIndex = 0; library.Controls.Add(filter, 0, 2); filter.SelectedIndexChanged += (s, e) => Refilter();
            sort.Items.AddRange(new object[] { "Newest", "Oldest", "Title", "Longest" }); sort.SelectedItem = config.NotesSort; sort.AccessibleName = "Sort notes"; library.Controls.Add(sort, 0, 3);
            sort.SelectedIndexChanged += (s, e) => { config.NotesSort = sort.Text; try { config.Save(); } catch (Exception ex) { uiError = "Sort applied for this session. Could not save preference: " + ex.Message; } Refilter(); };
            history.MouseMove += (s, e) => { int index = history.IndexFromPoint(e.Location); if (index >= 0) { var note = (NoteDocument)history.Items[index]; reviewHints.SetToolTip(history, note.Title + "\n" + note.CreatedUtc + " | " + note.Status); } };
            history.DrawMode = DrawMode.OwnerDrawFixed; history.ItemHeight = Math.Max(58, Font.Height * 2 + 20); history.FontChanged += (s, e) => history.ItemHeight = Math.Max(58, history.Font.Height * 2 + 20);
            history.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return; var note = (NoteDocument)history.Items[e.Index];
                using (var fill = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? ThemeColors.Selected : ThemeColors.Paper)) e.Graphics.FillRectangle(fill, e.Bounds);
                Color ink = SystemInformation.HighContrast && (e.State & DrawItemState.Selected) != 0 ? SystemColors.HighlightText : ThemeColors.Ink;
                TextRenderer.DrawText(e.Graphics, note.Title, Font, new Rectangle(e.Bounds.Left + 8, e.Bounds.Top + 4, e.Bounds.Width - 16, Font.Height + 5), ink, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                DateTime created; DateTime.TryParse(note.CreatedUtc, out created);
                string duration = note.DurationSeconds > 0 ? " | " + TimeSpan.FromSeconds(note.DurationSeconds).ToString(@"hh\:mm\:ss") : "";
                string detail = (note.Kind ?? "Note") + " | " + created.ToLocalTime().ToString("MMM d") + duration + " | " + note.Status;
                using (var small = new Font(Font.FontFamily, 8)) TextRenderer.DrawText(e.Graphics, detail, small, new Rectangle(e.Bounds.Left + 8, e.Bounds.Top + Font.Height + 10, e.Bounds.Width - 16, Font.Height + 4), SystemInformation.HighContrast ? ink : ThemeColors.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                e.DrawFocusRectangle();
            };
            pauseNote.Click += (s, e) => { if (session != null) session.TogglePause(); RefreshState(); };
            var conversation = Grid(1, 3); conversation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            conversation.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); conversation.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); conversation.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            transcriptTab.Controls.Remove(transcript); transcriptTab.Controls.Add(conversation);
            var find = Grid(3, 1); find.Padding = new Padding(0, 0, 0, 6);
            find.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); find.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78)); find.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            findText.Dock = DockStyle.Fill; findText.AccessibleName = "Search conversation";
            var next = new HubButton { Text = "Find next", Dock = DockStyle.Fill };
            var speakersButton = new HubButton { Text = "Speakers", Dock = DockStyle.Fill }; speakersButton.Click += (s, e) => EditSpeakerLabels();
            find.Controls.Add(findText, 0, 0); find.Controls.Add(next, 1, 0); find.Controls.Add(speakersButton, 2, 0);
            reviewHints.SetToolTip(findText, "Search this conversation (Ctrl+F)"); reviewHints.SetToolTip(speakersButton, "Edit speaker labels and voice samples");
            next.Click += (s, e) => FindNext(); findText.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; FindNext(); } };
            conversation.Controls.Add(find, 0, 0); conversation.Controls.Add(transcript, 0, 1);
            transcript.DoubleClick += (s, e) =>
            {
                int line = transcript.GetLineFromCharIndex(transcript.SelectionStart);
                for (int i = Math.Min(line, transcript.Lines.Length - 1); i >= 0; i--)
                { var match = Regex.Match(transcript.Lines[i], @"^\[(\d+:\d+:\d+)\]"); TimeSpan time; if (match.Success && TimeSpan.TryParse(match.Groups[1].Value, out time)) { var segment = selected.Segments.Where(x => x.Complete && String.IsNullOrEmpty(x.Error) && !String.IsNullOrWhiteSpace(x.RawText ?? x.Text)).OrderBy(x => x.StartFrame).ThenBy(x => x.Source).FirstOrDefault(x => (int)((x.TranscriptStartFrame ?? x.StartFrame) / 16000) == (int)time.TotalSeconds && transcript.Lines[i].Contains(NoteStore.Speaker(selected, x))); if (segment != null) audioSource.SelectedItem = segment.Source; StartPlayback((int)time.TotalMilliseconds); break; } }
            };
            var excerptTab = new TabPage("Local review"); var generate = new HubButton { Text = "Create overview from source quotes", Dock = DockStyle.Top, Height = 34 };
            generate.Click += (s, e) => { if (selected == null) return; lock (selected) { selected.Summary = NotesSummary.Generate(selected); store.Save(selected); excerptText.Text = selected.Summary; } };
            excerptTab.Controls.Add(excerptText); excerptTab.Controls.Add(generate); tabs.TabPages.Add(excerptTab);
            var questions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true }; var question = new ThemedTextBox { Width = 230, AccessibleName = "Search meeting statements" }; var ask = new HubButton { Text = "Find statements", Width = 140, Height = 32 };
            Action answer = () => { if (selected != null) lock (selected) excerptText.Text = NotesSummary.Answer(selected, question.Text); };
            ask.Click += (s, e) => answer(); question.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; answer(); } }; questions.Controls.AddRange(new Control[] { question, ask }); excerptTab.Controls.Add(questions); questions.BringToFront(); generate.BringToFront();
            var playback = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 2, Padding = new Padding(0, 6, 0, 0), Margin = Padding.Empty };
            playback.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); foreach (int width in new[] { 58, 58, 65, 78 }) playback.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width));
            playback.RowStyles.Add(new RowStyle(SizeType.Percent, 55)); playback.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            audioSource.Dock = DockStyle.Fill; audioSource.MinimumSize = new Size(95, 0); audioSource.AccessibleName = "Playback audio source";
            var play = new HubButton { Text = "Play", Dock = DockStyle.Fill }; var pause = new HubButton { Text = "Pause", Dock = DockStyle.Fill }; playButton = play; pausePlayback = pause;
            speed.Width = 70; speed.AccessibleName = "Playback speed"; speed.Items.AddRange(new object[] { "0.75x", "1x", "1.25x", "1.5x", "2x" }); speed.SelectedIndex = 1;
            playback.Controls.Add(audioSource, 0, 0); playback.Controls.Add(play, 1, 0); playback.Controls.Add(pause, 2, 0); playback.Controls.Add(speed, 3, 0); playback.Controls.Add(playTime, 4, 0);
            playPosition.AutoSize = false; playPosition.Dock = DockStyle.Fill; playPosition.AccessibleName = "Recording position"; playback.Controls.Add(playPosition, 0, 1); playback.SetColumnSpan(playPosition, 5);
            conversation.Controls.Add(playback, 0, 2);
            play.Click += (s, e) => StartPlayback(); pause.Click += (s, e) => { try { player.Pause(); } catch (Exception ex) { uiError = ex.Message; } };
            audioSource.SelectedIndexChanged += (s, e) => { player.Dispose(); playerOpen = false; };
            speed.SelectedIndexChanged += (s, e) => { try { if (playerOpen) player.Speed(new[] { 0.75, 1.0, 1.25, 1.5, 2.0 }[speed.SelectedIndex]); } catch (Exception ex) { uiError = "Playback speed unavailable: " + ex.Message; } };
            playPosition.MouseUp += (s, e) => StartPlayback(playPosition.Value);
            var actions = (TableLayoutPanel)export.Parent; actions.ColumnCount = 4; actions.ColumnStyles.Clear();
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
            actions.Controls.Remove(retry); export.Text = "Export PDF";
            var more = new HubButton { Text = "More", Dock = DockStyle.Fill }; more.Click += (s, e) => ShowFilesMenu(more); actions.Controls.Add(more, 3, 0);
            var meters = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 22, ColumnCount = 4 };
            meters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34)); meters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); meters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70)); meters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            meters.Controls.Add(new Label { Text = "Mic", Dock = DockStyle.Fill }, 0, 0); meters.Controls.Add(micMeter, 1, 0); meters.Controls.Add(new Label { Text = "Output", Dock = DockStyle.Fill }, 2, 0); meters.Controls.Add(outputMeter, 3, 0);
            sourceMeters = meters; reviewWorkspace = workspace;
            var captureState = new Panel { Dock = DockStyle.Fill }; workspace.Controls.Remove(status); status.Dock = DockStyle.Top; status.Height = 26; captureState.Controls.Add(captureDevices); captureDevices.Padding = new Padding(0, 26, 0, 22); captureState.Controls.Add(status); captureState.Controls.Add(meters); workspace.RowStyles[3].Height = 52; workspace.Controls.Add(captureState, 0, 3); meters.BringToFront();
        }
        protected override bool ProcessCmdKey(ref Message message, Keys keys)
        {
            if (Recording && keys == (Keys.Control | Keys.Alt | Keys.D1)) { AddBookmark("Important"); return true; }
            if (Recording && keys == (Keys.Control | Keys.Alt | Keys.D2)) { AddBookmark("Question"); return true; }
            if (Recording && keys == (Keys.Control | Keys.Alt | Keys.D3)) { AddBookmark("Follow up"); return true; }
            if (keys == (Keys.Control | Keys.F)) { tabs.SelectedIndex = 1; findText.Focus(); return true; }
            if (keys == Keys.F3) { FindNext(); return true; }
            if (keys == (Keys.Control | Keys.Shift | Keys.F)) { librarySearch.Focus(); return true; }
            return base.ProcessCmdKey(ref message, keys);
        }
        void FindNext()
        {
            string query = findText.Text; if (query.Length == 0) return;
            int start = Math.Min(transcript.TextLength, transcript.SelectionStart + Math.Max(1, transcript.SelectionLength));
            int index = transcript.Text.IndexOf(query, start, StringComparison.OrdinalIgnoreCase); if (index < 0) index = transcript.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (index < 0) { uiError = "No matches in this conversation."; return; }
            transcript.Select(index, query.Length); transcript.ScrollToCaret(); transcript.Focus();
        }
        void StartPlayback(int position = -1)
        {
            if (selected == null || audioSource.SelectedItem == null) return;
            try
            {
                if (Busy && ReferenceEquals(selected, session.Document)) { uiError = "Playback is available after this recording is saved."; return; }
                if (!playerOpen) { player.Open(store.AudioPath(selected, Convert.ToString(audioSource.SelectedItem))); playerOpen = true; player.Speed(new[] { 0.75, 1.0, 1.25, 1.5, 2.0 }[speed.SelectedIndex]); }
                player.Play(position); uiError = "";
            }
            catch (Exception ex) { playerOpen = false; uiError = ex.Message; }
        }
        void UpdateReviewState()
        {
            ApplyNotesView();
            emptyState.Visible = selected == null && !writingFresh;
            sourceMeters.Visible = captureDevices.Visible = Recording; reviewWorkspace.RowStyles[3].Height = Recording ? Font.Height * 4 : Busy || !String.IsNullOrWhiteSpace(uiError) || retry.Enabled ? Font.Height + 12 : 0;
            if (Recording) { string micName = microphone.SelectedItem == null ? "Default microphone" : microphone.SelectedItem.ToString(), outputName = output.SelectedItem == null ? "Default output" : output.SelectedItem.ToString(); captureDevices.Text = (session.Document.Mode == "Computer" ? "" : "Mic: " + micName + (session.MicrophoneSound > 70 ? " (activity)" : " (quiet)")) + (session.Document.Mode == "Both" ? " | " : "") + (session.Document.Mode == "Microphone" ? "" : "Output: " + outputName + (session.ComputerSound > 70 ? " (activity)" : " (quiet)")); reviewHints.SetToolTip(captureDevices, captureDevices.Text); }

            if (selected != null && !Busy && audioSource.Items.Count == 0 && new[] { "Microphone", "Computer" }.Any(source => File.Exists(store.AudioPath(selected, source)))) LoadAudioSources(selected);
            if (selected != null) { playPosition.Maximum = Math.Max(1, (int)Math.Min(Int32.MaxValue - 1, selected.DurationSeconds * 1000)); if (playerOpen) playPosition.Value = Math.Min(playPosition.Maximum, player.Position); playTime.Text = audioSource.Items.Count == 0 ? "No saved audio" : TimeSpan.FromMilliseconds(playerOpen ? player.Position : 0).ToString(@"mm\:ss"); }
            if (Busy && session.Document != null && !ReferenceEquals(selected, session.Document)) status.Text = "Active: " + session.Document.Title + " - " + session.Document.Status;
            if (selected != null)
            {
                int failures; lock (selected) failures = selected.Segments.Count(s => !s.Complete || !String.IsNullOrEmpty(s.Error));
                if (!Recording && failures > 0) status.Text += " | " + failures + " missing sections at " + String.Join(", ", selected.Segments.Where(s => !s.Complete || !String.IsNullOrEmpty(s.Error)).Take(3).Select(s => TimeSpan.FromSeconds(s.StartFrame / 16000.0).ToString(@"mm\:ss"))) + " - audio kept; Retry.";
            }
        }
        void LoadAudioSources(NoteDocument document)
        {
            audioSource.Items.Clear(); if (document != null) foreach (string source in new[] { "Microphone", "Computer" }) if (File.Exists(store.AudioPath(document, source))) audioSource.Items.Add(source);
            if (audioSource.Items.Count > 0) audioSource.SelectedIndex = 0;
            bool available = audioSource.Items.Count > 0; audioSource.Enabled = playPosition.Enabled = speed.Enabled = available; if (playButton != null) playButton.Enabled = available; if (pausePlayback != null) pausePlayback.Enabled = available;
            playTime.Text = available ? "00:00" : "No saved audio";
        }
        void DisplayConversation(string value)
        {
            transcript.Text = DialogUi.Lines(value); transcript.SelectAll(); transcript.SelectionColor = ThemeColors.Ink; transcript.SelectionFont = Font;
            foreach (Match match in Regex.Matches(transcript.Text, @"(?m)^\[\d+:\d+:\d+\][^\r\n]*")) { transcript.Select(match.Index, match.Length); transcript.SelectionColor = ThemeColors.Muted; using (var bold = new Font(Font, FontStyle.Bold)) transcript.SelectionFont = bold; }
            transcript.Select(0, 0);
        }
        public void ShowRecordingHelp()
        {
            MessageBox.Show(FindForm(), "Choose the same output device used by Zoom, Meet or your call app. Both captures your microphone and that device. Headphones avoid duplicate sound. If a device changes, stop, choose the new device and resume the saved record.", "Meeting audio");
        }
        void ResumeSelected()
        {
            if (selected == null || Busy || CanStart != null && !CanStart()) return;
            try
            {
                foreach (string source in selected.Mode == "Both" ? new[] { "Microphone", "Computer" } : new[] { selected.Mode }) if (!File.Exists(store.AudioPath(selected, source))) throw new IOException("The original audio is missing. Start a new note instead.");
                SaveEdits(); if (session != null) session.Dispose(); session = new NotesSession(store, config); session.Changed = SessionChanged;
                session.Resume(selected, microphone.SelectedItem == null ? "" : ((AudioEndpoint)microphone.SelectedItem).Id, output.SelectedItem == null ? "" : ((AudioEndpoint)output.SelectedItem).Id); RefreshState();
            }
            catch (Exception ex) { uiError = ex.Message; RefreshState(); }
        }
        internal static void SetSpeakerLabels(NoteDocument note, string[] labels)
        {
            if (labels.Length != note.Segments.Count) throw new InvalidDataException("The recording changed. Reopen speaker labels.");
            if (labels.Any(label => label != null && (label.Length > 80 || label.Any(Char.IsControl)))) throw new InvalidDataException("Use speaker names up to 80 characters, without line breaks.");
            for (int i = 0; i < labels.Length; i++) note.Segments[i].SpeakerLabel = (labels[i] ?? "").Trim();
        }
        void EditSpeakerLabels()
        {
            if (selected == null || Busy && ReferenceEquals(selected, session.Document)) return; var note = selected;
            using (var dialog = DialogUi.Create("Correct speaker labels", new Size(850, 600)))
            {
                string[] labels; lock (note) labels = note.Segments.Select(segment => segment.SpeakerLabel ?? "").ToArray();
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 }; layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.Controls.Add(DialogUi.Text("Select one or more original statements, choose a speaker, then Assign. Only labels change; words and recording sources are preserved."), 0, 0);
                var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = true, HideSelection = false, AccessibleName = "Original statements to label" };
                list.Columns.Add("Time / source", 150); list.Columns.Add("Original words", 430); list.Columns.Add("Speaker", 150);
                Action refresh = () => { list.Items.Clear(); for (int i = 0; i < note.Segments.Count; i++) { var section = note.Segments[i]; if (!section.Complete || String.IsNullOrWhiteSpace(section.RawText ?? section.Text)) continue; var item = new ListViewItem(TimeSpan.FromSeconds((section.TranscriptStartFrame ?? section.StartFrame) / 16000.0).ToString(@"hh\:mm\:ss") + " / " + section.Source) { Tag = i }; item.SubItems.Add((section.RawText ?? section.Text).Replace("\r", "").Replace("\n", " ")); item.SubItems.Add(String.IsNullOrWhiteSpace(labels[i]) ? section.VoiceId ?? "" : labels[i]); list.Items.Add(item); } }; refresh(); layout.Controls.Add(list, 0, 1);
                var assign = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true }; var speaker = new ThemedComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDown, MaxLength = 80, AccessibleName = "Speaker name" }; speaker.Items.AddRange(note.Participants.Concat(new[] { note.UserName }).Concat(labels).Where(name => !String.IsNullOrWhiteSpace(name)).Distinct().Cast<object>().ToArray());
                assign.Controls.Add(new Label { Text = "Speaker", AutoSize = true, Padding = new Padding(0, 8, 0, 0) }); assign.Controls.Add(speaker);
                assign.Controls.Add(DialogUi.Button("Assign to selection", (s, e) => { if (speaker.Text.Length > 80 || speaker.Text.Any(Char.IsControl)) { MessageBox.Show(dialog, "Use a speaker name up to 80 characters without line breaks."); return; } foreach (ListViewItem item in list.SelectedItems) { labels[(int)item.Tag] = speaker.Text.Trim(); item.SubItems[2].Text = speaker.Text.Trim(); } })); layout.Controls.Add(assign, 0, 2);
                assign.Controls.Add(DialogUi.Button("Play selected voice sample", (s, e) => { if (list.SelectedItems.Count == 0) return; var section = note.Segments[(int)list.SelectedItems[0].Tag]; try { player.Open(store.AudioPath(note, section.Source)); player.PlaySample((int)(section.StartFrame / 16), (int)((section.StartFrame + Math.Min(section.Frames, 128000)) / 16)); } catch (Exception ex) { MessageBox.Show(dialog, ex.Message); } }));
                assign.Controls.Add(DialogUi.Button("Select same voice", (s, e) => { if (list.SelectedItems.Count == 0) return; string id = note.Segments[(int)list.SelectedItems[0].Tag].VoiceId; if (String.IsNullOrEmpty(id)) return; foreach (ListViewItem item in list.Items) item.Selected = note.Segments[(int)item.Tag].VoiceId == id; }));
                dialog.FormClosed += (s, e) => player.Pause();
                var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
                actions.Controls.Add(DialogUi.Button("Save labels", (s, e) => { string[] previous = note.Segments.Select(section => section.SpeakerLabel).ToArray(); try { lock (note) { SetSpeakerLabels(note, labels); store.Save(note); } DisplayConversation(NoteStore.Transcript(note)); dialog.Close(); } catch (Exception ex) { lock (note) SetSpeakerLabels(note, previous); MessageBox.Show(dialog, ex.Message, "Could not save labels"); } }, true)); actions.Controls.Add(DialogUi.Button("Cancel", (s, e) => dialog.Close())); layout.Controls.Add(actions, 0, 3);
                dialog.Controls.Add(layout); ThemeColors.Apply(dialog, config.Theme); dialog.ShowDialog(FindForm());
            }
        }
    }
}
