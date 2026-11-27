using System;
using System.Drawing;
using System.Windows.Forms;

namespace Notewll
{
    // Shown before anything is typed the first time the AI prompt shortcut is used, or when the chosen
    // way of writing prompts is not ready (for example "my own API key" without a key).
    static class PromptSetup
    {
        public enum Choice { InChat, OwnKey, NotNow }

        public static Choice Show(string theme, string problem)
        {
            using (var dialog = DialogUi.Create("AI prompts", new Size(600, 420)))
            {
                dialog.StartPosition = FormStartPosition.CenterScreen; dialog.TopMost = true;
                var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
                Func<string, float, bool, Label> text = (value, size, bold) => new Label { Text = value, AutoSize = true, MaximumSize = new Size(540, 0), Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), Margin = new Padding(0, 0, 0, 10), UseMnemonic = false };
                layout.Controls.Add(text("AI prompts", 14, true));
                layout.Controls.Add(text(problem ?? "Say your request and Notewll asks the AI you're using to show an improved version of it, then answer.", 10, false));
                layout.Controls.Add(text("How should prompts be written?", 10, true));
                var inChat = new RadioButton { Text = "The AI I'm typing into - free and private (recommended)", AutoSize = true, Checked = true, Font = new Font("Segoe UI", 10), Margin = new Padding(0, 0, 0, 4) };
                var ownKey = new RadioButton { Text = "My own API key (Gemini, Claude, OpenAI...) - opens settings to add it", AutoSize = true, Font = new Font("Segoe UI", 10), Margin = new Padding(0, 0, 0, 4) };
                layout.Controls.Add(inChat); layout.Controls.Add(ownKey);
                layout.Controls.Add(text("Your request is kept: it continues as soon as you choose. With your own key, this request uses the AI you're typing into while you add the key.", 9, false));
                var choice = Choice.NotNow;
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0) };
                var go = DialogUi.Button("Continue", (s, e) => { choice = ownKey.Checked ? Choice.OwnKey : Choice.InChat; dialog.Close(); }, true);
                var later = DialogUi.Button("Not now", (s, e) => { choice = Choice.NotNow; dialog.Close(); });
                buttons.Controls.Add(go); buttons.Controls.Add(later);
                dialog.AcceptButton = go; dialog.CancelButton = later; dialog.Controls.Add(layout); dialog.Controls.Add(buttons);
                ThemeColors.Apply(dialog, theme);
                dialog.Shown += (s, e) => { dialog.Activate(); go.Focus(); };
                dialog.ShowDialog();
                return choice;
            }
        }
    }
}
