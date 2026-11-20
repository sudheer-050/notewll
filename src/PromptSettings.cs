using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Notewll
{
    sealed partial class SettingsForm
    {
        static readonly string[] PromptModes = { "Raw", "WakeWord", "Automatic" };
        readonly ComboBox promptMode = new ThemedComboBox { DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "AI prompts" };
        readonly TextBox promptModel = new TextBox { AccessibleName = "Local prompt model", Width = 220 };
        readonly CheckBox promptReview = new ThemedCheckBox { Text = "Let me review and edit each prompt before it is typed", AutoSize = true };

        void BuildPromptControls(Panel page, int top)
        {
            Label(page, "AI prompts", 20, top, 620, 26, 11, true, Ink);
            promptMode.Items.AddRange(new object[] { "Raw text - type exactly what I say", "Wake word - say \"Claude, ...\" or \"ChatGPT, ...\" first", "Automatic - when typing into an AI app or tab" });
            Setting(page, promptMode, top + 32);
            Label(page, "Rewrites your request into a prompt that follows that AI's own guidelines, using a local Ollama model. Short messages and greetings are typed as spoken. The wake word is never typed.", 20, top + 80, 620, 50, 9, false, Muted);
            Label(page, "Local model", 20, top + 138, 120, 26, 10, false, Ink); promptModel.Location = new System.Drawing.Point(140, top + 136); page.Controls.Add(promptModel);
            promptModel.TextChanged += (s, e) => MarkDirty(); promptMode.SelectedIndexChanged += (s, e) => MarkDirty();
            Setting(page, promptReview, top + 176); promptReview.CheckedChanged += (s, e) => MarkDirty();
            Button(page, "Open prompt guides", 20, top + 222, 220, (s, e) => { try { PromptCrafter.EnsureGuides(); Process.Start(new ProcessStartInfo { FileName = PromptCrafter.Folder, UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Prompt guides"); } });
            Button(page, "Update prompt guides", 260, top + 222, 240, async (s, e) =>
            {
                var button = (Control)s; button.Enabled = false; string text = button.Text; button.Text = "Updating...";
                try { string result = await Task.Run(() => PromptCrafter.Update()); MessageBox.Show(this, "Prompt guides: " + result, "Prompt guides updated"); }
                catch (Exception ex) { MessageBox.Show(this, "Could not update the prompt guides. Your current guides are unchanged.\n\n" + ex.Message, "Prompt guides"); }
                finally { button.Enabled = true; button.Text = text; }
            });
            LoadPromptSettings();
        }
        void LoadPromptSettings()
        {
            if (promptMode.Items.Count == 0) return;
            promptMode.SelectedIndex = Math.Max(0, Array.IndexOf(PromptModes, source.PromptMode)); promptModel.Text = source.PromptModel ?? "qwen3:8b"; promptReview.Checked = source.PromptReview;
        }
        void SavePromptSettings(Config result)
        {
            result.PromptMode = PromptModes[Math.Max(0, promptMode.SelectedIndex)]; result.PromptModel = promptModel.Text.Trim(); result.PromptReview = promptReview.Checked;
        }
    }
}
