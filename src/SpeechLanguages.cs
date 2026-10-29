using System;
using System.IO;
using System.Windows.Forms;
namespace Notewll
{
    static class SpeechLanguages
    {
        public static readonly string[] Codes = { "en", "auto", "es", "fr", "de", "it", "pt", "hi", "te", "ta", "ja", "ko", "zh", "ar" };
        public static readonly string[] Names = { "English (bundled)", "Detect language (local model)", "Spanish", "French", "German", "Italian", "Portuguese", "Hindi", "Telugu", "Tamil", "Japanese", "Korean", "Chinese", "Arabic" };
        public static bool Ready(Config c) { return File.Exists(c.QualityPython) && File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "whisper", "base.pt")); }
    }
    sealed partial class SettingsForm
    {
        readonly ComboBox speechLanguage = new ThemedComboBox { DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Speech language" };
        void BuildSpeechLanguages(Panel recording)
        {
            Label(recording, "Speech language", 20, 552, 620, 26, 11, true, Ink);
            speechLanguage.Items.AddRange(SpeechLanguages.Names); speechLanguage.SelectedIndex = Array.IndexOf(SpeechLanguages.Codes, source.SpeechLanguage); Setting(recording, speechLanguage, 586);
            Label(recording, "Other languages require the optional local multilingual model. English cleanup rules are skipped for them. Multilingual dictation finishes after you release the shortcut.", 20, 632, 620, 70, 9, false, Muted);
            speechLanguage.SelectedIndexChanged += (s, e) => MarkDirty();
        }
    }
}
