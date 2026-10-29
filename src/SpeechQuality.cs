using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Notewll
{
    sealed class VoiceTurn
    {
        public double start { get; set; }
        public double end { get; set; }
        public string text { get; set; }
        public double[] embedding { get; set; }
    }
    sealed class QualityResult
    {
        public string text { get; set; }
        public string error { get; set; }
        public List<VoiceTurn> turns { get; set; }
    }
    static class SpeechQuality
    {
        static readonly object gate = new object();
        static Process worker;
        static string python;
        public static bool Available(Config config) { return (config.QualityEnabled || config.SpeechLanguage != "en") && File.Exists(config.QualityPython) && File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "engine", "quality_worker.py")); }
        static QualityResult Call(Config config, object input)
        {
            lock (gate)
            {
                if (!Available(config)) throw new SttException(config.SpeechLanguage != "en" ? "Install multilingual speech in Preferences > Storage > Optional features, or choose English." : "Set up enhanced meeting speech in Preferences > Storage > Optional features, then enable it in Writing.");
                if (worker != null && python != config.QualityPython) Shutdown();
                if (worker == null || worker.HasExited)
                {
                    Shutdown();
                    var info = new ProcessStartInfo(config.QualityPython, "-u \"" + Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "engine", "quality_worker.py") + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
                    worker = Process.Start(info); python = config.QualityPython; worker.ErrorDataReceived += (s, e) => { }; worker.BeginErrorReadLine();
                }
                var json = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
                worker.StandardInput.WriteLine(json.Serialize(input)); worker.StandardInput.Flush();
                var read = Task.Run(() => worker.StandardOutput.ReadLine());
                if (!read.Wait(180000)) { worker.Kill(); worker.Dispose(); worker = null; throw new SttException("Enhanced speech timed out. Your audio can be retried."); }
                if (read.Result == null) { Shutdown(); throw new SttException("Local speech stopped. Your audio can be retried."); }
                QualityResult result;
                try { result = json.Deserialize<QualityResult>(read.Result); }
                catch (Exception) { Shutdown(); throw new SttException("Local speech returned an invalid response. Retry the retained audio."); }
                if (result == null || result.text == null && String.IsNullOrEmpty(result.error)) { Shutdown(); throw new SttException("Local speech returned no transcript. Retry the retained audio."); }
                if (!String.IsNullOrEmpty(result.error)) throw new SttException(result.error);
                if (result.turns == null) result.turns = new List<VoiceTurn>();
                return result;
            }
        }
        public static QualityResult Recognize(Config config, byte[] pcm, bool voices) { return Call(config, new { pcm = Convert.ToBase64String(pcm), voices = voices, language = config.SpeechLanguage, vocabulary = PersonalRule.Vocabulary(config) }); }
        public static string Refine(Config config, string text) { return SmartCleanup.Complete(config, text, 0); }
        public static string TryRefine(Config config, string text)
        {
            // Do not queue old partial sentences behind meeting transcription or another dictation.
            if (!System.Threading.Monitor.TryEnter(gate)) return null;
            try { return Refine(config, text); }
            finally { System.Threading.Monitor.Exit(gate); }
        }
        public static void Shutdown() { lock (gate) { if (worker != null) { if (!worker.HasExited) worker.Kill(); worker.Dispose(); worker = null; } } }
    }
}
