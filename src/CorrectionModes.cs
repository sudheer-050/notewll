using System;
using System.Windows.Forms;

namespace Notewll
{
    sealed partial class SettingsForm
    {
        readonly ComboBox correctionMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Text mode" };
        void BuildCorrectionModes(Panel writing)
        {
            Label(writing, "Text mode", 20, 12, 620, 24, 11, true, Ink);
            correctionMode.Items.AddRange(new object[] { "Raw text", "Modified text" });
            correctionMode.SelectedIndex = source.CleanupEnabled ? 1 : 0;
            Setting(writing, correctionMode, 42);
            Label(writing, "Raw text keeps the recognized words, including fillers and repeats.\nModified text removes fillers and repeated starts, with basic formatting.\nSpeech recognition itself may still mishear words or supply punctuation.", 20, 80, 620, 66, 9, false, Muted);
            correctionMode.SelectedIndexChanged += (s, e) => {
                cleanup.Checked = correctionMode.SelectedIndex != 0;
                MarkDirty(); RefreshCleaned();
            };
        }
        Config PracticeConfig()
        {
            var draft = source.Copy(); draft.CleanupEnabled = cleanup.Checked; draft.SmartCleanupEnabled = false;
            draft.WritingStyle = writingStyle.SelectedItem == null ? source.WritingStyle : writingStyle.Text;
            draft.Personalization = pendingRules ?? source.Personalization; return draft;
        }
        void ApplyPracticeCorrection()
        {
            var draft = PracticeConfig(); var result = WritingFormat.Compose(draft, raw.Text);
            cleaned.Text = result; copy.Enabled = result.Length > 0;
            sttStatus.Text = draft.CleanupEnabled ? "Modified text: basic cleanup applied." : "Raw text: recognized words kept unchanged.";
        }
    }
}
