using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Windows.Forms;
namespace Notewll
{
    static class WritingProfiles
    {
        public static Dictionary<string, string> Validate(string text)
        {
            var rules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in (text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('=');
                if (parts.Length != 2 || !Regex.IsMatch(parts[0].Trim(), @"^[\w.-]{1,80}$") || !new[] { "Standard", "Chat", "Email", "Code" }.Contains(parts[1].Trim()) || rules.ContainsKey(parts[0].Trim())) throw new ArgumentException("Use one unique app=Style rule per line; styles: Standard, Chat, Email, Code.");
                rules.Add(parts[0].Trim(), parts[1].Trim());
            }
            if (rules.Count > 100) throw new ArgumentException("Keep at most 100 app styles.");
            return rules;
        }
        public static Config Resolve(Config config, string process)
        {
            var copy = config.Copy(); string style;
            if (copy.AppStylesEnabled && Validate(copy.AppWritingStyles).TryGetValue(process ?? "", out style)) copy.WritingStyle = style;
            return copy;
        }
    }
    sealed partial class SettingsForm
    {
        CheckBox appStyles;
        string appStyleDraft;
        Control AppStyleControls()
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 2 };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            appStyles = new ThemedCheckBox { Text = "Use styles for specific apps (local)", AutoSize = true, Checked = source.AppStylesEnabled };
            richSnippetPaste.Checked = source.RichSnippetPaste; richSnippetPaste.CheckedChanged += (s, e) => MarkDirty(); panel.Controls.Add(richSnippetPaste, 0, 0); panel.SetColumnSpan(richSnippetPaste, 2);
            appStyleDraft = source.AppWritingStyles; appStyles.CheckedChanged += (s, e) => MarkDirty(); panel.Controls.Add(appStyles, 0, 1); appStyles.Anchor = AnchorStyles.Left;
            var appRuleButton = DialogUi.Button("App rules...", (s, e) => {
                using (var dialog = DialogUi.Create("App writing styles", new Size(560, 420)))
                {
                    var text = new ThemedTextBox { Multiline = true, Dock = DockStyle.Fill, Text = appStyleDraft, AccessibleName = "App writing style rules" };
                    var info = DialogUi.Text("One app process name per line, without .exe: outlook=Email, slack=Chat, code=Code. No window contents are read."); info.Dock = DockStyle.Top; info.AutoSize = false; info.Height = 80;
                    var save = DialogUi.Button("Use rules", (a, b) => { try { WritingProfiles.Validate(text.Text); appStyleDraft = text.Text; MarkDirty(); dialog.Close(); } catch (Exception ex) { MessageBox.Show(dialog, ex.Message); } }, true); save.Dock = DockStyle.Bottom;
                    var cancel = DialogUi.Button("Cancel", (a, b) => dialog.Close()); cancel.Dock = DockStyle.Bottom;
                    dialog.Controls.Add(text); dialog.Controls.Add(info); dialog.Controls.Add(save); dialog.Controls.Add(cancel); dialog.CancelButton = cancel; ThemeColors.Apply(dialog, source.Theme); dialog.ShowDialog(this);
                }
            }); appRuleButton.Dock = DockStyle.Fill; appRuleButton.AutoSize = false; appRuleButton.Height = 38; panel.Controls.Add(appRuleButton, 1, 1); return panel;
        }
    }
}
