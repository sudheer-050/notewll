using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Drawing;

namespace LocalDictation
{
    sealed class PersonalRule
    {
        public string Kind { get; set; }
        public string Trigger { get; set; }
        public string Replacement { get; set; }
        public string PronunciationId { get; set; }
        [System.Web.Script.Serialization.ScriptIgnore] public byte[] PronunciationPcm { get; set; }
        public PersonalRule Copy() { return new PersonalRule { Kind = Kind, Trigger = Trigger, Replacement = Replacement, PronunciationId = PronunciationId, PronunciationPcm = PronunciationPcm == null ? null : (byte[])PronunciationPcm.Clone() }; }
        public static void Validate(IEnumerable<PersonalRule> rules)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in rules)
            {
                if (rule == null || (rule.Kind != "Word" && rule.Kind != "Snippet") || String.IsNullOrWhiteSpace(rule.Trigger) || String.IsNullOrWhiteSpace(rule.Replacement)) throw new ArgumentException("Each entry needs a Word or Snippet type, a trigger and replacement.");
                Guid pronunciation;
                if (!String.IsNullOrEmpty(rule.PronunciationId) && (rule.Kind != "Word" || !Guid.TryParseExact(rule.PronunciationId, "N", out pronunciation))) throw new ArgumentException("Invalid pronunciation identifier.");
                if (rule.PronunciationPcm != null) { if (String.IsNullOrEmpty(rule.PronunciationId)) throw new ArgumentException("A pronunciation needs an identifier."); Pronunciations.Validate(rule.PronunciationPcm); }
                rule.Trigger = rule.Trigger.Trim();
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
            foreach (var rule in config.Personalization.Where(r => !wordsOnly || r.Kind == "Word").OrderByDescending(r => r.Trigger.Length))
                text = Regex.Replace(text, @"(?<![\p{L}\p{N}])" + Regex.Escape(rule.Trigger) + @"(?![\p{L}\p{N}])", m => rule.Replacement, RegexOptions.IgnoreCase);
            return text;
        }
        internal static PersonalRule Prompt(IWin32Window owner, PersonalRule initial, string theme, Config speechConfig = null)
        {
            if (initial == null || initial.Kind == "Word") return VoiceWordDialog.Prompt(owner, speechConfig ?? Config.Load(), initial);
            using (var dialog = DialogUi.Create(initial == null ? "Add a word or phrase" : "Edit saved phrase", new Size(620, 490)))
            {
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7 };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                for (int i = 0; i < 7; i++) layout.RowStyles.Add(new RowStyle(i == 4 ? SizeType.Percent : SizeType.AutoSize, i == 4 ? 100 : 0));
                var kind = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList }; kind.Items.AddRange(new object[] { "Snippet" }); kind.SelectedItem = initial == null ? "Word" : initial.Kind;
                var trigger = new TextBox { Dock = DockStyle.Top, MaxLength = 120, AccessibleName = "What you say" }; trigger.Text = initial == null ? "" : initial.Trigger;
                var replacement = new TextBox { Dock = DockStyle.Fill, Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical, MaxLength = 8000, AccessibleName = "What to write" }; replacement.Text = initial == null ? "" : DialogUi.Lines(initial.Replacement);
                layout.Controls.Add(kind, 0, 0); layout.Controls.Add(DialogUi.Text("What you say - for example, my address"), 0, 1); layout.Controls.Add(trigger, 0, 2); layout.Controls.Add(DialogUi.Text("What to write - the correct spelling or expanded text"), 0, 3); layout.Controls.Add(replacement, 0, 4);
                layout.Controls.Add(DialogUi.Text("Word entries also guide enhanced speech recognition. Snippets expand only in dictation."), 0, 5);
                PersonalRule result = null; var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
                actions.Controls.Add(DialogUi.Button("Use entry", (s, e) => { try { var entry = new PersonalRule { Kind = kind.Text, Trigger = trigger.Text.Trim(), Replacement = replacement.Text }; Validate(new[] { entry }); result = entry; dialog.Close(); } catch (Exception ex) { MessageBox.Show(dialog, ex.Message, "Check this entry"); } }, true)); actions.Controls.Add(DialogUi.Button("Cancel", (s, e) => dialog.Close())); layout.Controls.Add(actions, 0, 6);
                dialog.Controls.Add(layout); ThemeColors.Apply(dialog, theme); dialog.ShowDialog(owner); return result;
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
                var search = new TextBox { Dock = DockStyle.Top, AccessibleName = "Search saved words and snippets" }; layout.Controls.Add(search, 0, 1);
                var filter = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList }; filter.Items.AddRange(new object[] { "All entries", "Words", "Snippets" }); filter.SelectedIndex = 0; layout.Controls.Add(filter, 0, 2);
                var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false, AccessibleName = "Saved words and snippets" }; list.Columns.Add("What you say", 230); list.Columns.Add("What to write", 330); list.Columns.Add("Applies to", 190); layout.Controls.Add(list, 0, 3);
                Action refresh = () => { list.Items.Clear(); foreach (var rule in rules.Where(r => (filter.SelectedIndex == 0 || r.Kind == (filter.SelectedIndex == 1 ? "Word" : "Snippet")) && (r.Trigger + " " + r.Replacement).IndexOf(search.Text, StringComparison.OrdinalIgnoreCase) >= 0)) { var item = new ListViewItem(rule.Trigger) { Tag = rule, ToolTipText = rule.Replacement }; item.SubItems.Add(rule.Replacement.Replace("\r", "").Replace("\n", " / ")); item.SubItems.Add(rule.Kind == "Word" ? "Dictation + speech hints" : "Dictation only"); list.Items.Add(item); } };
                list.ShowItemToolTips = true; search.TextChanged += (s, e) => refresh(); filter.SelectedIndexChanged += (s, e) => refresh();
                Action edit = () => { if (list.SelectedItems.Count == 0) return; var old = (PersonalRule)list.SelectedItems[0].Tag; var entry = Prompt(dialog, old, config.Theme, config); if (entry == null) return; var proposed = rules.Where(r => r != old).Concat(new[] { entry }).ToList(); try { Validate(proposed); rules = proposed; refresh(); } catch (Exception ex) { MessageBox.Show(dialog, ex.Message); } };
                list.DoubleClick += (s, e) => edit(); var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
                buttons.Controls.Add(DialogUi.Button("Add entry", (s, e) => { var entry = Prompt(dialog, new PersonalRule { Kind = filter.SelectedIndex == 2 ? "Snippet" : "Word", Trigger = "", Replacement = "" }, config.Theme, config); if (entry == null) return; try { var proposed = rules.Concat(new[] { entry }).ToList(); Validate(proposed); rules = proposed; refresh(); } catch (Exception ex) { MessageBox.Show(dialog, ex.Message); } }));
                buttons.Controls.Add(DialogUi.Button("Edit", (s, e) => edit())); buttons.Controls.Add(DialogUi.Button("Remove", (s, e) => { if (list.SelectedItems.Count > 0 && MessageBox.Show(dialog, "Remove this saved entry?", "Remove entry", MessageBoxButtons.YesNo) == DialogResult.Yes) { rules.Remove((PersonalRule)list.SelectedItems[0].Tag); refresh(); } }));
                buttons.Controls.Add(DialogUi.Button("More...", (s, e) => { var menu = DialogUi.PopupMenu(); menu.Items.Add("Import entries (JSON)...", null, (a, b) => { using (var file = new OpenFileDialog { Filter = "Saved entries|*.json" }) if (file.ShowDialog(dialog) == DialogResult.OK) try { var proposed = rules.Concat(NotesData.ReadRules(file.FileName)).ToList(); Validate(proposed); rules = proposed; refresh(); } catch (Exception ex) { MessageBox.Show(dialog, ex.Message); } }); menu.Items.Add("Export entries (JSON)...", null, (a, b) => { using (var file = new SaveFileDialog { Filter = "Saved entries|*.json", DefaultExt = "json", AddExtension = true }) if (file.ShowDialog(dialog) == DialogResult.OK) try { NotesData.WriteRules(file.FileName, rules); } catch (Exception ex) { MessageBox.Show(dialog, ex.Message); } }); menu.Show((Control)s, new Point(0, ((Control)s).Height)); }));
                buttons.Controls.Add(DialogUi.Button("Use changes", (s, e) => { Validate(rules); config.Personalization = rules; if (changed != null) changed(); dialog.Close(); }, true)); buttons.Controls.Add(DialogUi.Button("Cancel", (s, e) => dialog.Close()));
                layout.Controls.Add(buttons, 0, 4); dialog.Controls.Add(layout); refresh(); ThemeColors.Apply(dialog, config.Theme); dialog.ShowDialog(owner);
            }
        }
    }
}
