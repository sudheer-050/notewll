using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
namespace Notewll
{
    static class UxVerification
    {
        static BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static object Field(object target, string name) { return target.GetType().GetField(name, Flags).GetValue(target); }
        static void Invoke(object target, string name) { target.GetType().GetMethod(name, Flags).Invoke(target, null); }
        static System.Collections.Generic.IEnumerable<Control> Controls(Control parent) { foreach (Control child in parent.Controls) { yield return child; foreach (var nested in Controls(child)) yield return nested; } }
        static void Snapshot(Form form, string path)
        {
            Application.DoEvents(); foreach (var control in Controls(form)) if (control.Visible) control.Refresh(); form.Refresh();
            using (var image = new Bitmap(form.Width, form.Height)) using (var graphics = Graphics.FromImage(image)) { IntPtr dc = graphics.GetHdc(); try { Native.PrintWindow(form.Handle, dc, 0); } finally { graphics.ReleaseHdc(dc); } image.Save(path); }
        }
        static void Dialog(Form owner, string name, string root, Action open, Action<Form> inspect)
        {
            Exception failure = null; bool found = false;
            using (var timer = new Timer { Interval = 200 })
            {
                timer.Tick += (s, e) => { var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(form => form != owner && form.Visible); if (dialog == null) return; timer.Stop(); found = true; try { inspect(dialog); Snapshot(dialog, Path.Combine(root, name + ".png")); } catch (Exception ex) { failure = ex; } finally { dialog.Close(); } };
                timer.Start(); open(); timer.Stop();
            }
            if (failure != null) throw failure; if (!found) throw new Exception("Dialog did not open: " + name);
        }
        public static void Run(string root)
        {
            Directory.CreateDirectory(root); Config.DiagnosticDataRoot(Path.Combine(root, "settings"));
            var config = new Config { NoteMicrophoneId = "previous-microphone", NoteOutputId = "previous-output" }; var store = new NoteStore(Path.Combine(root, "notes")); var note = store.Create("Release review", "Computer", "Alex"); note.Status = "Saved"; note.WrittenNotes = "Keep personal notes separate."; note.Segments.Add(new NoteSegment { Source = "Computer", Complete = true, Frames = 16000, RawText = "The original words stay unchanged.", Text = "The original words stay unchanged." }); store.Save(note);
            using (var track = new WaveTrack(store.AudioPath(note, "Computer"))) track.WriteAt(0, new byte[32000]); string original = NoteStore.Transcript(note);
            Application.EnableVisualStyles();
            using (var hub = new SettingsForm(config, "", "", store))
            {
                Config applied = null; hub.OnSaved = value => applied = value;
                hub.Show(); hub.UpdateTranscript("sue dear has a meeting", "Sudheer has a meeting.");
                var activityCard = (Surface)Field(hub, "activityCard");
                if (!((Control)Field(hub, "voiceKpis")).Visible || !((Control)Field(hub, "typingSpeed")).Visible || activityCard.Controls.OfType<HubButton>().Any() || Controls(hub).OfType<Label>().Any(label => label.Visible && label.Text == "Recent work")) throw new Exception("Home analytics is collapsed or Recent work remains.");
                var homeActions = (Surface)Field(hub, "homeActions"); var create = homeActions.Controls.OfType<HubButton>().Single();
                if (create.AccessibleName != "Create" || homeActions.Controls.Count != 2) throw new Exception("Home creation card still contains separate actions or extra text.");
                var normalSize = hub.Size;
                foreach (var size in new[] { normalSize, hub.MinimumSize })
                {
                    hub.Size = size; Application.DoEvents();
                    var heading = homeActions.Controls.OfType<Label>().Single();
                    if (heading.Right >= create.Left || create.Right > homeActions.ClientSize.Width || Math.Abs(create.Top + create.Height / 2 - homeActions.Height / 2) > 2) throw new Exception("Home Create button or heading is misaligned.");
                    Snapshot(hub, Path.Combine(root, size == normalSize ? "home-create.png" : "home-create-compact.png"));
                }
                hub.Size = normalSize; Application.DoEvents();
                using (var menu = hub.BuildHomeCreateMenu())
                {
                    if (!menu.Items.Cast<ToolStripItem>().Select(item => item.Text).SequenceEqual(new[] { "Dictate", "Note", "Meeting", "Conversation" })) throw new Exception("Home Create menu choices are incorrect.");
                    menu.Show(create, new Point(create.Width, create.Height), ToolStripDropDownDirection.BelowLeft); Application.DoEvents();
                    if (menu.Items.Cast<ToolStripItem>().Any(item => item.Height < menu.Font.Height + 12 || item.Width < menu.ClientSize.Width - menu.Padding.Horizontal - 2)) throw new Exception("Create menu has cramped or uneven rows.");
                    hub.CanTest = () => false; menu.Items[0].PerformClick(); hub.CanTest = null; menu.Close();
                    if (hub.CurrentPage != "Home" || !((TextBox)Field(hub, "raw")).Visible) throw new Exception("Create > Dictate did not open the editor.");
                    ((HubButton)Field(hub, "practiceToggle")).PerformClick();
                }
                using (var persistent = DialogUi.PopupMenu(false))
                {
                    int clicks = 0; persistent.Items.Add("Open Notewll", null, (s, e) => clicks++);
                    var advanced = new ToolStripMenuItem("More options"); advanced.DropDownItems.Add("Preferences"); persistent.Items.Add(advanced);
                    for (int i = 0; i < 2; i++)
                    {
                        persistent.Show(create, new Point(0, create.Height)); Application.DoEvents();
                        if (advanced.DropDown.Font.Size != persistent.Font.Size) throw new Exception("Submenu styling was not applied.");
                        persistent.Items[0].PerformClick(); persistent.Close(); Application.DoEvents();
                        if (persistent.IsDisposed || clicks != i + 1) throw new Exception("A persistent tray menu stopped working after closing.");
                    }
                }
                var continueDraft = typeof(SettingsForm).GetMethod("ContinueDraft", Flags | BindingFlags.Static);
                if ((string)continueDraft.Invoke(null, new object[] { "First line", "Second line" }) != "First line" + Environment.NewLine + "Second line" || (string)continueDraft.Invoke(null, new object[] { "First line", "" }) != "First line") throw new Exception("Repeat dictation replaced earlier text or inserted silence.");
                if (Controls(hub).OfType<ThemedTextBox>().Any(field => field.AutoSize)) throw new Exception("Text fields still collapse to the Windows single-line default height.");
                hub.ShowPage("Preferences");
                var accurate = (CheckBox)Field(hub, "accurateMode"); var enhanced = (CheckBox)Field(hub, "quality");
                accurate.Checked = enhanced.Checked = true;
                var draft = (Config)typeof(SettingsForm).GetMethod("PracticeConfig", Flags).Invoke(hub, null);
                if (!draft.AccurateDictationEnabled || !draft.QualityEnabled || config.AccurateDictationEnabled || config.QualityEnabled) throw new Exception("Speech practice does not use isolated draft preferences.");
                accurate.Checked = enhanced.Checked = false;
                var modernTabs = (AppTabs)Field(hub, "settingsTabs");
                foreach (TabPage page in modernTabs.TabPages)
                {
                    Controls(modernTabs).OfType<HubButton>().Single(b => b.Tag == page).PerformClick(); Application.DoEvents();
                    if (modernTabs.SelectedTab != page || !page.Visible) throw new Exception("Modern tab navigation failed: " + page.Text);
                }
                hub.SelectSettingsCategory("Recording");
                Snapshot(hub, Path.Combine(root, "preferences-modern.png"));
                ThemeColors.Apply(hub, "Dark"); Snapshot(hub, Path.Combine(root, "preferences-modern-dark.png")); ThemeColors.Apply(hub, config.Theme);
                foreach (string page in new[] { "Dictionary", "Snippets" })
                {
                    hub.ShowPage(page); Application.DoEvents();
                    var table = Controls(hub).OfType<ListView>().Single(c => c.AccessibleName == (page == "Dictionary" ? "Saved dictionary words" : "Saved snippets"));
                    if (table.Items.Count != 0 || Controls(hub).OfType<ComboBox>().Any(c => c.AccessibleName == (page == "Dictionary" ? "Saved dictionary words" : "Saved snippets"))) throw new Exception("The old saved-entry picker is still shown on " + page + ".");
                    string trigger = page == "Dictionary" ? "sue dear" : "my sign off";
                    string replacement = page == "Dictionary" ? "Sudheer" : "Kind regards, Sudheer";
                    var add = Controls(hub).OfType<Button>().Single(b => b.Visible && b.Text == (page == "Dictionary" ? "Add word" : "Add snippet"));
                    Dialog(hub, page + "-add", root, () => add.PerformClick(), dialog => {
                        if (page == "Dictionary")
                        {
                            Controls(dialog).OfType<TextBox>().Single(t => t.AccessibleName == "What you say").Text = trigger;
                            Controls(dialog).OfType<TextBox>().Single(t => t.AccessibleName == "Correct spelling").Text = replacement;
                            Controls(dialog).OfType<Button>().Single(b => b.Text == "Save word").PerformClick();
                        }
                        else
                        {
                            Controls(dialog).OfType<TextBox>().Single(t => t.AccessibleName == "What you say").Text = trigger;
                            Controls(dialog).OfType<RichTextBox>().Single(t => t.AccessibleName == "What to write").Text = replacement;
                            Controls(dialog).OfType<Button>().Single(b => b.Text == "Save snippet").PerformClick();
                        }
                    });
                    if (Controls(hub).Any(c => c.Visible && (c.AccessibleName == page + " preview" || c.Text == "Try a sentence"))) throw new Exception("The removed sentence preview is still visible.");
                    if (table.Columns[0].Text != "When you say" || table.Columns[1].Text != "Notewll writes" || table.Columns[0].TextAlign != HorizontalAlignment.Right || table.Columns[1].TextAlign != HorizontalAlignment.Left) throw new Exception("The library header changed.");
                    if (!(table is LibraryTable) || table.GetItemRect(0).Height < table.Font.Height + 12) throw new Exception("Library table rows are cramped.");
                    if (table.Items.Count != 1 || table.SelectedItems.Count != 1 || ((PersonalRule)table.SelectedItems[0].Tag).Trigger != trigger || table.Items[0].SubItems[1].Text != replacement) throw new Exception("The saved entry or row selection is incorrect.");
                    if (table.Height < table.Parent.ClientSize.Height - table.Parent.Padding.Vertical - 2) throw new Exception("Saved rows do not fill the table.");
                    var rules = (System.Collections.Generic.List<PersonalRule>)Field(hub, "pendingRules");
                    var extra = new PersonalRule { Kind = page == "Dictionary" ? "Word" : "Snippet", Trigger = "zzz test", Replacement = "another result" };
                    rules.Add(extra); ((Action)Field(hub, "refreshLibrary"))();
                    if (table.GetItemRect(1).Bottom > table.ClientSize.Height) throw new Exception("The table clips a saved entry.");
                    table.Items[0].Selected = table.Items[1].Selected = true; Application.DoEvents();
                    if (Controls(hub).OfType<Button>().Single(b => b.Visible && b.Text == "Edit").Enabled) throw new Exception("Bulk selection can accidentally edit one row.");
                    table.Items[0].Selected = false; table.Items[1].Selected = true; Application.DoEvents();
                    if (table.SelectedItems.Count != 1 || ((PersonalRule)table.SelectedItems[0].Tag).Trigger != extra.Trigger || !Controls(hub).OfType<Button>().Single(b => b.Visible && b.Text == "Edit").Enabled) throw new Exception("Selecting another row did not update the edit target.");
                    rules.Remove(extra); ((Action)Field(hub, "refreshLibrary"))();
                    var search = Controls(hub).OfType<TextBox>().Single(c => c.AccessibleName == "Search " + page);
                    var historySearchFrame = (Panel)Field(hub.Notes, "browserSearchFrame");
                    if (!(search.Parent is SearchField) || search.Parent.Height != historySearchFrame.Height || String.IsNullOrEmpty(((ThemedTextBox)search).Cue)) throw new Exception("Library search does not match History search sizing or placeholder.");
                    search.Text = "no matching phrase"; if (table.Items.Count != 0) throw new Exception("Library search ignored query.");
                    search.Text = replacement; if (table.Items.Count != 1) throw new Exception("Library search ignored replacement.");
                    search.Clear(); Snapshot(hub, Path.Combine(root, page + "-page.png"));
                }
                if (applied == null || applied.Personalization.Count != 2) throw new Exception("Saved entries were not applied immediately.");
                var rejectedRules = ((Config)Field(hub, "source")).Copy().Personalization; rejectedRules.Add(new PersonalRule { Kind = "Word", Trigger = "failed save", Replacement = "must not activate" });
                hub.OnSaved = value => { throw new IOException("Simulated write failure"); };
                bool saveRejected = false; try { typeof(SettingsForm).GetMethod("SaveLibrary", Flags).Invoke(hub, new object[] { rejectedRules }); } catch (TargetInvocationException ex) { saveRejected = ex.InnerException is IOException; }
                if (!saveRejected || ((Config)Field(hub, "source")).Personalization.Count != 2 || ((System.Collections.Generic.List<PersonalRule>)Field(hub, "pendingRules")).Count != 2) throw new Exception("A failed entry save activated draft rules.");
                hub.OnSaved = value => applied = value;
                hub.ShowPage("Style");
                Controls(hub).OfType<Button>().Single(b => b.Visible && b.Text == "Chat").PerformClick();
                if (!Controls(hub).OfType<TextBox>().Single(t => t.AccessibleName == "Style example").Text.EndsWith("Monday")) throw new Exception("Style preview did not update.");
                hub.SavePreferences();
                if (applied == null || applied.Personalization.Count != 2 || applied.WritingStyle != "Chat" || WritingFormat.Compose(applied, "sue dear") != "Sudheer") throw new Exception("Personalization drafts were not applied.");
                Snapshot(hub, Path.Combine(root, "Style.png")); ThemeColors.Apply(hub, "Dark"); Snapshot(hub, Path.Combine(root, "Style-dark.png")); ThemeColors.Apply(hub, config.Theme);
                hub.UpdateTranscript("sue dear has a meeting", "Sudheer has a meeting.");
                if (((TextBox)Field(hub, "cleaned")).Text != "Sudheer has a meeting.") throw new Exception("Preview discarded the corrected final transcript.");
                hub.ShowPage("Notes"); hub.Notes.SelectNote(note.Id); Application.DoEvents();
                hub.Notes.ShowLibrary();
                var browser = (Panel)Field(hub.Notes, "notesBrowser");
                if (Controls(browser).OfType<Button>().Any(button => new[] { "Find & act", "New note", "New conversation", "Record meeting", "Write note" }.Contains(button.Text))) throw new Exception("History still contains removed toolbar actions.");
                Snapshot(hub, Path.Combine(root, "history-simplified.png"));
                var historySearch = (TextBox)Field(hub.Notes, "browserSearch");
                historySearch.Text = "Release"; historySearch.Focus(); Application.DoEvents();
                int textTop = historySearch.Top + historySearch.GetPositionFromCharIndex(0).Y;
                if (historySearch.Multiline || Math.Abs(textTop + historySearch.Font.Height / 2 - historySearch.Parent.ClientSize.Height / 2) > 3) throw new Exception("History search text is not vertically centered.");
                if (hub.Notes.VisibleNotes != 1) throw new Exception("Centered History search stopped filtering entries.");
                Snapshot(hub, Path.Combine(root, "history-search-typed.png")); historySearch.Clear();
                hub.Notes.NewWrittenNote(); Application.DoEvents();
                if (!((Control)Field(hub.Notes, "personalSheet")).Visible || !((TextBox)Field(hub.Notes, "written")).CanFocus) throw new Exception("Written note editor did not open.");
                hub.Notes.SelectNote(note.Id); Application.DoEvents();
                var playerSources = (ComboBox)Field(hub.Notes, "audioSource"); if (playerSources.Items.Count != 1 || Convert.ToString(playerSources.SelectedItem) != "Computer") throw new Exception("Playback offers a nonexistent source.");
                var tabs = (AppTabs)Field(hub.Notes, "tabs"); tabs.SelectedIndex = 1; Application.DoEvents(); var transcript = (RichTextBox)Field(hub.Notes, "transcript"); if (!transcript.ReadOnly || !transcript.Text.Contains("original words")) throw new Exception("Conversation is missing or editable."); Snapshot(hub, Path.Combine(root, "conversation.png"));
                var originalSize = hub.Size; hub.Size = hub.MinimumSize; Application.DoEvents();
                if (transcript.Height < 100 || transcript.Width < 200) throw new Exception("Conversation reading area is crowded at minimum size.");
                Snapshot(hub, Path.Combine(root, "conversation-minimum.png")); hub.Size = originalSize; Application.DoEvents();
                Dialog(hub, "export", root, () => Invoke(hub.Notes, "ShowExportDialog"), dialog => {
                    var all = Controls(dialog).ToArray(); var choices = all.OfType<ComboBox>().ToArray(); if (choices.Length != 1 || all.OfType<Button>().Any(button => button.Text.Contains("CSV") || button.Text.Contains("Word") || button.Text.Contains("Excel"))) throw new Exception("Editable document export remains available.");
                    var preview = all.OfType<RichTextBox>().Single(); if (!preview.ReadOnly || preview.Text.StartsWith("#") || preview.Text.Contains("Personal notes")) throw new Exception("Conversation preview has markup or leaked annotations.");
                    choices[0].SelectedIndex = 1; if (!preview.Text.Contains("Keep personal notes separate.") || preview.Text.Contains("original words")) throw new Exception("Personal-note export scope leaks conversation."); choices[0].SelectedIndex = 0;
                    var save = all.OfType<Button>().Single(button => button.Text == "Save PDF..."); if (save.Bottom > save.Parent.ClientSize.Height || preview.Height < 70) throw new Exception("Export controls are clipped.");
                });
                string ids = config.NoteMicrophoneId + "|" + config.NoteOutputId;
                Dialog(hub, "devices-cancel", root, () => Invoke(hub.Notes, "ShowDevices"), dialog => { });
                if (ids != config.NoteMicrophoneId + "|" + config.NoteOutputId || File.Exists(Config.FilePath)) throw new Exception("Closing devices committed choices.");
                Dialog(hub, "dictionary", root, () => Invoke(hub, "EditRules"), dialog => { var all = Controls(dialog).ToArray(); if (all.OfType<DataGridView>().Any() || !all.OfType<ListView>().Any() || !all.OfType<Button>().Any(button => button.Text == "Add entry")) throw new Exception("Dictionary still requires grid editing."); });
                Dialog(hub, "speaker-labels", root, () => Invoke(hub.Notes, "EditSpeakerLabels"), dialog => { if (!Controls(dialog).OfType<ListView>().Any(list => list.MultiSelect)) throw new Exception("Speaker editing cannot select statements."); });
                Dialog(hub, "storage", root, () => NotesStorageDialog.Show(hub, store, config), dialog => { var pages = Controls(dialog).OfType<AppTabs>().Single().TabPages.Cast<TabPage>().Select(page => page.Text).ToArray(); if (!pages.SequenceEqual(new[] { "Storage", "Backups", "Recently deleted", "Retention", "Optional features" })) throw new Exception("Storage operations are not separated."); });
                hub.ShowPage("Preferences"); hub.SelectSettingsCategory("About"); if (Controls(hub).OfType<Label>().Any(label => label.Visible && (label.Text.Contains("thumbprint") || label.Text.Contains("manifest URL")))) throw new Exception("Publisher setup is exposed to ordinary users."); Snapshot(hub, Path.Combine(root, "about.png"));
                if (NoteStore.Transcript(note) != original || note.Segments[0].SpeakerLabel != null) throw new Exception("Review dialogs changed original data."); hub.CloseToTray = false;
            }
            var workflowStore = new NoteStore(Path.Combine(root, "voice-workflows"));
            var voiceNote = workflowStore.Create("", "Microphone", "Alex"); voiceNote.Kind = "Note"; voiceNote.VoiceDraft = true; workflowStore.Save(voiceNote);
            var conversationNote = workflowStore.Create("", "Microphone", "Alex"); conversationNote.Kind = "Conversation"; workflowStore.Save(conversationNote);
            if (ActivityHistory.Kind(conversationNote) != "Conversation" || NoteStore.Speaker(conversationNote, new NoteSegment { Source = "Microphone" }) != "Unassigned speaker") throw new Exception("Conversation was classified as a personal note or assigned a speaker without voice evidence.");
            using (var workflowHub = new SettingsForm(new Config(), "", "", workflowStore))
            {
                workflowHub.Show(); workflowHub.ShowPage("Notes"); workflowHub.Notes.SelectNote(voiceNote.Id); Application.DoEvents();
                var live = workflowHub.Notes.Document;
                var body = (TextBox)Field(workflowHub.Notes, "written");
                live.Segments.Add(new NoteSegment { Source = "Microphone", StartFrame = 0, Frames = 16000, RawText = "First spoken sentence.", Text = "First spoken sentence.", Complete = true });
                Invoke(workflowHub.Notes, "SessionChanged"); Application.DoEvents();
                if (body.Text != "First spoken sentence." || !live.VoiceDraft || workflowStore.List().Single(n => n.Id == voiceNote.Id).WrittenNotes != body.Text) throw new Exception("Voice note did not save recognized words into its note body.");
                live.Segments.Add(new NoteSegment { Source = "Microphone", StartFrame = 16000, Frames = 16000, RawText = "Second spoken sentence.", Text = "Second spoken sentence.", Complete = true });
                Invoke(workflowHub.Notes, "SessionChanged"); Application.DoEvents();
                if (!body.Text.Contains("Second spoken sentence.")) throw new Exception("Voice note did not append later recognized speech.");
                body.Text = "Manual correction";
                if (!live.EditableTextInitialized || live.VoiceDraft) throw new Exception("An edited voice note was not protected from later automatic updates.");
                live.Segments.Add(new NoteSegment { Source = "Microphone", StartFrame = 32000, Frames = 16000, RawText = "Later spoken sentence.", Text = "Later spoken sentence.", Complete = true });
                Invoke(workflowHub.Notes, "SessionChanged"); Application.DoEvents();
                if (body.Text != "Manual correction") throw new Exception("Later speech overwrote a user's edited note.");
                workflowHub.CloseToTray = false; workflowHub.Close();
            }
            var file = new FileInfo(Config.FilePath); if (file.Exists) throw new Exception("Read-only dialog checks wrote preferences.");
            Console.Write("ux-ok|pdf-only|readable-preview|scope|devices-cancel|dictionary|speaker-labels|storage|consumer-updates|originals-preserved");
        }
    }
}
