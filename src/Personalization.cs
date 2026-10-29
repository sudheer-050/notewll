using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Drawing;

namespace Notewll
{
    sealed class PersonalRule
    {
        public string Kind { get; set; }
        public string Trigger { get; set; }
        public string Replacement { get; set; }
        public string ReplacementRtf { get; set; }
        public bool Starred { get; set; }
        public string CreatedUtc { get; set; }
        public string Origin { get; set; }
        public string PronunciationId { get; set; }
        [System.Web.Script.Serialization.ScriptIgnore] public byte[] PronunciationPcm { get; set; }
        public PersonalRule Copy() { return new PersonalRule { Kind = Kind, Trigger = Trigger, Replacement = Replacement, ReplacementRtf = ReplacementRtf, Starred = Starred, CreatedUtc = CreatedUtc, Origin = Origin, PronunciationId = PronunciationId, PronunciationPcm = PronunciationPcm == null ? null : (byte[])PronunciationPcm.Clone() }; }
        public static void Validate(IEnumerable<PersonalRule> rules)
        {
            var entries = rules.ToArray();
            if (entries.Length > 1000 || entries.Where(r => r != null).Sum(r => (long)(r.Trigger ?? "").Length + (r.Replacement ?? "").Length + (r.ReplacementRtf ?? "").Length) > 512 * 1024) throw new ArgumentException("Keep at most 1,000 entries and 512 KB of saved text and formatting.");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in entries)
            {
                if (rule == null || (rule.Kind != "Word" && rule.Kind != "Snippet") || String.IsNullOrWhiteSpace(rule.Trigger) || String.IsNullOrWhiteSpace(rule.Replacement)) throw new ArgumentException("Each entry needs a Word or Snippet type, a trigger and replacement.");
                Guid pronunciation;
                if (!String.IsNullOrEmpty(rule.PronunciationId) && (rule.Kind != "Word" || !Guid.TryParseExact(rule.PronunciationId, "N", out pronunciation))) throw new ArgumentException("Invalid pronunciation identifier.");
                if (rule.PronunciationPcm != null) { if (String.IsNullOrEmpty(rule.PronunciationId)) throw new ArgumentException("A pronunciation needs an identifier."); Pronunciations.Validate(rule.PronunciationPcm); }
                rule.Trigger = rule.Trigger.Trim();
                if (rule.Origin != null && rule.Origin.Length > 80) throw new ArgumentException("Entry source label is too long.");
                if (!String.IsNullOrEmpty(rule.CreatedUtc)) { DateTime created; if (!DateTime.TryParse(rule.CreatedUtc, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out created)) throw new ArgumentException("Entry creation date is invalid."); rule.CreatedUtc = created.ToUniversalTime().ToString("o"); }
                RichSnippets.Validate(rule);
                if (rule.Trigger.Length > 120 || rule.Replacement.Length > 8000 || !seen.Add(rule.Trigger)) throw new ArgumentException("Triggers must be unique and at most 120 characters; replacement limit is 8,000 characters.");
            }
        }
        public static string[] Vocabulary(Config config)
        {
            // ponytail: bounded saved spellings bias decoding; snippets never become speech hints.
            return config.Personalization.Where(r => r.Kind == "Word" && r.Replacement.Length <= 80 && !r.Replacement.Any(Char.IsControl))
                .Select(r => r.Replacement.Trim()).Where(v => v.Length > 0).Reverse().Distinct(StringComparer.OrdinalIgnoreCase).Take(24).Reverse().ToArray();
        }
        public static string Apply(Config config, string text, bool wordsOnly = false)
        {
            if (String.IsNullOrEmpty(text)) return text ?? "";
            var rules = config.Personalization.Where(r => !wordsOnly || r.Kind == "Word").OrderByDescending(r => r.Trigger.Length).ToArray();
            if (rules.Length == 0) return text;
            var replacements = rules.ToDictionary(r => r.Trigger, r => r.Replacement, StringComparer.OrdinalIgnoreCase);
            // Match the original text once: expansion text must never trigger another saved rule.
            string pattern = @"(?<![\p{L}\p{N}_])(?:" + String.Join("|", rules.Select(r => Regex.Escape(r.Trigger))) + @")(?![\p{L}\p{N}_])";
            return Regex.Replace(text, pattern, m => replacements[m.Value], RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        }
        internal static PersonalRule Prompt(IWin32Window owner, PersonalRule initial, string theme, Config speechConfig = null)
        {
            if (initial == null || initial.Kind == "Word") return VoiceWordDialog.Prompt(owner, speechConfig ?? Config.Load(), initial);
            using (var dialog = DialogUi.Create(initial == null ? "Add a word or phrase" : "Edit saved phrase", new Size(620, 490)))
            {
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7 };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                for (int i = 0; i < 7; i++) layout.RowStyles.Add(new RowStyle(i == 4 ? SizeType.Percent : SizeType.AutoSize, i == 4 ? 100 : 0));
                var trigger = new ThemedTextBox { Dock = DockStyle.Top, MaxLength = 120, AccessibleName = "What you say" }; trigger.Text = initial == null ? "" : initial.Trigger;
                var replacement = new RichTextBox { Dock = DockStyle.Fill, MaxLength = 8000, DetectUrls = true, AccessibleName = "What to write" }; if (String.IsNullOrEmpty(initial.ReplacementRtf)) replacement.Text = DialogUi.Lines(initial.Replacement); else replacement.Rtf = initial.ReplacementRtf;
                layout.Controls.Add(DialogUi.Text("Snippet: say a short phrase to insert the text below."), 0, 0); layout.Controls.Add(DialogUi.Text("What you say - for example, my address"), 0, 1); layout.Controls.Add(trigger, 0, 2); layout.Controls.Add(DialogUi.Text("What to write"), 0, 3); layout.Controls.Add(RichSnippets.Editor(replacement), 0, 4);
                layout.Controls.Add(DialogUi.Text("Formatting is preserved for exact snippet phrases when formatted paste is enabled on Style. Other dictation uses plain text."), 0, 5);
                PersonalRule result = null; var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
                var save = DialogUi.Button("Save snippet", (s, e) => { try { var entry = initial.Copy(); entry.Trigger = trigger.Text.Trim(); entry.Replacement = replacement.Text; entry.ReplacementRtf = replacement.Rtf; Validate(new[] { entry }); result = entry; dialog.Close(); } catch (Exception ex) { MessageBox.Show(dialog, ex.Message, "Check this entry"); } }, true); actions.Controls.Add(save);
                KeyEventHandler shortcut = (s, e) => { if (e.Control && e.KeyCode == Keys.Enter) { save.PerformClick(); e.SuppressKeyPress = true; } }; trigger.KeyDown += shortcut; replacement.KeyDown += shortcut;
                actions.Controls.Add(DialogUi.Button("Cancel", (s, e) => dialog.Close())); layout.Controls.Add(actions, 0, 6);
                dialog.Controls.Add(layout); ThemeColors.Apply(dialog, theme); dialog.ShowDialog(owner); return result;
            }
        }
        internal static PersonalRule PromptText(IWin32Window owner, PersonalRule initial, string theme, Config config)
        {
            if (initial.Kind != "Word") return Prompt(owner, initial, theme, config);
            using (var dialog = DialogUi.Create("Dictionary word", new Size(580, 340)))
            {
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                var trigger = new ThemedTextBox { Height = 36, Dock = DockStyle.Top, Text = initial.Trigger, MaxLength = 120, AccessibleName = "What you say" };
                var replacement = new ThemedTextBox { Height = 36, Dock = DockStyle.Top, Text = initial.Replacement, MaxLength = 80, AccessibleName = "Correct spelling" };
                layout.Controls.Add(DialogUi.Text("Recognized word or phrase"), 0, 0); layout.Controls.Add(trigger, 0, 1);
                layout.Controls.Add(DialogUi.Text("Correct spelling"), 0, 2); layout.Controls.Add(replacement, 0, 3);
                PersonalRule result = null;
                var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
                var save = DialogUi.Button("Save word", (s, e) => { try { var value = initial.Copy(); value.Trigger = trigger.Text.Trim(); value.Replacement = replacement.Text.Trim(); Validate(new[] { value }); result = value; dialog.Close(); } catch (Exception ex) { MessageBox.Show(dialog, ex.Message, "Check word"); } }, true);
                actions.Controls.Add(save);
                actions.Controls.Add(DialogUi.Button("Record pronunciation...", (s, e) => { var value = VoiceWordDialog.Prompt(dialog, config, initial); if (value != null) { value.Starred = initial.Starred; value.CreatedUtc = initial.CreatedUtc; value.Origin = "Pronunciation"; result = value; dialog.Close(); } }));
                actions.Controls.Add(DialogUi.Button("Cancel", (s, e) => dialog.Close())); layout.Controls.Add(actions, 0, 4);
                dialog.AcceptButton = save; dialog.Controls.Add(layout); ThemeColors.Apply(dialog, theme); dialog.ShowDialog(owner); return result;
            }
        }
        public static void Edit(IWin32Window owner, Config config, Action changed)
        {
            var rules = config.Personalization.Select(rule => rule.Copy()).ToList();
            using (var dialog = DialogUi.Create("Words & snippets", new Size(800, 620)))
            {
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 }; layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                for (int i = 0; i < 5; i++) layout.RowStyles.Add(new RowStyle(i == 3 ? SizeType.Percent : SizeType.AutoSize, i == 3 ? 100 : 0));
                layout.Controls.Add(DialogUi.Text("Confirmed words guide speech recognition and replace whole phrases in dictation. Snippets expand in dictation only. Remove a rule to undo future use; original transcripts stay unchanged. Changes activate when you save Preferences."), 0, 0);
                var search = new ThemedTextBox { Dock = DockStyle.Top, AccessibleName = "Search saved words and snippets" }; layout.Controls.Add(search, 0, 1);
                var filter = new ThemedComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList }; filter.Items.AddRange(new object[] { "All entries", "Words", "Snippets" }); filter.SelectedIndex = 0; layout.Controls.Add(filter, 0, 2);
                var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false, AccessibleName = "Saved words and snippets" }; list.Columns.Add("What you say", 230); list.Columns.Add("What to write", 330); list.Columns.Add("Applies to", 190); layout.Controls.Add(list, 0, 3);
                Action refresh = () => { list.Items.Clear(); foreach (var rule in rules.Where(r => (filter.SelectedIndex == 0 || r.Kind == (filter.SelectedIndex == 1 ? "Word" : "Snippet")) && (r.Trigger + " " + r.Replacement).IndexOf(search.Text, StringComparison.OrdinalIgnoreCase) >= 0)) { var item = new ListViewItem(rule.Trigger) { Tag = rule, ToolTipText = rule.Replacement }; item.SubItems.Add(rule.Replacement.Replace("\r", "").Replace("\n", " / ")); item.SubItems.Add(rule.Kind == "Word" ? "Dictation + speech hints" : "Dictation only"); list.Items.Add(item); } };
                list.ShowItemToolTips = true; search.TextChanged += (s, e) => refresh(); filter.SelectedIndexChanged += (s, e) => refresh();
                Action edit = () => { if (list.SelectedItems.Count == 0) return; var old = (PersonalRule)list.SelectedItems[0].Tag; var entry = Prompt(dialog, old, config.Theme, config); if (entry == null) return; var proposed = rules.Where(r => r != old).Concat(new[] { entry }).ToList(); try { Validate(proposed); rules = proposed; refresh(); } catch (Exception ex) { MessageBox.Show(dialog, ex.Message); } };
                list.DoubleClick += (s, e) => edit(); var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 6, RowCount = 1 };
                for (int i = 0; i < 6; i++) buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 6));
                buttons.Controls.Add(DialogUi.Button("Add entry", (s, e) => { var entry = Prompt(dialog, new PersonalRule { Kind = filter.SelectedIndex == 2 ? "Snippet" : "Word", Trigger = "", Replacement = "" }, config.Theme, config); if (entry == null) return; try { var proposed = rules.Concat(new[] { entry }).ToList(); Validate(proposed); rules = proposed; refresh(); } catch (Exception ex) { MessageBox.Show(dialog, ex.Message); } }));
                buttons.Controls.Add(DialogUi.Button("Edit", (s, e) => edit())); buttons.Controls.Add(DialogUi.Button("Remove", (s, e) => { if (list.SelectedItems.Count > 0 && MessageBox.Show(dialog, "Remove this saved entry?", "Remove entry", MessageBoxButtons.YesNo) == DialogResult.Yes) { rules.Remove((PersonalRule)list.SelectedItems[0].Tag); refresh(); } }));
                buttons.Controls.Add(DialogUi.Button("More...", (s, e) => { var menu = DialogUi.PopupMenu(); menu.Items.Add("Import entries (JSON)...", null, (a, b) => { using (var file = new OpenFileDialog { Filter = "Saved entries|*.json" }) if (file.ShowDialog(dialog) == DialogResult.OK) try { var proposed = rules.Concat(NotesData.ReadRules(file.FileName)).ToList(); Validate(proposed); rules = proposed; refresh(); } catch (Exception ex) { MessageBox.Show(dialog, ex.Message); } }); menu.Items.Add("Export entries (JSON)...", null, (a, b) => { using (var file = new SaveFileDialog { Filter = "Saved entries|*.json", DefaultExt = "json", AddExtension = true }) if (file.ShowDialog(dialog) == DialogResult.OK) try { NotesData.WriteRules(file.FileName, rules); } catch (Exception ex) { MessageBox.Show(dialog, ex.Message); } }); menu.Show((Control)s, new Point(0, ((Control)s).Height)); }));
                buttons.Controls.Add(DialogUi.Button("Use changes", (s, e) => { Validate(rules); config.Personalization = rules; if (changed != null) changed(); dialog.Close(); }, true)); buttons.Controls.Add(DialogUi.Button("Cancel", (s, e) => dialog.Close()));
                foreach (Control button in buttons.Controls) { button.Dock = DockStyle.Fill; button.AutoSize = false; button.MinimumSize = Size.Empty; button.Margin = new Padding(4); }
                layout.Controls.Add(buttons, 0, 4); dialog.Controls.Add(layout); refresh(); ThemeColors.Apply(dialog, config.Theme); dialog.ShowDialog(owner);
            }
        }
    }
}
