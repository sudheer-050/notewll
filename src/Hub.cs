using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace LocalDictation
{
    // Compose the scrolling viewport and its children in one Windows paint pass.
    // Local double buffering alone does not cover native scrolling of child windows.
    sealed class BufferedScrollPanel : Panel
    {
        public BufferedScrollPanel() { DoubleBuffered = true; ResizeRedraw = true; }
        protected override CreateParams CreateParams
        {
            get { var parameters = base.CreateParams; parameters.ExStyle |= 0x02000000; return parameters; } // WS_EX_COMPOSITED
        }
        protected override void WndProc(ref Message message)
        {
            int kind = message.Msg;
            base.WndProc(ref message);
            if (kind == 0x0114 || kind == 0x0115 || kind == 0x020A || kind == 0x020E)
                RepaintViewport(); // scrollbar and wheel messages, after Windows has moved the children
        }
        protected override void OnScroll(ScrollEventArgs e) { base.OnScroll(e); RepaintViewport(); }
        void RepaintViewport() { Invalidate(true); Update(); }
    }

    sealed class Surface : Panel
    {
        public Color Fill = ThemeColors.Card;
        public static Color CanvasColor(Control parent)
        {
            while (parent != null) { var surface = parent as Surface; if (surface != null) return surface.Fill; if (parent.BackColor.A == 255) return parent.BackColor; parent = parent.Parent; }
            return ThemeColors.Paper;
        }
        public Surface() { DoubleBuffered = true; ResizeRedraw = true; SetStyle(ControlStyles.Opaque, true); BackColor = ThemeColors.Paper; }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.Clear(Surface.CanvasColor(Parent)); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Shape(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 18))
            using (var brush = new SolidBrush(Fill))
            using (var pen = new Pen(ThemeColors.Border))
            { e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(pen, path); }
        }
        public static GraphicsPath Shape(RectangleF r, float radius)
        {
            var p = new GraphicsPath(); float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
        }
    }

    sealed class HubButton : Button
    {
        public bool Primary, Selected;
        public HubButton()
        {
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent; Cursor = Cursors.Hand; Font = new Font("Segoe UI", 10);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Surface.CanvasColor(Parent)); }
        protected override void OnPaint(PaintEventArgs e)
        {
            OnPaintBackground(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = SystemInformation.HighContrast ? (Primary || Selected ? SystemColors.Highlight : SystemColors.Control) : Primary ? ThemeColors.Accent : Selected ? ThemeColors.Selected : ThemeColors.Button;
            if (!SystemInformation.HighContrast && ClientRectangle.Contains(PointToClient(Cursor.Position))) fill = Primary ? ThemeColors.Accent : ThemeColors.Hover;
            using (var path = Surface.Shape(new RectangleF(0, 0, Width - 1, Height - 1), 9))
            using (var brush = new SolidBrush(fill)) e.Graphics.FillPath(brush, path);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle,
                SystemInformation.HighContrast ? (Primary || Selected ? SystemColors.HighlightText : SystemColors.ControlText) : !Enabled ? ThemeColors.Muted : Primary ? ThemeColors.AccentInk : ThemeColors.Ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4));
        }
    }

    // The desktop hub shares the existing config, recorder, cleanup and local STT client.
    // Closing it only hides it when hosted by DictationApp; transcripts remain memory-only.
    sealed partial class SettingsForm : Form
    {
        static Color Ink { get { return ThemeColors.Ink; } }
        static Color Muted { get { return ThemeColors.Muted; } }
        static Color Paper { get { return ThemeColors.Paper; } }
        Config source;
        readonly Panel host = new Panel { Dock = DockStyle.Fill, BackColor = Paper };
        readonly Panel sidebar = new Panel { Name = "AppSidebar", Dock = DockStyle.Left, Width = 194, BackColor = ThemeColors.Sidebar };
        readonly Panel[] pages = new Panel[8];
        readonly HubButton[] navigation = new HubButton[8];
        readonly HubButton hotkey = new HubButton();
        readonly HubButton savePinned = new HubButton { Text = "Save preferences", Primary = true, Left = 18, Width = 158, Height = 38, Visible = false };
        readonly Label sidebarStatus = new Label { Left = 20, Width = 154, Height = 44, Visible = false, Font = new Font("Segoe UI", 8), ForeColor = Color.FromArgb(61, 115, 90), AutoEllipsis = true };
        readonly System.Collections.Generic.HashSet<int> capturedKeys = new System.Collections.Generic.HashSet<int>();
        string selectedShortcut;
        readonly ComboBox noteShortcut = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
        readonly ComboBox noteSource = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
        readonly ComboBox meetingSource = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
        HubButton captureTarget;
        NotesPane notes;
        public void ToggleNote(string mode) { if (notes != null) notes.Toggle(mode); }
        public void ToggleNote(string mode, string kind) { if (notes != null) notes.Toggle(mode, kind); }
        public bool NoteBusy { get { return notes != null && notes.Busy; } }
        public NotesPane Notes { get { return notes; } }
        public string PreferredNoteSource { get { return notes == null ? source.NotesSource : notes.Source; } }
        public bool IsNoteRecording { get { return notes != null && notes.Recording; } }
        public int NoteSoundLevel { get { return notes == null ? 0 : notes.SoundLevel; } }
        public string NoteMode { get { return notes == null ? "" : notes.RecordingMode; } }
        public bool IsCapturingShortcut { get; private set; }
        public void BeginShortcutCapture() { BeginShortcutCapture(hotkey); }
        void BeginShortcutCapture(HubButton button)
        {
            if (IsTesting || (CanTest != null && !CanTest())) return;
            if (IsCapturingShortcut) EndShortcutCapture();
            captureTarget = button; capturedKeys.Clear(); IsCapturingShortcut = true; captureTarget.Text = "Press your shortcut...";
            captureTarget.Primary = true; captureTarget.Focus(); captureTarget.Invalidate(); saved.Text = "Press keys together, then release. Escape cancels.";
        }
        public void CaptureShortcutKey(int key, bool down)
        {
            if (!IsCapturingShortcut) return;
            if (key == (int)Keys.Escape) { EndShortcutCapture(); return; }
            if (down) { capturedKeys.Add(Shortcut.Normalize(key)); captureTarget.Text = Shortcut.Format(capturedKeys); captureTarget.Invalidate(); }
            else if (capturedKeys.Count > 0)
            {
                try { string value = Shortcut.Format(Shortcut.Parse(Shortcut.Format(capturedKeys))); selectedShortcut = value; MarkDirty(); EndShortcutCapture(); saved.Text = "Shortcut captured. Save preferences to apply it."; }
                catch (Exception ex) { saved.Text = ex.Message; }
            }
        }
        void EndShortcutCapture() { IsCapturingShortcut = false; if (captureTarget == null) return; captureTarget.Text = selectedShortcut; captureTarget.Primary = false; captureTarget.Invalidate(); }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (IsCapturingShortcut) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }
        readonly ComboBox mic = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
        readonly ProgressBar meter = new ProgressBar { Maximum = 100, Style = ProgressBarStyle.Continuous };
        readonly Label micStatus = new Label(), sttStatus = new Label(), saved = new Label(), homeLatest = new Label(), homeShortcut = new Label(), homeMicrophone = new Label();
        readonly CheckBox quality = new CheckBox { Text = "Enhanced local notes and meeting voices", AutoSize = true };
        readonly CheckBox accurateMode = new CheckBox { Text = "Accurate local dictation and speech detection", AutoSize = true };
        readonly CheckBox cleanup = new CheckBox { Text = "Clean up filler words and spoken corrections", AutoSize = true };
        readonly CheckBox autoPaste = new CheckBox { Text = "Insert text into the app I was using", AutoSize = true };
        readonly CheckBox liveWriting = new CheckBox { Text = "Write live, then refine when I release the shortcut", AutoSize = true };
        readonly ComboBox pasteMethod = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
        readonly CheckBox startup = new CheckBox { Text = "Keep the toolbar ready when I sign in", AutoSize = true };
        readonly TextBox raw = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None };
        readonly TextBox cleaned = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None };
        readonly HubButton testMic = new HubButton { Text = "Test microphone" }, transcribe = new HubButton { Text = "Record a sentence", Primary = true }, testStt = new HubButton { Text = "Check connection" }, copy = new HubButton { Text = "Copy text" };
        readonly System.Windows.Forms.Timer poll = new System.Windows.Forms.Timer { Interval = 60 };
        MicRecorder testRecorder;
        DateTime testEnds;
        bool testTranscribes, checking, transcribing;
        float uiScale = 1;
        Label localFooter, privacyFooter, rawHeading, cleanedHeading;
        public bool CloseToTray;
        public Action<Config> OnSaved;
        public Func<bool> CanTest;
        public bool IsTesting { get { return VoiceEnrollmentDialog.OpenCount > 0 || VoiceWordDialog.OpenCount > 0 || testRecorder != null || transcribing || (notes != null && notes.Busy); } }
        public Config Result { get; private set; }
        public string CurrentPage { get; private set; }
        public string Summary { get { return "mics=" + String.Join(";", mic.Items.Cast<object>().Select(i => i.ToString())) + "|selected=" + mic.SelectedItem + "|cleanup=" + cleanup.Checked + "|autoPaste=" + autoPaste.Checked + "|startup=" + startup.Checked + "|preview=" + cleaned.Text; } }

        public SettingsForm(Config config, string lastRaw, string lastCleaned, NoteStore noteStore = null)
        {
            SuspendLayout();
            source = config; ThemeColors.Mode = config.Theme; Icon = AppLogo.Icon; Text = "Notewll"; BackColor = Paper; ForeColor = Ink;
            Font = new Font("Segoe UI", 10); ClientSize = new Size(1080, 740); MinimumSize = new Size(960, 690);
            AutoScaleMode = AutoScaleMode.None; StartPosition = FormStartPosition.CenterScreen;
            Controls.Add(host); Controls.Add(sidebar);
            sidebar.Paint += (s, e) => { using (var pen = new Pen(ThemeColors.Border)) e.Graphics.DrawLine(pen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height); };
            Brand(sidebar); sidebar.Controls.Add(savePinned); sidebar.Controls.Add(sidebarStatus); savePinned.Click += (s, e) => SavePreferences();
            string[] names = { "Home", "Playground", "Preferences", "Help", "Notes", "Dictionary", "Snippets", "Style" };
            for (int i = 0; i < names.Length; i++)
            {
                int at = i;
                navigation[i] = new HubButton { Text = names[i] == "Playground" ? "Practice" : names[i], Left = 18, Top = 128 + new[] { 0, 0, 5, 6, 1, 2, 3, 4 }[i] * 40, Width = 158, Height = 34, AccessibleName = names[i] };
                navigation[i].Click += (s, e) => ShowPage(names[at]); navigation[i].Visible = i != 1; sidebar.Controls.Add(navigation[i]);
                pages[i] = new BufferedScrollPanel { Dock = DockStyle.Fill, BackColor = Paper, AutoScroll = true, Visible = false, Name = names[i] }; host.Controls.Add(pages[i]);
            }
            localFooter = Label(sidebar, "●  On your device", 20, 650, 160, 23, 9, false, Color.FromArgb(61, 115, 90));
            privacyFooter = Label(sidebar, "Private by design\nNotes saved locally", 20, 678, 165, 42, 9, false, Muted);
            sidebar.Resize += (s, e) => LayoutFooter();
            selectedShortcut = config.Hotkey; hotkey.Text = selectedShortcut;
            hotkey.Click += (s, e) => BeginShortcutCapture();
            Deactivate += (s, e) => { if (IsCapturingShortcut) EndShortcutCapture(); };
            mic.Items.Add("Windows default microphone"); foreach (var name in Mics.List()) mic.Items.Add(name);
            int selected = mic.Items.IndexOf(config.Microphone); mic.SelectedIndex = selected > 0 ? selected : 0;
            liveWriting.Checked = config.LiveWriting; pasteMethod.Items.AddRange(new object[] { "Automatic (includes terminals)", "Ctrl+V", "Ctrl+Shift+V", "Shift+Insert" }); pasteMethod.SelectedIndex = Array.IndexOf(new[] { "Auto", "CtrlV", "CtrlShiftV", "ShiftInsert" }, config.PasteMethod);
            quality.Checked = config.QualityEnabled; accurateMode.Checked = config.AccurateDictationEnabled; cleanup.Checked = config.CleanupEnabled; autoPaste.Checked = config.AutoPaste; startup.Checked = config.StartWithWindows;
            noteShortcut.Items.AddRange(new object[] { "Ctrl+Shift+N", "Ctrl+Shift+Space", "Ctrl+Alt+N", "F9", "F10", "F11", "F12" });
            if (!noteShortcut.Items.Contains(config.NotesHotkey)) noteShortcut.Items.Add(config.NotesHotkey); noteShortcut.SelectedItem = config.NotesHotkey;
            noteSource.Items.AddRange(new object[] { "My microphone", "Computer audio", "Microphone + computer" }); noteSource.SelectedIndex = Array.IndexOf(new[] { "Microphone", "Computer", "Both" }, config.NotesSource);
            meetingSource.Items.AddRange(new object[] { "My microphone", "Computer audio", "Microphone + computer" }); meetingSource.SelectedIndex = Array.IndexOf(new[] { "Microphone", "Computer", "Both" }, config.MeetingSource);
            BuildHome(); BuildPlayground(); BuildPreferences(); BuildHelp(); BuildLibraryPage(5, "Word"); BuildLibraryPage(6, "Snippet"); BuildStylePage();
            notes = new NotesPane(config, noteStore) { Left = 40, Top = 18, Width = 760, Height = 700 };
            notes.CanStart = () => VoiceEnrollmentDialog.OpenCount == 0 && testRecorder == null && !transcribing && !IsCapturingShortcut && (CanTest == null || CanTest()); pages[4].Controls.Add(notes);
            notes.ClearHistory = ClearActivity;
            notes.SourceChanged = value => noteSource.SelectedIndex = Array.IndexOf(new[] { "Microphone", "Computer", "Both" }, value);
            var notesHandle = notes.Handle;
            raw.TextChanged += (s, e) => RefreshCleaned(); cleanup.CheckedChanged += (s, e) => RefreshCleaned();
            testMic.Click += (s, e) => StartTest(false); transcribe.Click += (s, e) => StartTest(true); testStt.Click += (s, e) => CheckStt(); poll.Tick += (s, e) => PollTest();
            copy.Click += (s, e) => { if (cleaned.Text.Length > 0) { try { Clipboard.SetDataObject(cleaned.Text, true, 5, 100); copy.Text = "Copied"; } catch (Exception ex) { sttStatus.Text = "Could not copy: " + ex.Message; } } };
            UpdateTranscript(lastRaw, lastCleaned); RefreshHome(); ShowPage("Home");
            FormClosing += (s, e) => { if (!ConfirmUnsaved()) { e.Cancel = true; return; } StopTest(); if (CloseToTray && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemThemeChanged;
            foreach (var page in pages) foreach (Control control in page.Controls) control.Tag = control.Bounds;
            ResumeLayout(true);
            using (var graphics = CreateGraphics())
            {
                uiScale = graphics.DpiX / 96f;
                if (uiScale != 1) Scale(new SizeF(uiScale, uiScale));
            }
            foreach (var page in pages) page.Resize += (s, e) => LayoutPage((Panel)s);
            LayoutFooter(); ShowPage("Home"); ThemeColors.Apply(this, source.Theme); WireDirtyState();
            Shown += (sender, args) => BeginInvoke((Action)(() => { LayoutFooter(); foreach (var page in pages) if (page.Visible) LayoutPage(page); }));
        }

        static Label Label(Control parent, string text, int x, int y, int w, int h, float size, bool bold, Color color)
        {
            var label = new Label { UseCompatibleTextRendering = true, Text = text, Left = x, Top = y, Width = w, Height = h, BackColor = parent is Surface ? ((Surface)parent).Fill : parent.BackColor, ForeColor = color, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular) };
            parent.Controls.Add(label); return label;
        }
        static HubButton Button(Control parent, string text, int x, int y, int w, EventHandler click, bool primary = false)
        {
            var b = new HubButton { Text = text, Left = x, Top = y, Width = w, Height = 38, Primary = primary }; b.Click += click; if (x >= 500) b.Anchor = AnchorStyles.Top | AnchorStyles.Right; parent.Controls.Add(b); return b;
        }
        void Brand(Control parent)
        {
            var icon = new Panel { Left = 22, Top = 34, Width = 36, Height = 36, BackColor = Color.Transparent };
            icon.Paint += (s, e) => { e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic; e.Graphics.DrawImage(AppLogo.Image, icon.ClientRectangle); };
            parent.Controls.Add(icon); Label(parent, "Notewll", 68, 31, 112, 50, 12, true, Ink);
            Label(parent, "PRIVATE. OFFLINE.", 22, 99, 160, 20, 8, true, Muted);
        }
        void Heading(Panel page, string title, string subtitle)
        {
            Label(page, title, 40, 18, 700, 32, 18, false, Ink);
            Label(page, subtitle, 42, 56, 700, 28, 9, false, Muted);
        }
        Surface Card(Panel page, int top, int height)
        {
            var card = new Surface { Left = 40, Top = top, Width = 760, Height = height, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            page.Controls.Add(card); card.Width = Math.Max(650, page.ClientSize.Width - 80); return card;
        }
        void BuildHome()
        {
            var p = pages[0];
            homeWelcome = Label(p, "Make room for more.", 40, 18, 730, 34, 18, true, Ink);
            homeWelcomeDetail = Label(p, "Dictate into your apps and see how much time your voice gives back.", 42, 58, 730, 28, 9, false, Muted);
            var actions = Card(p, 100, 120); homeActions = actions;
            Label(actions, "What's on your mind?", 22, 10, 330, 28, 13, true, Ink);
            homeShortcut.SetBounds(22, 40, 700, 26); homeShortcut.ForeColor = Muted; actions.Controls.Add(homeShortcut);
            Button(actions, "Dictate", 22, 50, 166, (s, e) => { if (BeginDictation != null) BeginDictation(); else ShowPage("Playground"); }, true);
            Button(actions, "New note", 204, 50, 166, (s, e) => { ShowPage("Notes"); notes.NewWrittenNote(); });
            Button(actions, "Record meeting", 386, 50, 180, (s, e) => { ShowPage("Notes"); notes.Toggle(source.MeetingSource, "Meeting"); });
            readinessStatus = Label(p, "Speech engine: checking availability...", 42, 232, 720, 24, 9, false, Muted);
            BuildActivityHome(p); BuildSpeakingProgress(p);
            string detail; SetReadiness(SpeechEngine.Healthy(source, out detail) ? "Ready - English speech stays on this device." : detail);
        }
        void BuildPlayground() { BuildHomePractice(); }
        void BuildPreferences() { BuildOrganizedPreferences(); }
        void BuildHelp()
        {
            var p = pages[3]; Heading(p, "Help", "Choose what you need help with.");
            var card = Card(p, 100, 350);
            Button(card, "Text is not appearing", 22, 20, 290, (s, e) => MessageBox.Show(this, "Click the target field first. Try the edge toolbar or your dictation key. For terminals, choose a paste method in Writing. Windows may block typing into an administrator window. Your recovered text is available from the system tray.", "Typing into another app"));
            Button(card, "No microphone sound", 22, 70, 290, (s, e) => { ShowPage("Preferences"); SelectSettingsCategory("Recording"); });
            Button(card, "No meeting audio", 22, 120, 290, (s, e) => { ShowPage("Notes"); notes.ShowRecordingHelp(); });
            recoverButton = Button(card, "Recover a dictation", 22, 170, 290, (s, e) => { if (OpenRecovery != null) OpenRecovery(); });
            Button(card, "Export a conversation", 22, 220, 290, (s, e) => MessageBox.Show(this, "Open a saved note and choose Export PDF. Select conversation, personal notes or the full record. Original conversation text stays read-only; external PDF copies are not tamper-proof.", "PDF export"));
            Button(card, "Storage and backup", 22, 270, 290, (s, e) => { if (notes != null && !notes.Busy) { NotesStorageDialog.Show(this, notes.Store, source); notes.ReloadLibrary(); } });
            issuesButton = Button(p, "Issues & recovery", 40, 610, 220, (s, e) => ShowIssues()); issuesButton.Visible = issues.Length > 0;
            var tips = Card(p, 470, 120); Label(tips, "Your recording controls", 22, 14, 680, 24, 12, true, Ink);
            Label(tips, "Hover the edge bar for icons, then hover an icon for its name. Drag its grip to move.\nThe separate voice animation is optional in App preferences. Escape cancels dictation.\nFor meetings, use headphones and select the output device used by your call.", 22, 46, 690, 65, 9, false, Muted);
        }
        public void ShowPage(string name)
        {
            bool openPractice = name == "Playground" || name == "Practice";
            if (openPractice) name = "Home";
            for (int i = 0; i < pages.Length; i++) { bool active = pages[i].Name == name; pages[i].Visible = active; navigation[i].Selected = active; navigation[i].Invalidate(); if (active) pages[i].BringToFront(); }
            if (name == "Home" && notes != null) RefreshHome();
            if (name == "Notes" && notes != null && !notes.Busy) notes.ShowLibrary();
            CurrentPage = name; savePinned.Visible = sidebarStatus.Visible = (name == "Preferences" || name == "Dictionary" || name == "Snippets" || name == "Style");
            if (refreshLibrary != null) refreshLibrary();
            host.PerformLayout(); foreach (var page in pages) if (page.Visible) { page.AutoScrollPosition = Point.Empty; LayoutPage(page); }
            if (openPractice) SetHomePractice(true, true);
        }
        void LayoutFooter()
        {
            if (localFooter == null) return;
            localFooter.Top = sidebar.ClientSize.Height - (int)(82 * uiScale);
            privacyFooter.Top = sidebar.ClientSize.Height - (int)(55 * uiScale);
            savePinned.Top = sidebar.ClientSize.Height - (int)(135 * uiScale); sidebarStatus.Top = sidebar.ClientSize.Height - (int)(185 * uiScale);
        }
        void LayoutTranscript(Surface card)
        {
            int pad = (int)(24 * uiScale), width = (card.ClientSize.Width - pad * 3) / 2;
            raw.Left = rawHeading.Left = pad; raw.Width = rawHeading.Width = width;
            cleaned.Left = cleanedHeading.Left = pad * 2 + width; cleaned.Width = cleanedHeading.Width = width;
        }
        void LayoutPage(Panel page)
        {
            var scrollPosition = page.AutoScrollPosition;
            page.SuspendLayout();
            try
            {
            foreach (Control control in page.Controls)
            {
                if (!(control.Tag is Rectangle)) continue;
                var bounds = (Rectangle)control.Tag;
                int left = (int)(bounds.Left * uiScale), width = (int)(bounds.Width * uiScale);
                if (control is Label) width = Math.Min(width, page.ClientSize.Width - left - (int)(40 * uiScale));
                control.SetBounds(left + scrollPosition.X, (int)(bounds.Top * uiScale) + scrollPosition.Y, Math.Max(1, width), (int)(bounds.Height * uiScale));
            }
            foreach (var card in page.Controls.OfType<Surface>())
            {
                card.Width = Math.Max((int)(600 * uiScale), page.ClientSize.Width - (int)(80 * uiScale));
                foreach (var button in card.Controls.OfType<HubButton>().Where(b => (b.Anchor & AnchorStyles.Right) != 0)) button.Left = card.Width - button.Width - (int)(24 * uiScale);
                foreach (var child in card.Controls.OfType<Surface>()) child.Left = card.Width - child.Width - (int)(34 * uiScale);
            }
            if (page == pages[0]) { LayoutActivity(); LayoutHomePractice(); }
            if (page == pages[4] && notes != null) { page.AutoScroll = false; notes.Width = Math.Max((int)(600 * uiScale), page.ClientSize.Width - (int)(80 * uiScale)); notes.Height = Math.Max((int)(410 * uiScale), page.ClientSize.Height - notes.Top - (int)(24 * uiScale)); }
            if (page == pages[0] && raw.Parent != null) LayoutTranscript((Surface)raw.Parent);
            if (page == pages[2] && settingsTabs != null) settingsTabs.SetBounds((int)(40 * uiScale), (int)(112 * uiScale), page.ClientSize.Width - (int)(80 * uiScale), page.ClientSize.Height - (int)(136 * uiScale));
            }
            finally { page.ResumeLayout(); page.Invalidate(true); }
        }

        public void UpdateLiveTranscript(string text) { raw.Text = text ?? ""; cleaned.Text = text ?? ""; homeLatest.Text = text ?? ""; }
        public void UpdateTranscript(string text, string result)
        {
            raw.Text = text ?? ""; RefreshCleaned();
            if (!String.IsNullOrEmpty(result)) { cleaned.Text = result; copy.Enabled = cleaned.Text.Length > 0; }
            if (homeLatest.Parent != null) homeLatest.Parent.Visible = true;
            homeLatest.Text = String.IsNullOrWhiteSpace(cleaned.Text) ? "Recent notes open with a double-click. Dictation audio is not stored." : cleaned.Text;
        }
        void RefreshCleaned() { var draft = PracticeConfig(); cleaned.Text = WritingFormat.Compose(draft, raw.Text); copy.Enabled = cleaned.Text.Length > 0; copy.Text = "Copy text"; }
        void RefreshHome() { RefreshActivity(); homeShortcut.Text = "Hold " + source.Hotkey.Replace("+", " + ") + " to dictate"; homeShortcut.Font = new Font("Segoe UI", 10, FontStyle.Italic); homeMicrophone.Text = source.Microphone == Config.DefaultMic ? "Windows default microphone" : source.Microphone; }
        public void SavePreferences()
        {
            try
            {
                if (IsCapturingShortcut) throw new InvalidOperationException("Release your shortcut keys first.");
                Result = source.Copy(); Result.Hotkey = selectedShortcut; Result.NotesHotkey = Convert.ToString(noteShortcut.SelectedItem);
                Result.NotesSource = new[] { "Microphone", "Computer", "Both" }[noteSource.SelectedIndex]; Result.MeetingSource = new[] { "Microphone", "Computer", "Both" }[meetingSource.SelectedIndex];
                Result.QualityEnabled = quality.Checked; Result.AccurateDictationEnabled = accurateMode.Checked; Result.Microphone = SelectedMic(); Result.SmartCleanupEnabled = false; Result.CleanupEnabled = cleanup.Checked; Result.AutoPaste = autoPaste.Checked; Result.LiveWriting = liveWriting.Checked;
                Result.PasteMethod = new[] { "Auto", "CtrlV", "CtrlShiftV", "ShiftInsert" }[pasteMethod.SelectedIndex]; Result.StartWithWindows = startup.Checked;
                Result.UpdateManifestUrl = updateUrl.Text.Trim(); Result.UpdatePublisherThumbprint = updatePublisher.Text.Replace(" ", "").Trim();
                Result.CheckUpdatesOnStart = updateOnStart.Checked; Result.WritingStyle = writingStyle.Text;
                Result.Personalization = pendingRules; Result.Theme = "System"; Result.ShowToolbar = toolbarChoice.Checked; Result.ShowVoiceIndicator = indicatorChoice.Checked; Result.ReducedMotion = motionChoice.Checked; Result.ReduceNoise = noiseChoice.Checked;
                var ballDraft=BallDraft(); Result.BallAppearance=ballDraft.BallAppearance; Result.BallGravity=ballDraft.BallGravity;
                Result.BallColorMode=ballDraft.BallColorMode; Result.BallColor1=ballDraft.BallColor1; Result.BallColor2=ballDraft.BallColor2; Result.BallGradientAngle=ballDraft.BallGradientAngle;
                Result.BallSizePercent=ballDraft.BallSizePercent; Result.BallExpandedSizePercent=ballDraft.BallExpandedSizePercent; Result.BallImagePath=ballDraft.BallImagePath;
                Result.Validate(); string warning = Shortcut.Warning(Result.Hotkey, Result.NotesHotkey); if (warning != null && MessageBox.Show(this, warning + "\nSave this shortcut anyway?", "Shortcut warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return; if (OnSaved != null) OnSaved(Result); source = Result; dirty = false; savePinned.Text = "Save preferences"; ThemeColors.Apply(this, source.Theme); if (notes != null) notes.UpdateConfig(Result); RefreshHome(); saved.Text = "Saved. Your next dictation uses these preferences.";
            }
            catch (Exception ex) { saved.Text = "Could not save: " + ex.Message; }
            sidebarStatus.Text = saved.Text;
        }
        string SelectedMic() { return mic.SelectedIndex <= 0 ? Config.DefaultMic : mic.SelectedItem.ToString(); }
        int practiceGeneration;
        void StartTest(bool andTranscribe)
        {
            if (testRecorder != null && testTranscribes && andTranscribe) { FinishPractice(); return; }
            if (transcribing || (CanTest != null && !CanTest())) { sttStatus.Text = "Finish your current dictation before testing."; return; }
            StopTest();
            try { testRecorder = new MicRecorder(SelectedMic()); testRecorder.Start(); }
            catch (Exception ex) { micStatus.Text = "Microphone unavailable. Choose a device and check Windows microphone permission. " + ex.Message; sttStatus.Text = micStatus.Text; return; }
            testTranscribes = andTranscribe; testEnds = DateTime.UtcNow.AddSeconds(andTranscribe ? 60 : 3);
            sttStatus.Text = andTranscribe ? "Speak a sentence, then click Stop and review. Maximum: one minute." : "Checking microphone activity..."; micStatus.Text = "Listening...";
            testMic.Enabled = false; transcribe.Enabled = andTranscribe; transcribe.Text = andTranscribe ? "Stop and review" : "Checking..."; poll.Start();
        }
        void StopTest()
        {
            practiceGeneration++; transcribing = false; poll.Stop(); testMic.Enabled = transcribe.Enabled = true; transcribe.Text = "Record a sentence"; meter.Value = 0;
            if (testRecorder != null) { testRecorder.Dispose(); testRecorder = null; }
        }
        void PollTest()
        {
            if (testRecorder == null) return; meter.Value = Math.Min(100, testRecorder.Level * 100 / 12000);
            if (DateTime.UtcNow >= testEnds) FinishPractice();
        }
        void FinishPractice()
        {
            if (testRecorder == null) return;
            string device = testRecorder.DeviceName; int peak = testRecorder.Peak; byte[] pcm = testRecorder.Stop(); bool recognize = testTranscribes; StopTest();
            source.SetupMicrophoneVerified = peak >= 150; micStatus.Text = peak < 150 ? "No sound detected. Choose another microphone or check permission." : "Microphone activity detected.";
            if (peak < 150) { sttStatus.Text = "No sound captured from " + device + ". Choose your microphone in Preferences."; Array.Clear(pcm, 0, pcm.Length); return; }
            if (!recognize) { if (CloseToTray) try { source.Save(); } catch (Exception ex) { micStatus.Text = "Input detected; setup status could not be saved: " + ex.Message; } Array.Clear(pcm, 0, pcm.Length); return; }
            transcribing = true; int gen = practiceGeneration; testMic.Enabled = transcribe.Enabled = false; sttStatus.Text = "Recognizing your sentence locally..."; var cfg = PracticeConfig(); cfg.Microphone = SelectedMic();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                string text = null, corrected = null, error = null; try { text = DictationSpeech.Transcribe(cfg, pcm); corrected = SmartCleanup.Complete(cfg, text, 8500); } catch (Exception ex) { error = ex.Message; } finally { Array.Clear(pcm, 0, pcm.Length); }
                Post(() => { if (gen != practiceGeneration) return; transcribing = false; testMic.Enabled = transcribe.Enabled = true; transcribe.Text = "Record a sentence";
                    if (error != null) { sttStatus.Text = error; return; } UpdateTranscript(text, corrected); source.SetupSpeechVerified = !String.IsNullOrWhiteSpace(text); if (CloseToTray) try { source.Save(); } catch (Exception ex) { sttStatus.Text = "Practice succeeded, but setup status could not be saved: " + ex.Message; return; }
                    sttStatus.Text = source.SetupSpeechVerified ? (SmartCleanup.Enabled(cfg) ? SmartCleanup.Status : "Sentence recognized using the selected correction mode. Compare both versions below.") : "No words recognized. Try a clear sentence.";
                });
            });
        }
        void CheckStt()
        {
            if (checking) return; checking = true; saved.Text = "Checking your bundled voice engine…"; testStt.Enabled = false;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                string problem = null;
                try { if (source.AccurateDictationEnabled) AccurateSpeech.Warmup(source.Copy()); else problem = SpeechEngine.EnsureRunning(source, 30); }
                catch (Exception ex) { problem = ex.Message; }
                Post(() => { checking = false; testStt.Enabled = true; saved.Text = problem ?? "Ready. The offline speech engine is working."; sidebarStatus.Text = saved.Text; SetReadiness(saved.Text); });
            });
        }
        void Post(Action action)
        {
            try { if (IsHandleCreated && !IsDisposed) BeginInvoke((Action)(() => { if (!IsDisposed) action(); })); }
            catch (InvalidOperationException) { } // Closing the app while a local request completes.
        }
        void SystemThemeChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e) { Post(() => { ThemeColors.Apply(this, source.Theme); }); }
        protected override void Dispose(bool disposing) { if (disposing) { Microsoft.Win32.SystemEvents.UserPreferenceChanged -= SystemThemeChanged; StopTest(); poll.Dispose(); } base.Dispose(disposing); }
    }
}
