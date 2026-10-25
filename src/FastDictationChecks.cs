using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;

namespace Notewll
{
    static class FastDictationChecks
    {
        public static int Run(string[] args)
        {
            if (args[0] == "--recognition-cancel-smoke")
            {
                using (var entered = new ManualResetEvent(false))
                using (var release = new ManualResetEvent(false))
                using (var recognition = new LiveRecognition())
                {
                    object gate = typeof(LiveRecognition).GetField("gate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(recognition);
                    var inference = System.Threading.Tasks.Task.Run(() => { lock (gate) { entered.Set(); release.WaitOne(); } });
                    try
                    {
                        if (!entered.WaitOne(2000)) throw new Exception("Controlled inference did not start.");
                        var cancel = System.Threading.Tasks.Task.Run(() => recognition.Dispose());
                        if (!cancel.Wait(500)) throw new Exception("Cancel blocked behind recognition.");
                    }
                    finally { release.Set(); inference.Wait(2000); }
                    if (recognition.Snapshot(new byte[0], 0, true) != "") throw new Exception("Canceled recognition accepted another request.");
                }
                Console.Write("cancel-ok|nonblocking|late-work-ignored"); return 0;
            }
            if (args[0] == "--live-grammar-smoke")
            {
                using (var started = new ManualResetEvent(false))
                using (var release = new ManualResetEvent(false))
                {
                    int calls = 0;
                    var grammar = new LiveGrammar(raw => { Interlocked.Increment(ref calls); started.Set(); release.WaitOne(); return "I have a meeting."; });
                    if (grammar.Resolve("", 0) != "" || calls != 0) throw new Exception("Silence started grammar work.");
                    grammar.Offer("i has a meeting");
                    if (!started.WaitOne(2000)) throw new Exception("Background grammar did not start.");
                    var clock = Stopwatch.StartNew();
                    try
                    {
                        if (grammar.Resolve("i has a meeting", 100) != "i has a meeting" || clock.ElapsedMilliseconds > 500) throw new Exception("Slow grammar blocked release.");
                        if (grammar.Resolve("i has a meeting tomorrow", 0) != "i has a meeting tomorrow" || calls != 1) throw new Exception("Stale correction lost words or queued another request.");
                    }
                    finally { release.Set(); }
                    if (grammar.Resolve("i has a meeting", 2000) != "I have a meeting.") throw new Exception("Completed correction was not reused.");
                    if (grammar.Resolve("i has a meeting", 0) != "I have a meeting." || calls != 1) throw new Exception("Cached correction ran twice.");
                    using (var tailStarted = new ManualResetEvent(false))
                    using (var tailRelease = new ManualResetEvent(false))
                    {
                        var prefix = new LiveGrammar(raw => { if (raw != "i has a meeting") { tailStarted.Set(); tailRelease.WaitOne(); } return "I have a meeting."; });
                        if (prefix.Resolve("i has a meeting", 2000) != "I have a meeting.") throw new Exception("Prefix correction was not ready.");
                        try
                        {
                            if (prefix.Resolve("i has a meeting tomorrow", 0) != "I have a meeting tomorrow") throw new Exception("Corrected prefix was lost as speech grew.");
                            if (!tailStarted.WaitOne(2000)) throw new Exception("Tail correction did not start.");
                            if (prefix.Resolve("i has a meetingful day", 0) != "i has a meetingful day" || prefix.Resolve("different words", 0) != "different words") throw new Exception("An unrelated cached correction was applied.");
                        }
                        finally { tailRelease.Set(); }
                    }
                    var failed = new LiveGrammar(raw => { throw new Exception("Engine unavailable"); });
                    if (failed.Resolve("keep my words", 2000) != "keep my words") throw new Exception("Failed grammar lost words.");
                    Console.Write("live-grammar-ok|bounded-release|exact-cache|no-stale-edit|single-request|silence-empty|failure-fallback");
                    return 0;
                }
            }
            SpeechEngine.DiagnosticRoot(args[1]);
            var config = Config.Load();
            bool accurate = config.AccurateDictationEnabled && Array.IndexOf(args, "--basic") < 0;
            bool useGrammar = !accurate && Array.IndexOf(args, "--grammar") >= 0;
            if (accurate) AccurateSpeech.Warmup(config);
            else { string problem = SpeechEngine.EnsureRunning(config, 10); if (problem != null) throw new Exception(problem); }
            var grammarConfig = config.Copy(); if (!useGrammar) grammarConfig.QualityEnabled = false;
            if (useGrammar) SpeechQuality.TryRefine(grammarConfig, "Hello.");
            var live = new LiveGrammar(raw => SpeechQuality.TryRefine(grammarConfig, raw));
            byte[] pcm = SpeechEngine.PcmFromWav(File.ReadAllBytes(args[2]));
            try { Console.Write(new JavaScriptSerializer().Serialize(Replay(config, live, pcm, accurate, useGrammar))); }
            finally { SpeechQuality.Shutdown(); AccurateSpeech.Shutdown(); SpeechEngine.Shutdown(); }
            return 0;
        }
        internal static object Replay(Config config, LiveGrammar live, byte[] pcm, bool accurate, bool useGrammar)
        {
            var elapsed = Stopwatch.StartNew();
            using (var recognition = new LiveRecognition(accurate ? config : null))
            {
                System.Threading.Tasks.Task pending = null;
                int step = accurate ? 16000 : 8000;
                for (int offset = step; offset < pcm.Length; offset += step)
                {
                    int pause = (int)(offset / 32L - elapsed.ElapsedMilliseconds);
                    if (pause > 0) Thread.Sleep(pause);
                    if (pending != null && !pending.IsCompleted) continue;
                    if (pending != null) pending.GetAwaiter().GetResult();
                    var snapshot = new byte[offset]; Buffer.BlockCopy(pcm, 0, snapshot, 0, offset);
                    pending = System.Threading.Tasks.Task.Run(() => live.Offer(recognition.Snapshot(snapshot, Peak(snapshot), false).Trim()));
                }
                int tailPause = (int)(pcm.Length / 32L - elapsed.ElapsedMilliseconds);
                if (tailPause > 0) Thread.Sleep(tailPause);
                var release = Stopwatch.StartNew();
                if (pending != null) { if (!pending.Wait(12000)) throw new Exception("Live recognition did not finish within its deadline."); pending.GetAwaiter().GetResult(); }
                string raw = recognition.Snapshot(pcm, Peak(pcm), true).Trim();
                long recognitionMs = release.ElapsedMilliseconds;
                string refined = live.Resolve(raw, accurate ? 0 : 600);
                string text = WritingFormat.Compose(config, refined);
                return new { AudioMs = pcm.Length / 32, ReleaseToTextMs = release.ElapsedMilliseconds, FinalRecognitionMs = recognitionMs, Raw = raw, Text = text, ModelCorrectionUsed = raw != refined, GrammarEnabled = useGrammar, AccurateRecognition = accurate, Scope = "Real-time WAV replay including outstanding inference on release; not physical microphone/shortcut or target-app insertion timing." };
            }
        }
        static int Peak(byte[] pcm) { int peak = 0; for (int i = 0; i + 1 < pcm.Length; i += 2) peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(pcm, i))); return peak; }
    }
}
