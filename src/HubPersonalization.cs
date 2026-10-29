using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Notewll
{
    sealed partial class SettingsForm
    {
        Action refreshLibrary;

        void BuildLibraryPage(int index, string kind)
        {
            bool words = kind == "Word";
            var page = pages[index];
            Heading(page, words ? "Dictionary" : "Snippets", words ? "Fix a word or name that speech recognition mishears." : "Expand a short spoken phrase into longer text.");
            var card = Card(page, 108, 420);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 4 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var search = new ThemedTextBox { AccessibleName = "Search " + page.Name };
            var searchField = new SearchField(search, words ? "Search dictionary..." : "Search snippets...") { Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 12) };
            var joined = new Surface { Dock = DockStyle.Fill, Padding = new Padding(1), Margin = Padding.Empty };
            joined.SizeChanged += (s, e) => { if (joined.Width < 2 || joined.Height < 2) return; using (var shape = Surface.Shape(new RectangleF(0, 0, joined.Width, joined.Height), 18)) { var old = joined.Region; joined.Region = new Region(shape); if (old != null) old.Dispose(); } };
            var table = new LibraryTable { Name = "PersonalizationTable", Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = true, ShowItemToolTips = true, HideSelection = false, Margin = Padding.Empty, AccessibleName = words ? "Saved dictionary words" : "Saved snippets" };
            table.Columns.Add("When you say", 300, HorizontalAlignment.Right); table.Columns.Add("Notewll writes", 300, HorizontalAlignment.Left);
            PersonalRule selectedRule = null;
            Action refresh = null;
            bool refreshing = false;
            HubButton editButton = null, remove = null;
            Action select = () => {
                selectedRule = table.SelectedItems.Count != 1 ? null : table.SelectedItems[0].Tag as PersonalRule;
                if (editButton != null) { editButton.Enabled = selectedRule != null; remove.Enabled = table.SelectedItems.Count > 0; }
            };
            table.SelectedIndexChanged += (s, e) => { if (!refreshing) select(); };
            var actions = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 4, RowCount = 1 };
            for (int i = 0; i < 4; i++) actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            var hint = new Label { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 8, 0, 0), Text = "Select a row to edit or remove it. Entries save immediately. Ctrl+F searches; double-click edits.", AutoEllipsis = true };
            refresh = () => {
                var entries = pendingRules.Where(r => r.Kind == kind).ToArray();
                var matches = entries.Where(r => (r.Trigger + " " + r.Replacement).IndexOf(search.Text, StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(r => r.Trigger, StringComparer.CurrentCultureIgnoreCase).ToArray();
                var choice = matches.FirstOrDefault(r => selectedRule != null && r.Trigger == selectedRule.Trigger) ?? matches.FirstOrDefault();
                refreshing = true; table.BeginUpdate(); table.Items.Clear();
                foreach (var rule in matches) { var item = new ListViewItem(rule.Trigger) { Tag = rule, ToolTipText = (rule.Origin ?? "Manual") + (String.IsNullOrEmpty(rule.CreatedUtc) ? "" : " ? " + rule.CreatedUtc) }; item.SubItems.Add(rule.Replacement.Replace('\r', ' ').Replace('\n', ' ')); table.Items.Add(item); if (rule == choice) item.Selected = true; }
                table.EndUpdate();
                refreshing = false;
                select();
            };
            Action<PersonalRule> commit = entry => {
                try {
                    var proposed = pendingRules.Where(r => r != selectedRule).Concat(new[] { entry }).ToList();
                    entry.CreatedUtc = entry.CreatedUtc ?? (selectedRule == null ? DateTime.UtcNow.ToString("o") : selectedRule.CreatedUtc) ?? DateTime.UtcNow.ToString("o");
                    entry.Origin = entry.Origin ?? (entry.PronunciationId == null ? "Manual" : "Pronunciation");
                    PersonalRule.Validate(proposed); var previous = selectedRule; selectedRule = entry;
                    try { SaveLibrary(proposed); } catch { selectedRule = previous; throw; }
                } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Check this entry"); }
            };
            Action edit = () => {
                if (selectedRule == null) return;
                var entry = PersonalRule.PromptText(this, selectedRule, source.Theme, source);
                if (entry != null) commit(entry);
            };
            table.DoubleClick += (s, e) => edit();
            actions.Controls.Add(DialogUi.Button(words ? "Add word" : "Add snippet", (s, e) => {
                var previous = selectedRule; selectedRule = null;
                var entry = PersonalRule.PromptText(this, new PersonalRule { Kind = kind, Trigger = "", Replacement = "" }, source.Theme, source);
                if (entry != null) commit(entry); else selectedRule = previous;
            }, true));
            editButton = DialogUi.Button("Edit", (s, e) => edit());
            remove = DialogUi.Button("Remove", (s, e) => {
                var selected = table.SelectedItems.Cast<ListViewItem>().Select(i => (PersonalRule)i.Tag).ToArray();
                if (selected.Length == 0 || MessageBox.Show(this, "Remove " + selected.Length + " saved entry/entries?", "Remove entries", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                try { SaveLibrary(pendingRules.Where(r => !selected.Contains(r)).ToList()); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Entries not removed"); }
            });
            actions.Controls.Add(editButton); actions.Controls.Add(remove);
            actions.Controls.Add(DialogUi.Button("Import / export", (s, e) => { LibraryFiles((Control)s, kind, table.SelectedItems.Cast<ListViewItem>().Select(i => (PersonalRule)i.Tag)); }));
            foreach (Control button in actions.Controls) { button.Dock = DockStyle.Fill; button.AutoSize = false; button.MinimumSize = Size.Empty; button.Margin = new Padding(4); }
            editButton.Enabled = remove.Enabled = false;
            search.TextChanged += (s, e) => refresh();
            table.KeyDown += (s, e) => { if (e.Control && e.KeyCode == Keys.F) { search.Focus(); e.SuppressKeyPress = true; } if (e.KeyCode == Keys.Enter) { edit(); e.SuppressKeyPress = true; } };
            joined.Controls.Add(table);
            layout.Controls.Add(searchField, 0, 0); layout.Controls.Add(joined, 0, 1); layout.Controls.Add(actions, 0, 2); layout.Controls.Add(hint, 0, 3);
            card.Controls.Add(layout); refreshLibrary += refresh; refresh();
        }

        void BuildStylePage()
        {
            var page = pages[7]; Heading(page, "Style", "Choose how your voice becomes writing.");
            var card = Card(page, 108, 440);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 6 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var choices = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            var description = new Label { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 12, 0, 24) };
            var preview = new ThemedTextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, AccessibleName = "Style example" };
            Action update = () => {
                foreach (HubButton button in choices.Controls) { button.Selected = button.Text == writingStyle.Text; button.Invalidate(); }
                description.Text = writingStyle.Text == "Chat" ? "Chat\r\nRelaxed, short messages with lighter punctuation." : writingStyle.Text == "Email" ? "Email\r\nA greeting and breathing room for longer messages." : writingStyle.Text == "Code" ? "Code\r\nConservative formatting. Cleanup is bypassed; your saved phrases still apply." : "Standard\r\nClear sentences with capitalization and punctuation.";
                var draft = source.Copy(); draft.CleanupEnabled = cleanup.Checked; draft.WritingStyle = writingStyle.Text; draft.CleanupLevel = correctionMode.SelectedIndex == 2 ? "Light" : "Standard"; draft.Personalization = pendingRules;
                preview.Text = "EXAMPLE\r\n\r\n" + WritingFormat.Compose(draft, "hello team please send the draft on monday");
            };
            foreach (var style in new[] { "Standard", "Chat", "Email", "Code" }) { string value = style; var button = new HubButton { Text = value, Width = 130, Height = 42, Margin = new Padding(0, 0, 12, 8) }; button.Click += (s, e) => writingStyle.SelectedItem = value; choices.Controls.Add(button); }
            var hint = new Label { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 16, 0, 0), Text = "Applies to all dictation after saving. Original meeting transcripts stay unchanged." };
            var cleanupRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, MinimumSize = new Size(0, 38) };
            cleanupRow.Controls.Add(new Label { Text = "Cleanup", AutoSize = true, Padding = new Padding(0, 6, 12, 0) }); correctionMode.Width = 260; cleanupRow.Controls.Add(correctionMode);
            layout.Controls.Add(cleanupRow, 0, 0); layout.Controls.Add(choices, 0, 1); layout.Controls.Add(description, 0, 2); layout.Controls.Add(preview, 0, 3); layout.Controls.Add(AppStyleControls(), 0, 4); layout.Controls.Add(hint, 0, 5); card.Controls.Add(layout);
            correctionMode.SelectedIndexChanged += (s, e) => update(); writingStyle.SelectedIndexChanged += (s, e) => update(); cleanup.CheckedChanged += (s, e) => update(); refreshLibrary += update; update();
        }
    }
}
