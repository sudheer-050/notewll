using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Notewll
{
    sealed partial class NotesPane : UserControl
    {
        readonly NoteStore store;
        Config config;
        readonly List<NoteDocument> documents;
        readonly TextBox title = Field("Note title"), name = Field("Your name"), written = Field("Modified text");
        readonly RichTextBox transcript = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, DetectUrls = false, BorderStyle = BorderStyle.None, WordWrap = true, AccessibleName = "Original conversation" };
        Panel recordingPanel; TableLayoutPanel newRecordingActions, captureTransport;
        readonly ComboBox mode = Choice(), microphone = Choice(), output = Choice();
        readonly TextBox modifiedText = new ThemedTextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, AccessibleName = "Modified text" };
        readonly AppTabs tabs = new AppTabs { Dock = DockStyle.Fill };
        readonly ListBox history = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, IntegralHeight = false };
        readonly HubButton record = new HubButton { Text = "Start note", Primary = true, Dock = DockStyle.Fill };
        readonly HubButton retry = new HubButton { Text = "Retry transcription", Dock = DockStyle.Fill };
        readonly Label status = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft }, participants = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
        readonly System.Windows.Forms.Timer pulse = new System.Windows.Forms.Timer { Interval = 500 };
        readonly System.Windows.Forms.Timer saveTimer = new System.Windows.Forms.Timer { Interval = 600 };
        readonly ProgressBar micMeter = new ThemedProgressBar { Dock = DockStyle.Fill, Maximum = 100 }, outputMeter = new ThemedProgressBar { Dock = DockStyle.Fill, Maximum = 100 };
        NotesSession session;
        NoteDocument selected;
        bool populating;
        string uiError = "";
        string reportedIssue = "";
        public Action<string> IssueReported;
        readonly Label noteTitle = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 17), AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
        readonly Label noteDetails = new Label { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(115, 116, 118), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        readonly HubButton devicesButton = new HubButton { Text = "Devices", Dock = DockStyle.Fill };
        readonly HubButton editDetails = new HubButton { Text = "Edit details", Dock = DockStyle.Fill };
        public Action ClearHistory;
        public Action<string> SourceChanged;
        string preferredSource;
        public string Source { get { return preferredSource; } }
        public string Feedback { get { return uiError; } }
        public NoteDocument Document { get { return selected; } }
        public NoteStore Store { get { return store; } }
        internal List<NoteDocument> HistorySnapshot { get { return documents.ToList(); } }
        public string SessionStatus { get { return Busy && session.Document != null ? session.Document.Status : selected == null ? "Ready" : selected.Status; } }
        public int SoundLevel { get { return session == null || !Recording ? 0 : Math.Max(session.MicrophoneSound, session.ComputerSound); } }
        public Func<bool> CanStart;
        public bool Busy { get { return session != null && session.Busy; } }
        public bool Recording { get { return session != null && session.Recording; } }
        public string RecordingMode { get { return session == null || session.Document == null ? "" : session.Document.Mode; } }
        public double RecordingSeconds { get { return session == null ? 0 : session.ElapsedSeconds; } }
        static TextBox Field(string accessible) { return new ThemedTextBox { Dock = DockStyle.Fill, AccessibleName = accessible, BorderStyle = BorderStyle.FixedSingle }; }
        static ComboBox Choice() { return new ThemedComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat }; }
        static Label Caption(string text) { return new Label { UseCompatibleTextRendering = true, Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(115, 116, 118) }; }
        static TableLayoutPanel Grid(int columns, int rows)
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = columns, RowCount = rows, Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Color.White };
            return panel;
        }
        public NotesPane(Config config, NoteStore store = null)
        {
            this.config = config; preferredSource = config.NotesSource; this.store = store ?? new NoteStore(); this.store.CleanupDeleted(config.DeletedRetentionDays); documents = this.store.List();
            BackColor = Color.FromArgb(250, 249, 246); Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.None;
            var layout = new Panel { Dock = DockStyle.Fill, BackColor = ThemeColors.Paper }; Controls.Add(layout); editorHost = layout;
            var library = Grid(1, 4); library.BackColor = BackColor; library.Padding = new Padding(0, 0, 12, 0); library.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            library.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); library.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); library.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); library.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            library.Controls.Add(Caption("Search saved notes"), 0, 0);
            var search = Field("Search saved notes"); library.Controls.Add(search, 0, 1); library.Controls.Add(history, 0, 2);
            var fresh = new HubButton { Text = "+ New", Dock = DockStyle.Fill }; library.Controls.Add(fresh, 0, 3); library.Visible = false; Controls.Add(library);
            var sheet = new Surface { Dock = DockStyle.Fill, Padding = new Padding(18), Fill = Color.White }; layout.Controls.Add(sheet); reviewSheet = sheet;
            var workspace = Grid(1, 6); workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); sheet.Controls.Add(workspace);
            var heading = Grid(2, 1); heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 106)); heading.Controls.Add(noteTitle, 0, 0); heading.Controls.Add(editDetails, 1, 0); workspace.Controls.Add(heading, 0, 0); workspace.Controls.Add(noteDetails, 0, 1);
            var transport = Grid(4, 1); captureTransport = transport; transport.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); transport.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); transport.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70)); transport.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            transport.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            transport.Controls.Add(record, 0, 0); var nextCapture = new Panel { Dock = DockStyle.Fill }; nextCapture.Controls.Add(mode); mode.Dock = DockStyle.Fill; transport.Controls.Add(nextCapture, 1, 0); var devices = devicesButton; transport.Controls.Add(devices, 2, 0); transport.Controls.Add(pauseNote, 3, 0); recordingPanel = new Panel { Dock = DockStyle.Fill }; recordingPanel.Controls.Add(transport); newRecordingActions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 }; newRecordingActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); newRecordingActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); var personal = new HubButton { Text = "New voice note", Dock = DockStyle.Fill }; var meetingButton = new HubButton { Text = "Record call", Dock = DockStyle.Fill }; personal.Click += (s, e) => NewVoiceNote(); meetingButton.Click += (s, e) => RecordOnlineMeeting(); newRecordingActions.Controls.Add(personal); newRecordingActions.Controls.Add(meetingButton); recordingPanel.Controls.Add(newRecordingActions); workspace.Controls.Add(recordingPanel, 0, 2);
            workspace.Controls.Add(status, 0, 3);
            var noteTab = new TabPage("My notes"); var transcriptTab = new TabPage("Original text"); tabs.TabPages.Add(noteTab); tabs.TabPages.Add(transcriptTab);
            written.Multiline = true; written.AcceptsReturn = written.AcceptsTab = true; written.ScrollBars = ScrollBars.Vertical; transcript.ScrollBars = RichTextBoxScrollBars.Vertical; transcript.ReadOnly = true;
            written.BorderStyle = transcript.BorderStyle = BorderStyle.None; written.BackColor = transcript.BackColor = Color.White;
            noteTab.Padding = transcriptTab.Padding = new Padding(12); noteTab.Controls.Add(written); transcriptTab.Controls.Add(transcript); workspace.Controls.Add(tabs, 0, 4);
            var actions = Grid(3, 1); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            var copy = new HubButton { Text = "Copy text", Dock = DockStyle.Fill }; var folder = new HubButton { Text = "Export", Dock = DockStyle.Fill };
            actions.Controls.Add(copy, 0, 0); actions.Controls.Add(retry, 1, 0); actions.Controls.Add(folder, 2, 0); workspace.Controls.Add(actions, 0, 5);
            mode.Items.AddRange(new object[] { "My microphone", "Computer audio", "Both sources" }); mode.SelectedIndex = Array.IndexOf(new[] { "Microphone", "Computer", "Both" }, config.NotesSource);
            devices.Click += (sender, args) => ShowDevices(); editDetails.Click += (sender, args) => EditDetails(); folder.Click += (sender, args) => ShowExportDialog();
            search.TextChanged += (sender, args) => { libraryQuery = search.Text; Refilter(); };
            try
            {
                microphone.Items.AddRange(CoreAudio.List(false).Cast<object>().ToArray()); output.Items.AddRange(CoreAudio.List(true).Cast<object>().ToArray());
                microphone.SelectedIndex = 0; output.SelectedIndex = 0;
                SelectEndpoint(microphone, config.NoteMicrophoneId); SelectEndpoint(output, config.NoteOutputId);
                for (int i = 1; i < microphone.Items.Count; i++) if (String.IsNullOrEmpty(config.NoteMicrophoneId) && ((AudioEndpoint)microphone.Items[i]).Name.StartsWith(config.Microphone, StringComparison.OrdinalIgnoreCase)) microphone.SelectedIndex = i;
            }
            catch (Exception ex) { status.Text = "Audio devices: " + ex.Message; }
            history.DisplayMember = "Title"; // NoteDocument properties are used by WinForms binding.
            history.SelectedIndexChanged += (s, e) => { if (!populating) Select(history.SelectedItem as NoteDocument); };
            foreach (var document in documents) history.Items.Add(document);
            record.Click += (s, e) => { if (Recording) Stop(); else NewVoiceNote(); };
            retry.Click += (s, e) => Retry(); fresh.Click += (s, e) => ShowNewNoteMenu(fresh);
            copy.Click += (s, e) => { try { if (selected != null) Clipboard.SetText(tabs.SelectedIndex == 0 ? written.Text : tabs.SelectedTab == modifiedText.Parent ? modifiedText.Text : tabs.SelectedIndex == 2 ? excerptText.Text : NoteStore.Transcript(selected)); } catch (Exception ex) { status.Text = ex.Message; } };
            mode.SelectedIndexChanged += (s, e) => { RefreshState(); if (!populating) { preferredSource = SelectedMode(); if (SourceChanged != null) SourceChanged(preferredSource); } };
            title.TextChanged += Edited; name.TextChanged += Edited; written.TextChanged += Edited;
            saveTimer.Tick += (s, e) => { saveTimer.Stop(); SaveEdits(); };
            pulse.Tick += (s, e) => RefreshState(); pulse.Start();
            BuildReviewTools(library, workspace, noteTab, transcriptTab, search, copy, folder);
            BuildNotesBrowser(search);
            BuildConversationTools();
            var modifiedTab = new TabPage("Modified text") { Padding = new Padding(12) }; modifiedTab.Controls.Add(modifiedText); tabs.TabPages.Add(modifiedTab);
            modifiedTab.Enter += (s, e) => { if (selected != null) modifiedText.Text = DialogUi.Lines(ModifiedVersion(selected, written.Text)); modifiedText.Select(0, 0); };
            Select(null);
        }
        static string ModifiedVersion(NoteDocument document, string edited)
        {
            if (document.Kind == "Dictation" || document.Kind == "Note" && (document.EditableTextInitialized || !String.IsNullOrWhiteSpace(edited))) return edited ?? "";
            lock (document) return String.Join(Environment.NewLine + Environment.NewLine, document.Segments.Where(segment => segment.Complete && String.IsNullOrEmpty(segment.Error)).Select(segment => segment.Text ?? segment.RawText));
        }
        void ShowNewNoteMenu(Control anchor)
        {
            var menu = DialogUi.PopupMenu();
            menu.Items.Add("Write a note", null, (s, e) => NewWrittenNote());
            var note = menu.Items.Add("New voice note", null, (s, e) => NewVoiceNote()); note.Enabled = !Busy;
            var conversation = menu.Items.Add("New conversation", null, (s, e) => NewConversation()); conversation.Enabled = !Busy;
            var meeting = menu.Items.Add("Record online meeting", null, (s, e) => RecordOnlineMeeting()); meeting.Enabled = !Busy;
            menu.Show(anchor, new Point(0, anchor.Height));
        }
        string SelectedMode() { return mode.SelectedIndex == 1 ? "Computer" : mode.SelectedIndex == 2 ? "Both" : "Microphone"; }
        void Edited(object sender, EventArgs e) { if (!populating) { if (sender == written && selected != null && selected.Kind == "Note") { selected.EditableTextInitialized = true; selected.VoiceDraft = false; } if (personalSaveState != null) personalSaveState.Text = "Saving..."; saveTimer.Stop(); saveTimer.Start(); } }
        void SaveEdits()
        {
            if (populating) return;
            try
            {
            if (selected == null)
            {
                if (String.IsNullOrWhiteSpace(written.Text) && String.IsNullOrWhiteSpace(title.Text)) return;
                selected = store.Create(title.Text, SelectedMode(), name.Text); selected.Status = "Saved"; selected.Kind = "Note";
                documents.Insert(0, selected); populating = true; history.Items.Insert(0, selected); history.SelectedItem = selected; populating = false;
            }
                lock (selected)
                {
                    selected.WrittenNotes = written.Text; NoteMetadata.UpdateTitle(selected);
                    store.Save(selected); store.SavedName = selected.UserName;
                }
                RefreshPersonalSaveState();
                int index = history.Items.IndexOf(selected); if (index >= 0) { populating = true; history.Items[index] = selected; populating = false; }
            }
            catch (Exception ex) { uiError = "Could not save note: " + ex.Message; status.Text = uiError; if (personalSaveState != null) personalSaveState.Text = uiError; }
        }
        void Select(NoteDocument document)
        {
            if (populating) return;
            SaveEdits(); player.Dispose(); playerOpen = false; uiError = ""; reviewingConversation = false; selected = document; if (document != null) writingFresh = false; populating = true;
            title.Text = document == null ? "" : document.Title; name.Text = document == null ? store.SavedName : document.UserName;
            if (document != null && document.VoiceDraft)
            {
                document.WrittenNotes = String.Join(Environment.NewLine + Environment.NewLine, document.Segments.Where(segment => segment.Complete && String.IsNullOrEmpty(segment.Error) && !String.IsNullOrWhiteSpace(segment.RawText ?? segment.Text)).OrderBy(segment => segment.StartFrame).Select(segment => segment.Text ?? segment.RawText));
            }
            else if (document != null && ActivityHistory.Kind(document) == "Note" && !document.EditableTextInitialized && !(Busy && ReferenceEquals(document, session.Document)))
            {
                if (String.IsNullOrWhiteSpace(document.WrittenNotes)) document.WrittenNotes = String.Join(Environment.NewLine + Environment.NewLine, document.Segments.Where(segment => segment.Complete && String.IsNullOrEmpty(segment.Error)).Select(segment => segment.Text ?? segment.RawText));
                document.EditableTextInitialized = true;
            }
            modifiedText.Text = document == null ? "" : DialogUi.Lines(ModifiedVersion(document, document.WrittenNotes));
            written.Text = document == null ? "" : document.WrittenNotes; DisplayConversation(document == null ? "" : NoteStore.Transcript(document)); LoadAudioSources(document);
            if (personalTitle != null) personalTitle.Text = document == null ? "" : document.Title;
            history.SelectedItem = document; excerptText.Text = document == null ? "" : document.Summary; populating = false; RefreshState();
        }
        public void UpdateConfig(Config value) { config = value; preferredSource = value.NotesSource; if (!Busy) { populating = true; mode.SelectedIndex = Array.IndexOf(new[] { "Microphone", "Computer", "Both" }, value.NotesSource); populating = false; } }
        public void NewVoiceNote() { Toggle("Microphone", "Note"); }
        public void NewConversation() { Toggle("Microphone", "Conversation"); }
        public void RecordOnlineMeeting() { Toggle(config.MeetingSource == "Computer" ? "Computer" : "Both", "Meeting"); }
        public void RecordPlannedMeeting(string title)
        {
            if (Busy) return; RecordOnlineMeeting();
            if (Recording && selected != null) { selected.Title = title; selected.AutoTitle = false; store.Save(selected); }
        }
        public void Toggle(string source) { Toggle(source, "Note"); }
        public void Toggle(string source, string kind)
        {
            if (!Recording && config.SpeechLanguage != "en" && !SpeechLanguages.Ready(config)) { status.Text = "Install multilingual speech in Storage > Optional features, or choose English."; return; }
            if (Recording) { if (session.Document.Kind == kind) Stop(); else status.Text = "Stop the current recording before starting a " + kind.ToLowerInvariant() + "."; return; }
            if (Busy) { status.Text = "Finishing the last transcription. Audio is already saved."; return; }
            populating = true; mode.SelectedIndex = source == "Computer" ? 1 : source == "Both" ? 2 : 0; populating = false; Start(source, kind);
        }
        void Start(string source, string kind)
        {
            if (Busy || (CanStart != null && !CanStart())) { status.Text = "Finish the current dictation first."; return; }
            browsingNotes = false; ApplyNotesView(); SaveEdits(); uiError = "";
            if (microphone.SelectedItem == null && source != "Computer" || output.SelectedItem == null && source != "Microphone") { uiError = "No selected audio device is available."; status.Text = uiError; return; }
            try
            {
                if (session != null) session.Dispose(); session = new NotesSession(store, config); session.Changed = SessionChanged;
                session.Start("", source, store.SavedName, microphone.SelectedItem == null ? "" : ((AudioEndpoint)microphone.SelectedItem).Id, output.SelectedItem == null ? "" : ((AudioEndpoint)output.SelectedItem).Id, kind);
                store.Save(session.Document); AddSessionDocument(); tabs.SelectedIndex = kind == "Note" ? 0 : 1; RefreshState();
            }
            catch (Exception ex) { AddSessionDocument(); tabs.SelectedIndex = kind == "Note" ? 0 : 1; uiError = ex.Message; RefreshState(); }
        }
        void AddSessionDocument()
        {
            if (session == null || session.Document == null) return;
            selected = session.Document;
            if (!documents.Contains(selected)) { documents.Insert(0, selected); populating = true; history.Items.Insert(0, selected); history.SelectedItem = selected; written.Clear(); title.Text = selected.Title; populating = false; }
        }
        void Stop() { if (session != null) { SaveEdits(); session.Stop(); RefreshState(); } }
        void Retry()
        {
            if (selected == null || Busy || (CanStart != null && !CanStart())) return;
            try { SaveEdits(); if (session != null) session.Dispose(); session = new NotesSession(store, config); session.Changed = SessionChanged; session.Retry(selected); RefreshState(); }
            catch (Exception ex) { status.Text = ex.Message; }
        }
        void SessionChanged()
        {
            try { if (IsHandleCreated && !IsDisposed) BeginInvoke((Action)(() =>
            {
                if (IsDisposed || selected == null) return;
                if (session != null && !ReferenceEquals(selected, session.Document)) { RefreshState(); return; }
                if (!saveTimer.Enabled) { populating = true; name.Text = selected.UserName; title.Text = selected.Title; populating = false; }
                int selectedIndex = history.Items.IndexOf(selected); if (selectedIndex >= 0) { populating = true; history.Items[selectedIndex] = selected; populating = false; }
                string value = NoteStore.Transcript(selected);
                if (transcript.Text.Replace("\r", "") != value.Replace("\r", "")) { DisplayConversation(value); transcript.SelectionStart = transcript.TextLength; transcript.ScrollToCaret(); }
                if (selected.Kind == "Note" && selected.VoiceDraft)
                {
                    string dictated = String.Join(Environment.NewLine + Environment.NewLine, selected.Segments.Where(part => part.Complete && String.IsNullOrEmpty(part.Error) && !String.IsNullOrWhiteSpace(part.RawText ?? part.Text)).OrderBy(part => part.StartFrame).Select(part => part.Text ?? part.RawText));
                    if (written.Text != dictated) { populating = true; written.Text = dictated; populating = false; SaveEdits(); }
                }
                RefreshState();
            })); }
            catch (InvalidOperationException) { }
        }
        void RefreshState()
        {
            bool busy = Busy; bool recording = Recording;
            written.ReadOnly = busy && selected != null && selected.Kind == "Note" && session != null && ReferenceEquals(selected, session.Document);
            if (selected != null && tabs.SelectedTab == modifiedText.Parent) { string value = DialogUi.Lines(ModifiedVersion(selected, written.Text)); if (modifiedText.Text != value) modifiedText.Text = value; }
            bool configuring = selected == null || recording && ReferenceEquals(selected, session.Document); newRecordingActions.Visible = !configuring; newRecordingActions.Enabled = !busy; captureTransport.Visible = configuring; if (configuring) captureTransport.BringToFront(); else newRecordingActions.BringToFront();
            record.Text = recording ? "Stop & save" : "New voice note"; record.Enabled = recording || !busy;
            mode.Enabled = microphone.Enabled = output.Enabled = !busy; history.Enabled = true;
            if (!busy) { microphone.Enabled = mode.SelectedIndex != 1; output.Enabled = mode.SelectedIndex != 0; }
            pauseNote.Enabled = recording; pauseNote.Text = session != null && session.Paused ? "Resume" : "Pause";
            devicesButton.Enabled = !busy; editDetails.Visible = selected != null; editDetails.Enabled = selected != null && !Recording;
            retry.Enabled = !busy && selected != null && selected.Segments.Any(s => !s.Complete || !String.IsNullOrEmpty(s.Error));
            if (selected != null)
            {
                int pending; lock (selected) pending = selected.Segments.Count(s => !s.Complete);
                status.Text = recording ? (session.Paused ? "Paused " : "Recording ") + TimeSpan.FromSeconds(RecordingSeconds).ToString(@"hh\:mm\:ss") + " - " + pending + " sections awaiting text" : busy ? "Audio saved. Finishing transcription..." : selected.Status;
                participants.Text = selected.Participants.Count == 0 ? "Introduce yourself: “I am Sudheer.” You can edit your name above." : "Introductions: " + String.Join(", ", selected.Participants);
            }
            else { status.Text = "Ready. Notes and audio save locally."; participants.Text = "Introduce yourself: “I am Sudheer.” You can edit your name above."; }
            noteTitle.Text = selected == null ? "Ready when you are." : selected.Title;
            noteDetails.Text = selected == null ? "Titles and names fill in automatically. Edit details later." :
                (selected.Kind ?? "Note") + " \u00b7 " + DateTime.Parse(selected.CreatedUtc).ToLocalTime().ToString("MMM d - h:mm tt") + " | " + (String.IsNullOrWhiteSpace(selected.UserName) ? "Name from your introduction" : selected.UserName) + (selected.Participants.Count > 0 ? " | " + String.Join(", ", selected.Participants) : "");
            if (selected != null && selected.Segments.Count > 0) noteDetails.Text += " | " + selected.Mode + " audio";
            recordingPanel.Visible = busy;
            using (var graphics = CreateGraphics()) reviewWorkspace.RowStyles[2].Height = busy ? 44 * graphics.DpiX / 96f : 0;
            UpdateReviewState();
            if (!String.IsNullOrWhiteSpace(uiError)) status.Text = uiError;
            string issue = !String.IsNullOrWhiteSpace(uiError) ? uiError : selected == null ? "" : selected.CaptureError;
            if (!String.IsNullOrWhiteSpace(issue) && issue != reportedIssue) { reportedIssue = issue; if (IssueReported != null) IssueReported(issue); }
            micMeter.Value = session == null || !recording ? 0 : Math.Min(100, session.MicrophoneLevel * 100 / 12000);
            outputMeter.Value = session == null || !recording ? 0 : Math.Min(100, session.ComputerLevel * 100 / 12000);
        }
        void EditDetails()
        {
            if (selected == null || Busy && ReferenceEquals(selected, session.Document)) return; SaveEdits();
            using (var dialog = new Form { Text = "Edit note details", Icon = AppLogo.Icon, ClientSize = new Size(470, 390), Font = Font, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, AutoScaleMode = AutoScaleMode.None })
            {
                var grid = Grid(2, 6); grid.Padding = new Padding(22); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                for (int i = 0; i < 6; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 20)); dialog.Controls.Add(grid);
                TextBox editedTitle = Field("Title"), editedName = Field("Your name"), editedPeople = Field("People"); editedTitle.Text = selected.Title; editedName.Text = selected.UserName; editedPeople.Text = String.Join(", ", selected.Participants);
                grid.Controls.Add(Caption("Title"), 0, 0); grid.Controls.Add(editedTitle, 1, 0); grid.Controls.Add(Caption("Your name"), 0, 1); grid.Controls.Add(editedName, 1, 1); grid.Controls.Add(Caption("People"), 0, 2); grid.Controls.Add(editedPeople, 1, 2);
                var editedTags = Field("Tags"); editedTags.Text = selected.Tags; grid.Controls.Add(Caption("Tags"), 0, 3); grid.Controls.Add(editedTags, 1, 3);
                var apply = new HubButton { Text = "Save details", Dock = DockStyle.Fill, Primary = true }; grid.Controls.Add(apply, 1, 5);
                var cancel = new HubButton { Text = "Cancel", Dock = DockStyle.Fill }; cancel.Click += (s, e) => dialog.Close(); grid.Controls.Add(cancel, 0, 5); dialog.CancelButton = cancel;
                var project = Field("Project"); project.Text = selected.Project; grid.Controls.Add(Caption("Project"), 0, 4); grid.Controls.Add(project, 1, 4);
                apply.Click += (sender, args) =>
                {
                    try { lock (selected) { if (editedTitle.Text.Trim() != selected.Title && !String.IsNullOrWhiteSpace(editedTitle.Text)) { selected.Title = editedTitle.Text.Trim(); selected.AutoTitle = false; } if (editedName.Text.Trim() != selected.UserName) { selected.UserName = editedName.Text.Trim(); selected.NameEdited = true; store.SavedName = selected.UserName; } selected.Project = project.Text.Trim(); selected.Tags = editedTags.Text.Trim(); selected.Participants = editedPeople.Text.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0).Distinct().ToList(); store.Save(selected); } title.Text = selected.Title; name.Text = selected.UserName; RefreshState(); dialog.Close(); }
                    catch (Exception ex) { MessageBox.Show(dialog, ex.Message, "Could not save details"); }
                };
                ThemeColors.Apply(dialog, config.Theme);
                using (var graphics = dialog.CreateGraphics()) { float scale = graphics.DpiX / 96f; if (scale != 1) dialog.Scale(new SizeF(scale, scale)); }
                dialog.ShowDialog(FindForm());
            }
        }
        void ShowDevices()
        {
            if (Busy) return;
            using (var dialog = DialogUi.Create("Recording devices", new Size(620, 370)))
            {
                var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                for (int i = 0; i < 6; i++) grid.RowStyles.Add(new RowStyle(i == 4 ? SizeType.Percent : SizeType.AutoSize, i == 4 ? 100 : 0));
                var selectedMic = Choice(); var selectedOutput = Choice();
                try { selectedMic.Items.AddRange(CoreAudio.List(false).Cast<object>().ToArray()); selectedOutput.Items.AddRange(CoreAudio.List(true).Cast<object>().ToArray()); selectedMic.SelectedIndex = selectedOutput.SelectedIndex = 0; SelectEndpoint(selectedMic, config.NoteMicrophoneId); SelectEndpoint(selectedOutput, config.NoteOutputId); }
                catch (Exception ex) { uiError = "Devices unavailable: " + ex.Message; return; }
                grid.Controls.Add(DialogUi.Text("Your microphone"), 0, 0); grid.Controls.Add(selectedMic, 0, 1);
                grid.Controls.Add(DialogUi.Text("Computer audio - choose the output used by your call"), 0, 2); grid.Controls.Add(selectedOutput, 0, 3);
                grid.Controls.Add(DialogUi.Text("These devices apply to new and resumed recordings. Cancel or close this window to keep your current choices."), 0, 4);
                var footer = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
                footer.Controls.Add(DialogUi.Button("Apply devices", (sender, args) =>
                {
                    try { string micId = ((AudioEndpoint)selectedMic.SelectedItem).Id, outputId = ((AudioEndpoint)selectedOutput.SelectedItem).Id;
                        var draft = config.Copy(); draft.NoteMicrophoneId = micId; draft.NoteOutputId = outputId; draft.Save();
                        config.NoteMicrophoneId = micId; config.NoteOutputId = outputId;
                        microphone.Items.Clear(); output.Items.Clear(); microphone.Items.AddRange(selectedMic.Items.Cast<object>().ToArray()); output.Items.AddRange(selectedOutput.Items.Cast<object>().ToArray()); SelectEndpoint(microphone, micId); SelectEndpoint(output, outputId); dialog.DialogResult = DialogResult.OK; dialog.Close();
                    } catch (Exception ex) { MessageBox.Show(dialog, ex.Message, "Could not save devices"); }
                }, true));
                var cancel = DialogUi.Button("Cancel", (sender, args) => dialog.Close()); cancel.DialogResult = DialogResult.Cancel; dialog.CancelButton = cancel; footer.Controls.Add(cancel); grid.Controls.Add(footer, 0, 5);
                dialog.Controls.Add(grid); ThemeColors.Apply(dialog, config.Theme); dialog.ShowDialog(FindForm());
            }
        }
        void ShowFilesMenu(Control anchor)
        {
            var menu = DialogUi.PopupMenu();
            var retryItem = menu.Items.Add("Retry missing transcription", null, (s, e) => Retry()); retryItem.Enabled = retry.Enabled;
            var speakers = menu.Items.Add("Correct speaker labels..."); speakers.Enabled = selected != null && !(Busy && ReferenceEquals(selected, session.Document)); speakers.Click += (s, e) => EditSpeakerLabels();
            var resume = menu.Items.Add("Resume this recording"); resume.Enabled = selected != null && selected.Segments.Count > 0 && !Busy; resume.Click += (s, e) => ResumeSelected();
            var storageItem = menu.Items.Add("Recently deleted & storage", null, (s, e) => { if (!Busy) { NotesStorageDialog.Show(FindForm(), store, config); ReloadLibrary(); } });
            var open = menu.Items.Add("Open conversation (read-only)"); open.Enabled = selected != null;
            open.Click += (s, e) =>
            {
                if (selected == null) return;
                using (var view = new Form { Text = selected.Title + " - Read-only", Icon = AppLogo.Icon, Size = new Size(740, 620), StartPosition = FormStartPosition.CenterParent })
                {
                    view.Controls.Add(new ThemedTextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = Font, Text = NoteStore.Transcript(selected), BackColor = Color.White });
                    view.ShowDialog(FindForm());
                }
            };
            var audio = new ToolStripMenuItem("Open recording"); audio.Enabled = selected != null && !Busy;
            foreach (string source in new[] { "Microphone", "Computer" })
            {
                string path = selected == null ? "" : store.AudioPath(selected, source);
                var item = audio.DropDownItems.Add(source); item.Enabled = System.IO.File.Exists(path);
                item.Click += (s, e) => { try { Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); } catch (Exception ex) { uiError = ex.Message; RefreshState(); } };
            }
            menu.Items.Add(audio);
            var folderItem = menu.Items.Add("Show files", null, (s, e) => OpenFolder());
            var export = menu.Items.Add("Export..."); export.Enabled = selected != null && !(Busy && ReferenceEquals(selected, session.Document)); export.Click += (s, e) => ShowExportDialog(); menu.Items.Add(new ToolStripSeparator());
            var delete = menu.Items.Add("Delete note and recordings"); delete.Enabled = selected != null && !Busy;
            delete.Click += (s, e) => DeleteSelected();
            foreach (var item in new ToolStripItem[] { speakers, resume, storageItem, open, audio, folderItem, export, delete }) menu.Items.Remove(item);
            foreach (var separator in menu.Items.OfType<ToolStripSeparator>().ToArray()) { menu.Items.Remove(separator); separator.Dispose(); }
            var review = new ToolStripMenuItem("Review"); review.DropDownItems.Add(open); review.DropDownItems.Add(speakers); menu.Items.Remove(retryItem); review.DropDownItems.Add(retryItem);
            var recordings = new ToolStripMenuItem("Recording"); recordings.DropDownItems.Add(resume); recordings.DropDownItems.Add(audio);
            var chooseDevices = recordings.DropDownItems.Add("Choose recording devices...", null, (s, e) => ShowDevices()); chooseDevices.Enabled = !Busy;
            recordings.DropDownItems.Add("Check meeting audio", null, (s, e) => ShowRecordingHelp());
            var manage = new ToolStripMenuItem("Manage"); manage.DropDownItems.Add(folderItem); manage.DropDownItems.Add(storageItem); manage.DropDownItems.Add(new ToolStripSeparator()); manage.DropDownItems.Add(delete);
            var clearHistory = manage.DropDownItems.Add("Clear history...", null, (s, e) => { if (ClearHistory != null) ClearHistory(); }); clearHistory.Enabled = !Busy && ClearHistory != null;
            menu.Items.Add(review); menu.Items.Add(recordings); menu.Items.Add(export); menu.Items.Add(manage);
            menu.Show(anchor, new Point(0, anchor.Height));
        }
        void DeleteSelected()
        {
            if (selected == null || Busy) return;
            if (MessageBox.Show(FindForm(), "Delete this note and its recordings? They will move to the local .deleted folder so they can be recovered.", "Delete note", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                saveTimer.Stop(); var removed = selected;
                if (session != null) { session.Dispose(); session = null; }
                store.Delete(removed); populating = true; selected = null; documents.Remove(removed); history.Items.Remove(removed);
                written.Clear(); transcript.Clear(); title.Clear(); populating = false;
                Select(documents.FirstOrDefault());
            }
            catch (Exception ex) { populating = false; uiError = "Could not delete note: " + ex.Message; RefreshState(); }
        }
        void OpenFolder()
        {
            try { System.IO.Directory.CreateDirectory(store.Root); Process.Start(new ProcessStartInfo { FileName = selected == null ? store.Root : store.Folder(selected), UseShellExecute = true }); }
            catch (Exception ex) { status.Text = ex.Message; }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { saveTimer.Stop(); SaveEdits(); pulse.Stop(); pulse.Dispose(); saveTimer.Dispose(); if (session != null) session.Dispose(); player.Dispose(); reviewHints.Dispose(); microphone.Dispose(); output.Dispose(); title.Dispose(); name.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
