using System;
using System.IO;
using System.Linq;

namespace Notewll
{
    static class HandsFreeVerification
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        public static void Worker(string root, string python, string engine, string[] clips)
        {
            Config.DiagnosticDataRoot(Path.Combine(Path.GetFullPath(root), "data")); SpeechEngine.DiagnosticRoot(engine);
            var config = new Config { HandsFreePython = python };
            var samples = clips.Select(p => SpeechEngine.PcmFromWav(File.ReadAllBytes(p))).ToArray();
            try {
                MyVoiceMatcher.Ready(config); var profile = MyVoiceMatcher.Enroll(config, samples); profile.Save(); profile = MyVoiceProfile.Load();
                Check(MyVoiceMatcher.Accept(config, profile, samples[0]), "Enrollment sample did not pass strict matching");
                Check(!MyVoiceMatcher.Accept(config, profile, new byte[96000]), "Silence passed voice matching");
                string text = SpeechEngine.Transcribe(config, MicRecorder.ToWav(samples[0])); Check(!String.IsNullOrWhiteSpace(text), "Accepted speech did not transcribe");
                File.Delete(MyVoiceProfile.FilePath);
                Console.Write("handsfree-worker-ok|local-ready|three-sample-enrollment|encrypted-roundtrip|matched-accepted|silence-rejected|accepted-speech-transcribed|no-typing-injected");
            } finally { foreach (var sample in samples) Array.Clear(sample, 0, sample.Length); MyVoiceMatcher.Shutdown(); SpeechEngine.Shutdown(); }
        }
        public static void Run(string root)
        {
            Config.DiagnosticDataRoot(Path.Combine(Path.GetFullPath(root), "data"));
            var vector = new double[256]; vector[7] = 1;
            var profile = new MyVoiceProfile { Version = 1, References = new[] { vector, vector, vector } };
            profile.Save(); Check(MyVoiceProfile.Load().References[1][7] == 1, "Encrypted voice profile did not roundtrip");
            var ciphertext = File.ReadAllBytes(MyVoiceProfile.FilePath); ciphertext[ciphertext.Length / 2] ^= 17; File.WriteAllBytes(MyVoiceProfile.FilePath, ciphertext);
            bool rejected = false; try { MyVoiceProfile.Load(); } catch { rejected = true; } Check(rejected, "Tampered voice profile was accepted"); profile.Save();
            var target = new HandsFreeTarget { Window = (IntPtr)1, Focus = (IntPtr)2, Caret = (IntPtr)3, Epoch = 12 };
            var moved = new HandsFreeTarget { Window = (IntPtr)1, Focus = (IntPtr)2, Caret = (IntPtr)3, Epoch = 13 };
            var other = new HandsFreeTarget { Window = (IntPtr)4, Focus = (IntPtr)2, Caret = (IntPtr)3, Epoch = 12 };
            Check(!target.Same(null) && !target.Same(moved) && !target.Same(other), "Focus or physical input change did not invalidate the target");
            var gate = new HandsFreeBuffer(); var silence = new byte[640]; var speech = new byte[640];
            for (int i = 0; i < speech.Length; i += 2) Buffer.BlockCopy(BitConverter.GetBytes((short)1100), 0, speech, i, 2);
            for (int i = 0; i < 100000; i++) Check(gate.Frame(silence, target) == null && gate.BufferedBytes <= 6400, "Silence accumulated beyond the 200 ms pre-roll");
            for (int i = 0; i < 74; i++) gate.Frame(speech, target);
            for (int i = 0; i < 35; i++) Check(gate.Frame(silence, target) == null, "A too-short phrase was queued");
            gate.Reset(); for (int i = 0; i < 10; i++) gate.Frame(silence, target);
            for (int i = 0; i < 100; i++) gate.Frame(speech, target);
            HandsFreePhrase phrase = null; for (int i = 0; i < 35; i++) phrase = gate.Frame(silence, target);
            Check(phrase != null && phrase.Pcm.Length == 70400 && phrase.Target.Same(target), "Phrase pre-roll or trailing-silence trimming failed");
            for (int i = 0; i < 100; i++) gate.Frame(speech, target); gate.Frame(speech, moved);
            Check(gate.BufferedBytes == 0, "Moving the caret retained an in-flight phrase");
            for (int i = 0; i < 200; i++) Check(gate.Frame(speech, moved) == null && gate.BufferedBytes == 0, "A discarded phrase's tail started typing into the new target");
            for (int i = 0; i < 35; i++) gate.Frame(silence, moved);
            for (int i = 0; i < 100; i++) gate.Frame(speech, moved);
            phrase = null; for (int i = 0; i < 35; i++) phrase = gate.Frame(silence, moved);
            Check(phrase != null, "A fresh phrase did not resume after silence"); gate.Reset();
            for (int i = 0; i < 1000; i++) { gate.Frame(speech, target); Check(gate.BufferedBytes <= 400 * 640, "Continuous speech grew without bound"); } gate.Reset();
            for (int i = 0; i < 100; i++) Check(gate.Frame(speech, null) == null && gate.BufferedBytes == 0, "No-target speech was captured");
            int bytes = 0;
            using (var recorder = new MicRecorder(Config.DefaultMic, chunk => bytes += chunk.Length)) { for (int i = 0; i < 1000; i++) recorder.AppendChunk(speech); Check(recorder.Bytes == 0 && bytes == 640000, "Always-on mic retained its full audio history"); }
            using (var recorder = new MicRecorder(Config.DefaultMic)) { recorder.AppendChunk(speech); Check(recorder.Bytes == 640, "Shortcut recording lost its in-memory audio"); }
            File.Delete(MyVoiceProfile.FilePath); Check(!File.Exists(MyVoiceProfile.FilePath), "Voice profile deletion failed");
            Console.Write("handsfree-ok|dpapi-profile|tamper-rejected|focus-epoch|short-speech-rejected|silence-bounded|phrase-trimming|long-speech-bounded|no-target-discarded|streaming-mic-no-history|shortcut-buffer-preserved|profile-deleted");
        }
    }
}
