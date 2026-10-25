using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace Notewll
{
    static class HistoryCleanupDialog
    {
        static Label Text(Control parent, string text, int x, int y, int width, int height, float size = 10, bool bold = false)
        {
            var label = new Label { Text = text, Left = x, Top = y, Width = width, Height = height, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), BackColor = ThemeColors.Card, ForeColor = bold ? ThemeColors.Ink : ThemeColors.Muted, UseMnemonic = false };
            parent.Controls.Add(label); return label;
        }
        static Surface Card(Control parent, int top, int height)
        { var card = new Surface { Left = 24, Top = top, Width = 612, Height = height }; parent.Controls.Add(card); return card; }
        internal static List<NoteDocument> Choose(Form owner, List<NoteDocument> entries)
        {
            using (var dialog = DialogUi.Create("Clear history", new Size(660, 700)))
            {
                dialog.AutoScaleMode = AutoScaleMode.None; dialog.BackColor = ThemeColors.Paper;
                Text(dialog, "A fresh start.", 28, 18, 590, 42, 22, true).BackColor = ThemeColors.Paper;
                Text(dialog, "Choose what to clear. Keep everything else.", 30, 66, 590, 28).BackColor = ThemeColors.Paper;
                var time = Card(dialog, 108, 194); Text(time, "Time range", 18, 14, 570, 25, 11, true);
                string[] ranges = { "Last hour", "24 hours", "7 days", "4 weeks", "All time", "Custom" };
                int range = 2; bool reviewing = false;
                var times = ranges.Select((name, i) => new HubButton { Text = name, Left = 18 + i % 3 * 194, Top = 48 + i / 3 * 46, Width = 182, Height = 36, Selected = i == range, AccessibleName = "Time range: " + name }).ToArray();
                time.Controls.AddRange(times);
                var from = new DateTimePicker { Left = 18, Top = 145, Width = 280, Format = DateTimePickerFormat.Custom, CustomFormat = "MMM d yyyy, HH:mm", Value = DateTime.Now.AddDays(-7), AccessibleName = "From date and time", Visible = false };
                var to = new DateTimePicker { Left = 312, Top = 145, Width = 280, Format = DateTimePickerFormat.Custom, CustomFormat = "MMM d yyyy, HH:mm", Value = DateTime.Now, AccessibleName = "To date and time", Visible = false };
                time.Controls.AddRange(new Control[] { from, to });
                var timeHint = Text(time, "Only entries created within this period will be selected.", 18, 148, 570, 30, 9);
                var types = Card(dialog, 316, 104); Text(types, "Activity types", 18, 12, 570, 24, 11, true);
                string[] names = { "Dictations", "Notes", "Meetings" }, kinds = { "Dictation", "Note", "Meeting" };
                var choices = names.Select((name, i) => new HubButton { Left = 18 + i * 194, Top = 48, Width = 182, Height = 36, Text = name, Selected = i == 0, AccessibleName = "Include " + name }).ToArray(); types.Controls.AddRange(choices);
                var quantity = Card(dialog, 434, 92); Text(quantity, "How many?", 18, 12, 240, 25, 11, true);
                var all = new HubButton { Text = "All matching", Selected = true, Left = 18, Top = 44, Width = 170, Height = 34, AccessibleName = "All matching items" };
                var capped = new HubButton { Text = "Limit to newest", Left = 200, Top = 44, Width = 188, Height = 34, AccessibleName = "Limit number of items" };
                var limit = new NumericUpDown { Left = 406, Top = 46, Width = 180, Minimum = 1, Maximum = 1000000, Value = 100, Enabled = false, AccessibleName = "Maximum items" }; quantity.Controls.AddRange(new Control[] { all, capped, limit });
                var summary = Card(dialog, 540, 82); summary.Fill = ThemeColors.Selected;
                var count = Text(summary, "0", 18, 8, 100, 54, 28, true); count.AccessibleName = "Cleanup matching count";
                var message = Text(summary, "entries selected", 128, 14, 460, 28, 11, true);
                Text(summary, "Text and recordings move to Recently deleted for recovery.", 128, 44, 462, 27, 8);
                var back = new HubButton { Text = "Cancel", Left = 276, Top = 642, Width = 160, Height = 40 };
                var remove = new HubButton { Text = "Review deletion", Primary = true, Left = 450, Top = 642, Width = 186, Height = 40, AccessibleName = "Review deletion" }; dialog.Controls.AddRange(new Control[] { back, remove });
                List<NoteDocument> result = null;
                Func<List<NoteDocument>> selection = () => { var now = DateTime.Now; var since = range == 0 ? now.AddHours(-1) : range == 1 ? now.AddDays(-1) : range == 2 ? now.AddDays(-7) : range == 3 ? now.AddDays(-28) : range == 5 ? from.Value : DateTime.MinValue;
                    return ActivityHistory.Match(entries, kinds.Where((kind, i) => choices[i].Selected).ToArray(), since, range == 5 ? to.Value : now, all.Selected ? int.MaxValue : (int)limit.Value); };
                Action update = () => {
                    from.Visible = to.Visible = range == 5; timeHint.Visible = range != 5;
                    var selected = selection(); count.Text = selected.Count.ToString("N0"); message.Text = selected.Count == 1 ? "entry selected" : "entries selected";
                    if (range == 5 && from.Value > to.Value) message.Text = "Choose an end after the start";
                    remove.Enabled = selected.Count > 0;
                    foreach (var button in times.Concat(choices).Concat(new[] { all, capped })) { button.AccessibleDescription = button.Selected ? "Selected" : "Not selected"; button.Invalidate(); }
                };
                for (int i = 0; i < times.Length; i++) { int at = i; times[i].Click += (s, e) => { range = at; for (int j = 0; j < times.Length; j++) times[j].Selected = j == at; update(); }; }
                foreach (var button in choices) button.Click += (s, e) => { button.Selected = !button.Selected; update(); };
                all.Click += (s, e) => { all.Selected = true; capped.Selected = false; limit.Enabled = false; update(); };
                capped.Click += (s, e) => { all.Selected = false; capped.Selected = true; limit.Enabled = true; update(); };
                from.ValueChanged += (s, e) => update(); to.ValueChanged += (s, e) => update(); limit.ValueChanged += (s, e) => update();
                remove.Click += (s, e) => {
                    if (!reviewing) { result = selection(); reviewing = true; time.Enabled = types.Enabled = quantity.Enabled = false; back.Text = "Back"; remove.Text = "Confirm deletion"; message.Text = "Ready to clear " + result.Count + (result.Count == 1 ? " entry" : " entries"); remove.Invalidate(); }
                    else { dialog.DialogResult = DialogResult.OK; dialog.Close(); }
                };
                back.Click += (s, e) => { if (reviewing) { reviewing = false; result = null; time.Enabled = types.Enabled = quantity.Enabled = true; back.Text = "Cancel"; remove.Text = "Review deletion"; update(); } else dialog.Close(); };
                ThemeColors.Apply(dialog, "System"); summary.Fill = ThemeColors.Selected; foreach (Control child in summary.Controls) child.BackColor = ThemeColors.Selected;
                float scale; using (var g = dialog.CreateGraphics()) scale = g.DpiX / 96f;
                if (scale != 1) dialog.Scale(new SizeF(scale, scale));
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog; dialog.MaximizeBox = false; dialog.MinimizeBox = false;
                update(); return dialog.ShowDialog(owner) == DialogResult.OK ? result : null;
            }
        }
    }
}
