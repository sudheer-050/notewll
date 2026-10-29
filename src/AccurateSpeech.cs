using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Notewll
{
    static class DictationSpeech
    {
        public static string Transcribe(Config config, byte[] pcm)
        {
            if (config.SpeechLanguage != "en") return SpeechQuality.Recognize(config, pcm, false).text;
            if (config.AccurateDictationEnabled) return AccurateSpeech.Transcribe(config, pcm);
            int peak = 0;
            for (int i = 0; i + 1 < pcm.Length; i += 2) peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(pcm, i)));
            return SpeechEngine.Transcribe(config, MicRecorder.ToWav(MicRecorder.Normalize(pcm, peak)));
        }
    }
    // Separate from the grammar/meeting worker: grammar can never hold up recognition.
    static class AccurateSpeech
    {
        static readonly object gate = new object();
        static Process worker;
        static string python;
        static volatile bool ready;
        public static bool Ready { get { return ready; } }
        static QualityResult Call(Config config, object request, int timeout)
        {
            lock (gate)
            {
                try
                {
                    string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "engine", "accurate_worker.py");
                    if (!File.Exists(config.QualityPython) || !File.Exists(script)) throw new SttException("Set up accurate speech in Preferences > Storage > Optional features, then enable it in Writing.");
                    if (worker != null && python != config.QualityPython) Stop();
                    if (worker == null || worker.HasExited)
                    {
                        Stop();
                        var info = new ProcessStartInfo(config.QualityPython, "-u \"" + script + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
                        worker = Process.Start(info); python = config.QualityPython;
                        var started = worker; started.EnableRaisingEvents = true;
                        started.Exited += (s, e) => { if (Object.ReferenceEquals(worker, started)) ready = false; };
                        worker.ErrorDataReceived += (s, e) => { }; worker.BeginErrorReadLine();
                    }
                    var json = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
                    worker.StandardInput.WriteLine(json.Serialize(request)); worker.StandardInput.Flush();
                    var read = Task.Run(() => worker.StandardOutput.ReadLine());
                    if (!read.Wait(timeout)) throw new SttException("Accurate local speech timed out. Your audio is available in Recover last dictation; retry after the engine is ready.");
                    if (read.Result == null) throw new SttException("Accurate local speech stopped. Reopen Notewll to prepare it again.");
                    var result = json.Deserialize<QualityResult>(read.Result);
                    if (!String.IsNullOrEmpty(result.error)) throw new SttException(result.error);
                    return result;
                }
                catch { Stop(); throw; }
            }
        }
        public static void Warmup(Config config) { Call(config, new { op = "warmup" }, 30000); ready = true; }
        public static string Transcribe(Config config, byte[] pcm) { return Call(config, new { pcm = Convert.ToBase64String(pcm) }, ready ? 10000 : 30000).text; }
        static void Stop()
        {
            ready = false;
            if (worker == null) return;
            try
            {
                // Python's Windows venv launcher can have a child; stop only our recorded worker tree.
                if (!worker.HasExited)
                    using (var stop = Process.Start(new ProcessStartInfo("taskkill.exe", "/PID " + worker.Id + " /T /F") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })) stop.WaitForExit(3000);
            }
            catch { }
            finally { worker.Dispose(); worker = null; }
        }
        public static void Shutdown() { lock (gate) Stop(); }
    }
}
