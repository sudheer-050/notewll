using System;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
namespace Notewll
{
    static class RichSnippets
    {
        static string Normalize(string text) { return text.Replace("\r\n", "\n").TrimEnd('\n'); }
        public static void Validate(PersonalRule rule)
        {
            if (String.IsNullOrEmpty(rule.ReplacementRtf)) return;
            if (rule.Kind != "Snippet" || rule.ReplacementRtf.Length > 64000 || Regex.IsMatch(rule.ReplacementRtf, @"\\(?:object|objdata|pict|file|datastore|field|bin)\b", RegexOptions.IgnoreCase)) throw new ArgumentException("Snippet formatting must be text only and at most 64 KB.");
            using (var editor = new RichTextBox()) { editor.Rtf = rule.ReplacementRtf; if (Normalize(editor.Text) != Normalize(rule.Replacement)) throw new ArgumentException("Formatted snippet text does not match its plain text."); }
        }
        public static PersonalRule Exact(Config config, string raw)
        {
            string trigger = (raw ?? "").Trim();
            return config.Personalization.FirstOrDefault(r => r.Kind == "Snippet" && (r.Trigger.Equals(trigger, StringComparison.OrdinalIgnoreCase) || r.Trigger.Equals(trigger.TrimEnd('.', ',', '!', '?'), StringComparison.OrdinalIgnoreCase)));
        }
        public static PersonalRule Formatted(Config config, string raw)
        { if (!config.CleanupEnabled || !config.RichSnippetPaste) return null; var rule = Exact(config, raw); return rule == null || String.IsNullOrEmpty(rule.ReplacementRtf) ? null : rule; }
        public static Control Editor(RichTextBox editor)
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, WrapContents = false };
            foreach (var style in new[] { FontStyle.Bold, FontStyle.Italic })
            {
                var captured = style;
                bar.Controls.Add(DialogUi.Button(style == FontStyle.Bold ? "Bold" : "Italic", (s, e) => { var font = editor.SelectionFont ?? editor.Font; editor.SelectionFont = new Font(font, font.Style ^ captured); editor.Focus(); }));
            }
            bar.Controls.Add(DialogUi.Button("Bullets", (s, e) => { editor.SelectionBullet = !editor.SelectionBullet; editor.Focus(); }));
            bar.Controls.Add(DialogUi.Button("Link...", (s, e) => {
                using (var dialog = DialogUi.Create("Insert link", new Size(480, 200)))
                {
                    var url = new ThemedTextBox { Height = 36, Dock = DockStyle.Top, AccessibleName = "Link URL" };
                    var save = DialogUi.Button("Insert URL", (a, b) => { Uri uri; if (!Uri.TryCreate(url.Text, UriKind.Absolute, out uri) || (uri.Scheme != "https" && uri.Scheme != "http")) { MessageBox.Show(dialog, "Enter an http or https URL."); return; } editor.SelectedText = uri.AbsoluteUri; dialog.Close(); }, true);
                    var cancel = DialogUi.Button("Cancel", (a, b) => dialog.Close()); cancel.Dock = DockStyle.Bottom;
                    save.Dock = DockStyle.Bottom; dialog.Controls.Add(url); dialog.Controls.Add(save); dialog.Controls.Add(cancel); dialog.CancelButton = cancel; dialog.AcceptButton = save; ThemeColors.Apply(dialog, ThemeColors.Mode); dialog.ShowDialog(panel.FindForm());
                }
            }));
            foreach (Control button in bar.Controls) { button.AutoSize = false; button.MinimumSize = Size.Empty; button.Size = new Size(100, 32); button.Padding = Padding.Empty; }
            editor.Dock = DockStyle.Fill; panel.Controls.Add(editor); panel.Controls.Add(bar); return panel;
        }
        public static bool Paste(Config config, IntPtr target, PersonalRule rule)
        {
            if (!config.CleanupEnabled || !config.RichSnippetPaste || String.IsNullOrEmpty(rule.ReplacementRtf) || Native.GetForegroundWindow() != target || Native.ModifiersDown()) return false;
            Validate(rule); var original = ClipboardPaste.Snapshot(); uint sequence = 0;
            try { var data = new DataObject(); data.SetData(DataFormats.Rtf, rule.ReplacementRtf); data.SetData(DataFormats.UnicodeText, rule.Replacement); Clipboard.SetDataObject(data, true, 5, 100); sequence = ClipboardPaste.Sequence; return Native.Paste(target, config.PasteMethod); }
            finally { if (sequence != 0) ClipboardPaste.RestoreAfterPaste(original, sequence); }
        }
    }
    sealed partial class SettingsForm
    {
        readonly CheckBox richSnippetPaste = new ThemedCheckBox { AutoSize = true, Text = "Paste formatted snippets on release (uses clipboard; turns off live insertion)" };
    }
}
