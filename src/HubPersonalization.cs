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
            Heading(page, words ? "Dictionary" : "Snippets", words ? "Names and spellings that sound like you." : "Say a short phrase. Write the whole thing.");
            var card = Card(page, 108, 480);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 5 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var search = new TextBox { Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 12), AccessibleName = "Search " + page.Name };
            var count = new Label { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 12, 0, 12), TextAlign = ContentAlignment.MiddleLeft };
            var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false, BorderStyle = BorderStyle.None, AccessibleName = page.Name + " entries", ShowItemToolTips = true };
            list.Columns.Add(words ? "Your pronunciation" : "What you say", 220); list.Columns.Add(words ? "Correct spelling" : "What to write", 400);
            list.Resize += (s, e) => { list.Columns[0].Width = Math.Max(100, list.ClientSize.Width / 3); list.Columns[1].Width = Math.Max(100, list.ClientSize.Width - list.Columns[0].Width - 8); };
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            var hint = new Label { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 8, 0, 0), Text = words ? "Record your pronunciation, add the correct spelling, then Save preferences. Double-click a word to listen or edit." : "Changes are drafts until you click Save preferences. Search by phrase or replacement.", AutoEllipsis = true };
            Action refresh = () => {
                var selected = list.SelectedItems.Count == 0 ? null : list.SelectedItems[0].Tag;
                list.BeginUpdate(); list.Items.Clear();
                var entries = pendingRules.Where(r => r.Kind == kind).ToArray();
                foreach (var rule in entries.Where(r => (r.Trigger + " " + r.Replacement).IndexOf(search.Text, StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(r => r.Trigger, StringComparer.CurrentCultureIgnoreCase))
                {
                    var item = new ListViewItem(words ? (String.IsNullOrEmpty(rule.PronunciationId) ? "Record pronunciation" : "Voice recording") : rule.Trigger) { Tag = rule, ToolTipText = rule.Replacement };
                    item.SubItems.Add(rule.Replacement.Replace("\r", "").Replace("\n", " / ")); list.Items.Add(item); item.Selected = rule == selected;
                }
                list.EndUpdate(); count.Text = entries.Length == 0 ? (words ? "No saved words yet. Record a word and add its correct spelling." : "No snippets yet. Try a signature, address or reply.") : list.Items.Count == 0 ? "No matches. Clear your search to see all entries." : list.Items.Count + " of " + entries.Length + " personal entries";
            };
            Action<PersonalRule> commit = entry => {
                try {
                    var old = list.SelectedItems.Count == 0 ? null : list.SelectedItems[0].Tag;
                    var proposed = pendingRules.Where(r => r != old).Concat(new[] { entry }).ToList();
                    PersonalRule.Validate(proposed); pendingRules = proposed; MarkDirty(); RefreshCleaned(); refreshLibrary();
                } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Check this entry"); }
            };
            Action edit = () => {
                if (list.SelectedItems.Count == 0) return;
                var entry = PersonalRule.Prompt(this, (PersonalRule)list.SelectedItems[0].Tag, source.Theme, source);
                if (entry != null) commit(entry);
            };
            actions.Controls.Add(DialogUi.Button(words ? "Add word" : "Add snippet", (s, e) => {
                list.SelectedItems.Clear();
                var entry = PersonalRule.Prompt(this, new PersonalRule { Kind = kind, Trigger = "", Replacement = "" }, source.Theme, source);
                if (entry != null) commit(entry);
            }, true));
            var editButton = DialogUi.Button("Edit", (s, e) => edit());
            var remove = DialogUi.Button("Remove", (s, e) => {
                if (list.SelectedItems.Count == 0) return;
                if (MessageBox.Show(this, "Remove this entry from your draft?", "Remove entry", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                pendingRules.Remove((PersonalRule)list.SelectedItems[0].Tag); MarkDirty(); RefreshCleaned(); refreshLibrary();
            });
            actions.Controls.Add(editButton); actions.Controls.Add(remove);
            actions.Controls.Add(DialogUi.Button("Import / export", (s, e) => { EditRules(); refreshLibrary(); }));
            editButton.Enabled = remove.Enabled = false;
            list.SelectedIndexChanged += (s, e) => editButton.Enabled = remove.Enabled = list.SelectedItems.Count > 0;
            list.DoubleClick += (s, e) => edit(); search.TextChanged += (s, e) => refresh();
            var searchGroup = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 2 };
            searchGroup.Controls.Add(new Label { Text = "Search " + page.Name.ToLowerInvariant(), AutoSize = true, Margin = new Padding(0, 0, 0, 8) }, 0, 0); searchGroup.Controls.Add(search, 0, 1);
            searchGroup.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.Controls.Add(searchGroup, 0, 0); layout.Controls.Add(count, 0, 1); layout.Controls.Add(list, 0, 2); layout.Controls.Add(actions, 0, 3); layout.Controls.Add(hint, 0, 4);
            card.Controls.Add(layout); refreshLibrary += refresh; refresh();
        }

        void BuildStylePage()
        {
            var page = pages[7]; Heading(page, "Style", "Choose how your voice becomes writing.");
            var card = Card(page, 108, 440);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 4 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var choices = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            var description = new Label { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 12, 0, 24) };
            var preview = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, AccessibleName = "Style example" };
            Action update = () => {
                foreach (HubButton button in choices.Controls) { button.Selected = button.Text == writingStyle.Text; button.Invalidate(); }
                description.Text = writingStyle.Text == "Chat" ? "Chat\r\nRelaxed, short messages with lighter punctuation." : writingStyle.Text == "Email" ? "Email\r\nA greeting and breathing room for longer messages." : writingStyle.Text == "Code" ? "Code\r\nConservative formatting. Cleanup is bypassed; your saved phrases still apply." : "Standard\r\nClear sentences with capitalization and punctuation.";
                var draft = source.Copy(); draft.CleanupEnabled = cleanup.Checked; draft.WritingStyle = writingStyle.Text; draft.Personalization = pendingRules;
                preview.Text = "EXAMPLE\r\n\r\n" + WritingFormat.Compose(draft, "hello team please send the draft on monday");
            };
            foreach (var style in new[] { "Standard", "Chat", "Email", "Code" }) { string value = style; var button = new HubButton { Text = value, Width = 130, Height = 42, Margin = new Padding(0, 0, 12, 8) }; button.Click += (s, e) => writingStyle.SelectedItem = value; choices.Controls.Add(button); }
            var hint = new Label { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 16, 0, 0), Text = "Applies to all dictation after saving. Original meeting transcripts stay unchanged." };
            layout.Controls.Add(choices, 0, 0); layout.Controls.Add(description, 0, 1); layout.Controls.Add(preview, 0, 2); layout.Controls.Add(hint, 0, 3); card.Controls.Add(layout);
            writingStyle.SelectedIndexChanged += (s, e) => update(); cleanup.CheckedChanged += (s, e) => update(); refreshLibrary += update; update();
        }
    }
}
