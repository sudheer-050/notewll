using System;
using System.IO;
namespace Notewll
{
    static class BackendVerification
    {
        public static void Run(string root, string firstWorker, string secondWorker)
        {
            Config.DiagnosticDataRoot(root);
            var config = new Config { Theme = "Dark" }; config.Save();
            string original = File.ReadAllText(Config.FilePath);
            using (var locked = new FileStream(Config.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                config.Theme = "Light"; bool rejected = false;
                try { config.Save(); } catch (IOException) { rejected = true; }
                if (!rejected || File.ReadAllText(Config.FilePath) != original) throw new Exception("A failed preference save damaged the last saved configuration.");
            }
            config.Save();
            if (Config.Load().Theme != "Light" || Directory.GetFiles(root, "*.tmp").Length != 0) throw new Exception("Atomic preference save did not complete cleanly.");
            try
            {
                config.QualityEnabled = true; config.QualityPython = firstWorker;
                if (SpeechQuality.Recognize(config, new byte[0], false).text != Path.GetFileNameWithoutExtension(firstWorker)) throw new Exception("First speech worker was not selected.");
                config.QualityPython = secondWorker;
                if (SpeechQuality.Recognize(config, new byte[0], false).text != Path.GetFileNameWithoutExtension(secondWorker)) throw new Exception("Speech worker ignored the selected runtime.");
            }
            finally { SpeechQuality.Shutdown(); }
            Console.Write("backend-ok|atomic-preferences|failed-save-preserved|speech-runtime-switch");
        }
    }
}
