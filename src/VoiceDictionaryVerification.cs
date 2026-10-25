using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace Notewll
{
    static class VoiceDictionaryVerification
    {
        static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        static System.Collections.Generic.IEnumerable<Control> Descendants(Control parent) { foreach (Control child in parent.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
        static void Snapshot(Form dialog, string path) { Application.DoEvents(); using (var image = new Bitmap(dialog.Width, dialog.Height)) { dialog.DrawToBitmap(image, new Rectangle(Point.Empty, dialog.Size)); image.Save(path); } }
        public static void Run(string root, string speechFixture = null)
        {
            var liveConfig = speechFixture == null ? null : Config.Load();
            Directory.CreateDirectory(root); Config.DiagnosticDataRoot(Path.Combine(root, "settings")); Application.EnableVisualStyles();
            var pcm = new byte[32000]; // UI/storage fixture, deliberately not a microphone or ASR accuracy trial.
            PersonalRule word;
            using (var dialog = new VoiceWordDialog(new Config(), new PersonalRule { Kind = "Word", Trigger = "", Replacement = "" }))
            {
                dialog.Show();
                var fields = Descendants(dialog).OfType<TextBox>().ToArray(); Assert(fields.Length == 1 && fields[0].AccessibleName == "Correct spelling", "Dictionary still asks for a typed trigger");
                Assert(!Descendants(dialog).OfType<Button>().Single(button => button.Text == "Use word").Enabled && VoiceWordDialog.OpenCount == 1, "Unrecorded words can be saved or recording guard is inactive");
                Snapshot(dialog, Path.Combine(root, "dictionary-before-recording.png"));
                dialog.AcceptRecording(pcm, "sue dear."); fields[0].Text = "Sudheer"; word = dialog.BuildRule();
                Assert(word.Trigger == "sue dear" && word.Replacement == "Sudheer" && word.PronunciationPcm.SequenceEqual(pcm), "Voice and spelling pair was not retained");
                Assert(!File.Exists(Pronunciations.PathFor(word.PronunciationId)), "Draft audio was written before Save preferences");
                Snapshot(dialog, Path.Combine(root, "dictionary-recorded.png"));
                Descendants(dialog).OfType<Button>().Single(button => button.Text == "Use word").PerformClick(); Assert(dialog.Result != null && dialog.Result.PronunciationPcm.Length == pcm.Length, "Closing destroyed accepted draft recording");
            }
            Assert(VoiceWordDialog.OpenCount == 0, "Recording guard remained active after closing");
            var config = new Config(); config.Personalization.Add(word); config.Personalization.Add(new PersonalRule { Kind = "Snippet", Trigger = "my sign off", Replacement = "Kind regards, Sudheer" });
            var copy = config.Copy(); Assert(copy.Personalization[0].PronunciationPcm.SequenceEqual(pcm) && !Object.ReferenceEquals(copy.Personalization[0].PronunciationPcm, word.PronunciationPcm), "Draft copying lost or shared the recorded voice buffer");
            config.Save(); string json = File.ReadAllText(Config.FilePath); Assert(!json.Contains("PronunciationPcm"), "Raw audio was embedded in preferences JSON");
            var saved = Config.Load(); var savedWord = saved.Personalization[0]; Assert(savedWord.PronunciationId == word.PronunciationId && Pronunciations.Load(savedWord.PronunciationId).SequenceEqual(pcm), "Pronunciation did not survive preferences save/reload");
            Assert(PersonalRule.Apply(saved, "sue dear said my sign off") == "Sudheer said Kind regards, Sudheer", "Recorded-word correction or snippet expansion failed");
            Assert(PersonalRule.Vocabulary(saved).SequenceEqual(new[] { "Sudheer" }), "Recognition hints contain snippets or lost the word");
            using (var dialog = new VoiceWordDialog(saved, savedWord))
            {
                dialog.Show(); Assert(Descendants(dialog).OfType<Button>().Single(button => button.Text == "Listen").Enabled, "Saved pronunciation cannot be replayed");
                dialog.AcceptRecording(pcm, "sue deer"); Descendants(dialog).OfType<TextBox>().Single().Text = "Sudheer";
                var replacement = dialog.BuildRule(); Assert(replacement.PronunciationId != savedWord.PronunciationId && File.Exists(Pronunciations.PathFor(savedWord.PronunciationId)), "Re-recording changed the active audio before save"); dialog.Close();
            }
            Assert(File.ReadAllText(Config.FilePath) == json && Directory.GetFiles(Pronunciations.Root, "*.wav").Length == 1, "Cancelling changed preferences or wrote draft audio");
            using (var legacy = new VoiceWordDialog(saved, new PersonalRule { Kind = "Word", Trigger = "note well", Replacement = "Notewll" }))
            { legacy.Show(); Assert(!Descendants(legacy).OfType<Button>().Single(button => button.Text == "Use word").Enabled, "Legacy text entry skipped pronunciation capture"); legacy.Close(); }
            foreach (var invalid in new[] { new byte[0], new byte[15998], new byte[32001], new byte[Pronunciations.MaximumBytes + 2] }) { bool rejected = false; try { Pronunciations.Validate(invalid); } catch (InvalidDataException) { rejected = true; } Assert(rejected, "Invalid audio size was accepted"); }
            bool traversal = false; try { Pronunciations.PathFor("../outside"); } catch (InvalidDataException) { traversal = true; } Assert(traversal, "Pronunciation identifier allowed path traversal");
            if (speechFixture != null)
            {
                var clip = SpeechEngine.PcmFromWav(File.ReadAllBytes(speechFixture)); if (clip.Length > Pronunciations.MaximumBytes) Array.Resize(ref clip, Pronunciations.MaximumBytes); Pronunciations.Validate(clip);
                try
                {
                    if (liveConfig.AccurateDictationEnabled) AccurateSpeech.Warmup(liveConfig);
                    string recognized = DictationSpeech.Transcribe(liveConfig, clip); Assert(!String.IsNullOrWhiteSpace(recognized), "Configured recognizer returned no text for speech fixture");
                    using (var dialog = new VoiceWordDialog(liveConfig, new PersonalRule { Kind = "Word", Trigger = "", Replacement = "" })) { dialog.Show(); dialog.AcceptRecording(clip, recognized); Descendants(dialog).OfType<TextBox>().Single().Text = "Test pronunciation"; var entry = dialog.BuildRule(); var trial = new Config(); trial.Personalization.Add(entry); trial.Save(); Assert(PersonalRule.Apply(trial, entry.Trigger) == "Test pronunciation" && Pronunciations.Load(entry.PronunciationId).SequenceEqual(clip), "Actual ASR sample did not become a persisted correction"); dialog.Close(); }
                    File.WriteAllText(Path.Combine(root, "recognition-result.txt"), "PASS: existing local speech fixture -> configured recognizer -> recorded spelling entry -> local audio/config persistence -> correction. No text was injected into the recognition step. Physical microphone capture still needs a spoken trial.");
                }
                finally { Array.Clear(clip, 0, clip.Length); AccurateSpeech.Shutdown(); SpeechQuality.Shutdown(); }
            }
            File.WriteAllText(Path.Combine(root, "result.txt"), "PASS: record/spelling UI (no typed trigger), unsaved drafts stay in memory, local WAV persistence/reload, cancellation, re-record isolation, old-entry compatibility, word correction, vocabulary hints, snippet isolation, buffer copying, audio bounds, path validation and recording guard. Audio input and recognized words were injected fixtures; physical microphone and live recognition need a real spoken trial.");
        }
    }
}
