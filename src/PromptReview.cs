using System;
using System.Drawing;
using System.Windows.Forms;

namespace Notewll
{
    // Lets the person edit the rewritten prompt before it is typed. Returns the text to type, or null to type nothing.
    static class PromptReview
    {
        public static string Show(string guideName, string prompt, string spoken, string theme)
        {
            using (var dialog = DialogUi.Create(guideName + " prompt", new Size(640, 420)))
            {
                dialog.StartPosition = FormStartPosition.CenterScreen; dialog.TopMost = true; dialog.KeyPreview = true;
                var hint = DialogUi.Text("Edit the prompt if you like, then type it. Ctrl+Enter types it; Esc discards."); hint.Dock = DockStyle.Top; hint.AutoSize = false; hint.Height = 34;
                var box = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true, AcceptsTab = false, WordWrap = true, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 11), Text = DialogUi.Lines(prompt), AccessibleName = guideName + " prompt" };
                string result = null;
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0) };
                var type = DialogUi.Button("Type it", (s, e) => { result = box.Text.Replace("\r\n", "\n").Trim(); dialog.Close(); }, true);
                var mine = DialogUi.Button("Use my words", (s, e) => { result = spoken; dialog.Close(); });
                var discard = DialogUi.Button("Discard", (s, e) => { result = null; dialog.Close(); });
                buttons.Controls.Add(type); buttons.Controls.Add(mine); buttons.Controls.Add(discard);
                dialog.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter && e.Control) { e.SuppressKeyPress = true; type.PerformClick(); }
                    else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; discard.PerformClick(); }
                };
                dialog.Controls.Add(box); dialog.Controls.Add(hint); dialog.Controls.Add(buttons);
                ThemeColors.Apply(dialog, theme);
                dialog.Shown += (s, e) => { dialog.Activate(); box.Focus(); box.SelectionStart = box.TextLength; };
                dialog.ShowDialog();
                return String.IsNullOrWhiteSpace(result) ? null : result;
            }
        }
    }
}
