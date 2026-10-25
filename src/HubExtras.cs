using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace LocalDictation
{
    sealed partial class SettingsForm
    {
        readonly ListBox recentNotes = new ListBox { BorderStyle = BorderStyle.None, DisplayMember = "Title", Font = new Font("Segoe UI", 9), IntegralHeight = false };
        AppTabs settingsTabs;
        Label readinessStatus;
        readonly ComboBox writingStyle = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        HubButton recoverButton;
        Label toolbarStatus;
        public void SetToolbarStatus(string value) { if (toolbarStatus != null) toolbarStatus.Text = "Toolbar: " + value; }
        public void SetRecoveryAvailable(bool available) { if (recoverButton != null) recoverButton.Visible = available; }
        readonly CheckBox toolbarChoice = new CheckBox { Text = "Show the edge toolbar", AutoSize = true };
        readonly CheckBox indicatorChoice = new CheckBox { Text = "Show the separate voice animation while recording", AutoSize = true };
        readonly CheckBox motionChoice = new CheckBox { Text = "Reduce motion", AutoSize = true };
        readonly CheckBox noiseChoice = new CheckBox { Text = "Suppress quiet background noise in notes", AutoSize = true };
        readonly TextBox updateUrl = new TextBox(), updatePublisher = new TextBox();
        readonly CheckBox updateOnStart = new CheckBox { Text = "Check the configured signed-update feed on startup", AutoSize = true };
        bool dirty;
        System.Collections.Generic.List<PersonalRule> pendingRules;
        public Action BeginDictation, OpenRecovery;
        public Action DismissIssues;
        string[] issues = new string[0];
        HubButton issuesButton;
        public void SetIssues(System.Collections.Generic.IEnumerable<string> entries) { issues = entries.ToArray(); if (issuesButton != null) { issuesButton.Text = "Issues (" + issues.Length + ")"; issuesButton.Visible = issues.Length > 0; } }
        public void ShowIssues()
        {
            using (var dialog = DialogUi.Create("Issues & recovery", new Size(680, 460)))
            {
                var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Text = issues.Length == 0 ? "No unacknowledged issues this session." : String.Join("\r\n\r\n", issues) };
                var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 110, WrapContents = true, AutoScroll = true };
                var recover = new Button { Text = "Recover last dictation", AutoSize = true }; recover.Click += (s, e) => { dialog.Close(); if (OpenRecovery != null) OpenRecovery(); };
                var acknowledge = new Button { Text = "Acknowledge issues", AutoSize = true }; acknowledge.Click += (s, e) => { if (DismissIssues != null) DismissIssues(); dialog.Close(); };
                recover.Visible = recoverButton != null && recoverButton.Visible;
                actions.Controls.Add(DialogUi.Button("Microphone permissions", (s, e) => OpenPermissionSettings()));
                actions.Controls.Add(DialogUi.Button("Meeting audio help", (s, e) => { dialog.Close(); ShowPage("Notes"); notes.ShowRecordingHelp(); }));
                actions.Controls.Add(DialogUi.Button("Typing help", (s, e) => { dialog.Close(); ShowPage("Help"); }));
                actions.Controls.AddRange(new Control[] { recover, acknowledge }); dialog.Controls.Add(text); dialog.Controls.Add(actions); ThemeColors.Apply(dialog, source.Theme); dialog.ShowDialog(this);
            }
        }
        public void SetReadiness(string value) { if (readinessStatus != null) readinessStatus.Text = value; }
        void MarkDirty() { dirty = true; savePinned.Text = "Save changes *"; }
        void WireDirtyState()
        {
            updateUrl.TextChanged += (s, e) => MarkDirty(); updatePublisher.TextChanged += (s, e) => MarkDirty();
            updateOnStart.CheckedChanged += (s, e) => MarkDirty(); writingStyle.SelectedIndexChanged += (s, e) => { MarkDirty(); RefreshCleaned(); };
            foreach (var box in new[] { quality, accurateMode, cleanup, autoPaste, liveWriting, startup, toolbarChoice, indicatorChoice, motionChoice, noiseChoice }) box.CheckedChanged += (s, e) => MarkDirty();
            foreach (var box in new[] { mic, noteShortcut, noteSource, meetingSource, pasteMethod }) box.SelectedIndexChanged += (s, e) => MarkDirty();
        }
        bool ConfirmUnsaved()
        {
            if (!dirty || !CloseToTray) return true;
            var result = MessageBox.Show(this, "Save your preference changes before closing?", "Unsaved preferences", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (result == DialogResult.Cancel) return false;
            if (result == DialogResult.Yes) { SavePreferences(); return !dirty; }
            selectedShortcut = source.Hotkey; hotkey.Text = selectedShortcut;
            correctionMode.SelectedIndex = source.CleanupEnabled ? 1 : 0; quality.Checked = source.QualityEnabled; accurateMode.Checked = source.AccurateDictationEnabled; cleanup.Checked = source.CleanupEnabled; autoPaste.Checked = source.AutoPaste; liveWriting.Checked = source.LiveWriting; startup.Checked = source.StartWithWindows;
            toolbarChoice.Checked = source.ShowToolbar; indicatorChoice.Checked = source.ShowVoiceIndicator; motionChoice.Checked = source.ReducedMotion; noiseChoice.Checked = source.ReduceNoise; writingStyle.SelectedItem = source.WritingStyle;
            noteShortcut.SelectedItem = source.NotesHotkey; noteSource.SelectedIndex = Array.IndexOf(new[] { "Microphone", "Computer", "Both" }, source.NotesSource); meetingSource.SelectedIndex = Array.IndexOf(new[] { "Microphone", "Computer", "Both" }, source.MeetingSource);
            mic.SelectedIndex = Math.Max(0, mic.Items.IndexOf(source.Microphone)); pasteMethod.SelectedIndex = Array.IndexOf(new[] { "Auto", "CtrlV", "CtrlShiftV", "ShiftInsert" }, source.PasteMethod);
            updateUrl.Text = source.UpdateManifestUrl; updatePublisher.Text = source.UpdatePublisherThumbprint;
            updateOnStart.Checked = source.CheckUpdatesOnStart;
            LoadBallChoices(source);
            pendingRules = source.Copy().Personalization; dirty = false; savePinned.Text = "Save preferences"; return true;
        }
        Panel SettingsTab(string name)
        {
            var tab = new TabPage(name) { Padding = new Padding(12), BackColor = Paper };
            settingsTabs.TabPages.Add(tab); var panel = new Panel { Size = new Size(700, 520), Dock = DockStyle.Fill, AutoScroll = true, BackColor = Paper }; tab.Controls.Add(panel);
            panel.Resize += (s, e) => { float scale; using (var g = panel.CreateGraphics()) scale = g.DpiX / 96f; foreach (Control child in panel.Controls) if (child is Label || (child.Anchor & AnchorStyles.Right) != 0) child.Width = Math.Max(1, panel.ClientSize.Width - child.Left - (int)(20 * scale)); };
            return panel;
        }
        void Setting(Panel parent, Control control, int top, int height = 32)
        {
            control.SetBounds(20, top, 630, height); control.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; parent.Controls.Add(control);
        }
        void BuildOrganizedPreferences()
        {
            var p = pages[2]; Heading(p, "Preferences", "Recording, writing and storage - organized in one place.");
            settingsTabs = new AppTabs { Left = 40, Top = 112, Width = 760, Height = 580, Font = Font }; p.Controls.Add(settingsTabs);
            var recording = SettingsTab("Recording");
            Label(recording, "Hold-to-write shortcut", 20, 16, 620, 28, 12, true, Ink); Setting(recording, hotkey, 54, 40);
            Label(recording, "Press a key or combination. Modifier keys can delay live writing until release.", 20, 104, 620, 42, 9, false, Muted);
            Label(recording, "Microphone", 20, 154, 620, 25, 11, true, Ink); Setting(recording, mic, 186);
            testMic.SetBounds(20, 238, 160, 36); recording.Controls.Add(testMic); meter.SetBounds(200, 250, 270, 12); recording.Controls.Add(meter);
            micStatus.SetBounds(20, 286, 620, 44); recording.Controls.Add(micStatus);
            Button(recording, "Windows microphone permissions", 20, 342, 294, (s, e) => OpenPermissionSettings());
            testStt.Text = "Check speech engine"; testStt.SetBounds(332, 342, 200, 38); recording.Controls.Add(testStt);
            Button(recording, "Repair speech installation", 20, 394, 260, (s, e) => { var setup = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Uninstall.exe"); if (File.Exists(setup)) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = setup, UseShellExecute = true }); else MessageBox.Show(this, "Run the latest Setup executable to repair the speech files."); });
            pendingRules = source.Copy().Personalization;
            var writing = SettingsTab("Writing");
            BuildCorrectionModes(writing); Setting(writing, autoPaste, 152); Setting(writing, liveWriting, 188); Setting(writing, accurateMode, 224); Setting(writing, quality, 260);
            Label(writing, "Paste method", 20, 300, 600, 26, 11, true, Ink); Setting(writing, pasteMethod, 330);
            Label(writing, "Writing style", 20, 370, 600, 26, 11, true, Ink); writingStyle.Items.AddRange(new object[] { "Standard", "Chat", "Email", "Code" }); writingStyle.SelectedItem = source.WritingStyle; Setting(writing, writingStyle, 400); Button(writing, "Words & snippets", 20, 446, 248, (s, e) => EditRules());
            Label(writing, "Accurate mode needs the optional local setup. Wait for Ready before dictating.\nChoose Raw text to keep recognized words, or Modified text for basic cleanup.\nBoth modes run on your device without a sentence-editing model.", 20, 492, 620, 90, 10, false, Muted);
            var noteSettings = SettingsTab("Notes");
            Label(noteSettings, "Recording shortcut - select one", 20, 20, 620, 28, 11, true, Ink); Setting(noteSettings, noteShortcut, 58);
            Label(noteSettings, "Personal-note audio", 20, 120, 620, 28, 11, true, Ink); Setting(noteSettings, noteSource, 158);
            Label(noteSettings, "Meeting audio", 20, 220, 620, 28, 11, true, Ink); Setting(noteSettings, meetingSource, 258); Setting(noteSettings, noiseChoice, 324);
            Label(noteSettings, "The shortcut toggles personal notes. Start meetings from Home or the floating ball.\nNoise suppression reduces quiet sound; it does not guarantee removal of music or every background voice.", 20, 376, 620, 74, 9, false, Muted);
            var appearance = SettingsTab("App");
            toolbarChoice.Checked = source.ShowToolbar; indicatorChoice.Checked = source.ShowVoiceIndicator; motionChoice.Checked = source.ReducedMotion; noiseChoice.Checked = source.ReduceNoise;
            toolbarStatus = Label(appearance, "Toolbar: " + (source.ShowToolbar ? "Ready on the screen edge" : "Hidden in preferences"), 20, 324, 620, 44, 9, false, Muted); Setting(appearance, toolbarChoice, 40); Setting(appearance, indicatorChoice, 86); Setting(appearance, motionChoice, 132); Setting(appearance, startup, 178);
            Label(appearance, "Click the ball to reveal its icons. Drag and release it to drop it to the bottom edge.\nKeyboard users can open the Windows tray action menu without changing the target text field.", 20, 240, 620, 76, 10, false, Muted);
            BuildHandsFreePreferences();
            BuildBallPreferences();
            var storage = SettingsTab("Storage");
            Label(storage, "Keep control of your local data", 20, 20, 620, 32, 14, true, Ink);
            Label(storage, "Recordings, deleted notes and download cache use disk space.\nBack up notes before moving them. Conversation exports can include private annotations.\nSpeech and notes stay on this PC; no account or cloud subscription is required.", 20, 78, 620, 110, 10, false, Muted);
            Button(storage, "Storage & recently deleted", 20, 220, 284, (s, e) => { if (notes != null && !notes.Busy) { if (PauseHandsFree != null) PauseHandsFree(); NotesStorageDialog.Show(this, notes.Store, source); notes.ReloadLibrary(); } else MessageBox.Show(this, "Finish the current recording/transcription before managing storage."); });
            var about = SettingsTab("About");
            Label(about, "Notewll 0.10.0", 20, 20, 620, 38, 18, true, Ink);
            Label(about, "Windows x64 | English | One-file offline setup\nIncludes the app, speech runtime and English model.\n\nVoice typing, personal notes and meeting recordings.\nCustomizable floating ball with gravity, bounce and rolling.\nLive color/gradient previews, separate ball/menu sizes, and rotating local images.\n\nEnhanced recognition models are optional downloads.\nThis Windows preview is unsigned.", 20, 82, 630, 220, 10, false, Muted);
            Button(about, "Open release notes", 20, 336, 220, (s, e) => ShowReleaseNotes());
            Button(about, "Check for updates", 260, 336, 220, async (s, e) => await ReleaseUpdates.Check(this, source));
            var updateInfo = String.IsNullOrWhiteSpace(source.UpdateManifestUrl) || String.IsNullOrWhiteSpace(source.UpdatePublisherThumbprint) ? "Check for updates opens GitHub Releases. Run the latest installer to update; your notes and preferences are kept." : "Signed updates are available from this publisher. Downloads are verified before installation.";
            Label(about, updateInfo, 20, 398, 620, 70, 10, false, Muted);
            Setting(about, updateOnStart, 482); updateOnStart.Checked = source.CheckUpdatesOnStart; updateOnStart.Enabled = !String.IsNullOrWhiteSpace(source.UpdateManifestUrl) && !String.IsNullOrWhiteSpace(source.UpdatePublisherThumbprint);
            updateUrl.Text = source.UpdateManifestUrl; updatePublisher.Text = source.UpdatePublisherThumbprint;

        }
        void EditRules()
        {
            var draft = source.Copy(); draft.Personalization = pendingRules;
            PersonalRule.Edit(this, draft, () => { pendingRules = draft.Personalization; MarkDirty(); });
        }
        void RememberCorrection()
        {
            var entry = PersonalRule.Prompt(this, new PersonalRule { Kind = "Word", Trigger = raw.SelectedText, Replacement = "" }, source.Theme, source);
            if (entry == null) return;
            try { var proposed = pendingRules.Where(rule => !rule.Trigger.Equals(entry.Trigger, StringComparison.OrdinalIgnoreCase)).Concat(new[] { entry }).ToList(); PersonalRule.Validate(proposed); pendingRules = proposed; ShowPage("Preferences"); SelectSettingsCategory("Writing"); MarkDirty(); saved.Text = "Correction added. Save preferences to use it."; } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not remember correction"); }
        }
        async void TestAudioFile()
        {
            using (var file = new OpenFileDialog { Filter = "PCM WAV audio|*.wav", Title = "Choose a 16 kHz mono PCM16 speech sample" })
            {
                if (file.ShowDialog(this) != DialogResult.OK) return;
                if (new FileInfo(file.FileName).Length > 20 * 1024 * 1024) { MessageBox.Show(this, "Use a test clip smaller than 20 MB."); return; }
                try
                {
                    byte[] wav = File.ReadAllBytes(file.FileName); sttStatus.Text = "Transcribing the selected local test clip...";
                    var config = source.Copy();
                    string result = await System.Threading.Tasks.Task.Run(() => DictationSpeech.Transcribe(config, SpeechEngine.PcmFromWav(wav)));
                    if (!IsDisposed) { UpdateTranscript(result, result); sttStatus.Text = "Local test complete. Compare the recognized words against your reference transcript."; }
                }
                catch (Exception ex) { if (!IsDisposed) sttStatus.Text = ex.Message; }
            }
        }
        internal void SelectSettingsCategory(string name) { foreach (TabPage tab in settingsTabs.TabPages) if (tab.Text == name) { settingsTabs.SelectedTab = tab; break; } }
        static void OpenPermissionSettings() { try { System.Diagnostics.Process.Start("ms-settings:privacy-microphone"); } catch (Exception ex) { MessageBox.Show(ex.Message, "Open microphone settings"); } }
        void ShowReleaseNotes() { MessageBox.Show(this, "0.10.0\n\nOptional hands-free mode with local voice enrollment.\nMatched phrases type after a pause into the text field you click.\nClick the listening ball or press Escape to pause the mic.\n\nOne-file offline installation for Windows x64.\nVoice typing, personal notes and meeting recordings.\nA floating ball you can throw, bounce and customize.\nSports and planet skins, nine gravity environments and live previews.\nIndependent ball/menu sizing, custom colors and rotating local images.\nReadable black icons on translucent round white tiles.\n\nThe installer includes the basic English speech engine and model. Enhanced recognition models are optional downloads. This Windows preview is unsigned; real microphone, call and cross-app typing acceptance still depends on the desktop environment.", "Release notes"); }

        public void ShowFirstRunGuide() { FirstRunSetup(); source.SetupGuideSeen = true; source.Save(); }
        void FirstRunSetup()
        {
            using (var dialog = DialogUi.Create("Check your setup", new Size(640, 510)))
            {
                string detail; bool available = SpeechEngine.Healthy(source, out detail);
                var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
                var status = DialogUi.Text("1. Voice files: " + (available ? "Ready - English, offline" : detail) + "\r\n\r\n2. Microphone: " + (source.SetupMicrophoneVerified ? "Input activity verified" : "Not tested yet") + "\r\n\r\n3. Practice sentence: " + (source.SetupSpeechVerified ? "Recognized successfully" : "Not verified yet") + "\r\n\r\n4. Other-app typing: " + (source.SetupInsertionConfirmed ? "Confirmed by you" : "Not confirmed yet")); flow.Controls.Add(status);
                flow.Controls.Add(DialogUi.Button("Microphone permissions", (s, e) => OpenPermissionSettings()));
                flow.Controls.Add(DialogUi.Button("Choose microphone", (s, e) => { dialog.Close(); ShowPage("Preferences"); SelectSettingsCategory("Recording"); }));
                flow.Controls.Add(DialogUi.Button("Try dictation on Home", (s, e) => { dialog.Close(); ShowPage("Playground"); }));
                flow.Controls.Add(DialogUi.Text("For the last step, click a text field in Notepad or your browser, hold your dictation key, speak and release. Check that the text appears correctly."));
                var confirm = DialogUi.Button("I tested typing in another app", (s, e) => { source.SetupInsertionConfirmed = true; if (CloseToTray) source.Save(); dialog.Close(); }, true); confirm.Enabled = source.SetupSpeechVerified; flow.Controls.Add(confirm);
                flow.Controls.Add(DialogUi.Button("Do this later", (s, e) => dialog.Close()));
                flow.Resize += (s, e) => { foreach (Label label in flow.Controls.OfType<Label>()) label.MaximumSize = new Size(Math.Max(240, flow.ClientSize.Width - 32), 0); };
                dialog.Controls.Add(flow); ThemeColors.Apply(dialog, source.Theme); dialog.ShowDialog(this);
            }
        }
    }
}
