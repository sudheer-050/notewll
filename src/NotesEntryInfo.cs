using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace Notewll
{
    static class NotesEntryInfo
    {
        internal static List<KeyValuePair<string, string>> Details(NoteStore store, NoteDocument entry)
        {
            string kind = ActivityHistory.Kind(entry);
            var rows = new List<KeyValuePair<string, string>>(); Action<string, string> add = (label, value) => rows.Add(new KeyValuePair<string, string>(label, value));
            add("Type", kind == "Note" ? "Personal note" : kind == "Meeting" ? "Meeting / conversation" : "Dictation");
            add("Created", ActivityHistory.LocalDate(entry).ToString("dddd, MMMM d, yyyy 'at' HH:mm:ss"));
            int words = ActivityHistory.Words(entry), sentences = ActivityHistory.SentenceCount(ActivityHistory.Text(entry));
            add("Content", words.ToString("N0") + (words == 1 ? " word / " : " words / ") + sentences.ToString("N0") + (sentences == 1 ? " sentence" : " sentences"));
            if (kind == "Dictation")
            {
                string app = entry.Platform;
                if (String.Equals(app, "chrome", StringComparison.OrdinalIgnoreCase)) app = "Google Chrome";
                else if (String.Equals(app, "msedge", StringComparison.OrdinalIgnoreCase)) app = "Microsoft Edge";
                else if (String.Equals(app, "explorer", StringComparison.OrdinalIgnoreCase)) app = "File Explorer";
                add("Written in", String.IsNullOrWhiteSpace(app) ? "App was not recorded for this entry" : app);
                add("Window", String.IsNullOrWhiteSpace(entry.TargetWindowTitle) ? "Window title was not recorded for this entry" : entry.TargetWindowTitle);
            }
            else
            {
                add("Written in", "Notewll / " + (kind == "Note" ? "Personal notes" : "Meetings"));
                if (entry.Segments.Count > 0) add("Audio source", entry.Mode == "Both" ? "Microphone and computer audio" : entry.Mode == "Computer" ? "Computer audio" : "Microphone / in-person audio");
                if (entry.DurationSeconds > 0) add("Capture length", VoiceKpis.Duration(entry.DurationSeconds / 60));
            }
            var audio = new[] { "Microphone", "Computer" }.Where(source => File.Exists(store.AudioPath(entry, source))).ToArray();
            add("Saved recording", audio.Length == 0 ? "No saved audio" : String.Join(" and ", audio));
            if (!String.IsNullOrWhiteSpace(entry.UserName)) add("Your name", entry.UserName);
            if (entry.Participants.Count > 0) add("Participants", String.Join(", ", entry.Participants));
            if (!String.IsNullOrWhiteSpace(entry.Tags)) add("Tags", entry.Tags);
            add("Status", String.IsNullOrWhiteSpace(entry.Status) ? "Saved" : entry.Status);
            add("Storage", "Saved locally on this device"); return rows;
        }
        public static void Show(Form owner, NoteStore store, NoteDocument entry)
        {
            using (var dialog = DialogUi.Create("Info", new Size(660, 590)))
            {
                dialog.AutoScaleMode = AutoScaleMode.None;
                var heading = new Label { Text = entry.Title, Dock = DockStyle.Top, Height = 60, Font = new Font("Segoe UI", 18, FontStyle.Bold), AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 10, 0) };
                var card = new Surface { Dock = DockStyle.Fill, Padding = new Padding(20), AccessibleName = "Entry information" };
                var scroll = new BufferedScrollPanel { Dock = DockStyle.Fill, AutoScroll = true };
                var grid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = Padding.Empty, Margin = Padding.Empty };
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                var rows = Details(store, entry); int index = 0;
                foreach (var item in rows)
                {
                    grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    var label = new Label { Text = item.Key, AutoSize = true, Font = new Font("Segoe UI", 9), Margin = new Padding(0, 5, 14, 16) };
                    var value = new Label { Text = item.Value, AutoSize = true, Dock = DockStyle.Top, Font = new Font("Segoe UI", 10), Margin = new Padding(0, 3, 0, 16), AccessibleName = item.Key };
                    grid.Controls.Add(label, 0, index); grid.Controls.Add(value, 1, index++);
                }
                grid.Layout += (s, e) => { int width = Math.Max(100, grid.ClientSize.Width - (int)grid.ColumnStyles[0].Width - 6); foreach (var label in grid.Controls.OfType<Label>().Where(label => grid.GetColumn(label) == 1)) { var maximum = new Size(width, 0); if (label.MaximumSize != maximum) label.MaximumSize = maximum; } };
                scroll.Controls.Add(grid); card.Controls.Add(scroll);
                var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0) };
                var close = new HubButton { Text = "Close", Width = 120, Height = 38, Primary = true }; close.Click += (s, e) => dialog.Close(); footer.Controls.Add(close); dialog.CancelButton = close;
                var copy = new HubButton { Text = "Copy details", Width = 150, Height = 38 }; copy.Click += (s, e) => { try { Clipboard.SetDataObject(entry.Title + Environment.NewLine + String.Join(Environment.NewLine, rows.Select(row => row.Key + ": " + row.Value)), true, 5, 100); copy.Text = "Copied"; } catch (Exception ex) { MessageBox.Show(dialog, "Could not copy: " + ex.Message); } }; footer.Controls.Add(copy);
                dialog.Controls.Add(card); dialog.Controls.Add(footer); dialog.Controls.Add(heading); heading.SendToBack(); footer.SendToBack();
                ThemeColors.Apply(dialog, ThemeColors.Mode);
                dialog.ShowDialog(owner);
            }
        }
    }
}
