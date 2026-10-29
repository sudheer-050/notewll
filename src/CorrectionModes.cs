using System;
using System.Windows.Forms;

namespace Notewll
{
    sealed partial class SettingsForm
    {
        readonly ComboBox correctionMode = new ThemedComboBox { DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Text mode" };
        void InitializeCorrectionModes()
        {
            
            correctionMode.Items.AddRange(new object[] { "Raw text", "Modified text", "Light cleanup" });
            correctionMode.SelectedIndex = !source.CleanupEnabled ? 0 : source.CleanupLevel == "Light" ? 2 : 1;
            
            correctionMode.SelectedIndexChanged += (s, e) => {
                cleanup.Checked = correctionMode.SelectedIndex != 0;
                MarkDirty(); RefreshCleaned();
            };
        }
        Config PracticeConfig()
        {
            var draft = source.Copy(); draft.CleanupEnabled = cleanup.Checked; draft.SmartCleanupEnabled = false;
            draft.AccurateDictationEnabled = accurateMode.Checked; draft.QualityEnabled = quality.Checked; draft.ReduceNoise = noiseChoice.Checked;
            draft.WritingStyle = writingStyle.SelectedItem == null ? source.WritingStyle : writingStyle.Text;
            draft.CleanupLevel = correctionMode.SelectedIndex == 2 ? "Light" : "Standard";
            draft.SpeechLanguage = SpeechLanguages.Codes[Math.Max(0, speechLanguage.SelectedIndex)];
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
