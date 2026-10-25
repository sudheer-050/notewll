using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Notewll
{
    sealed class MyVoiceProfile
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool MoveFileEx(string existing, string target, uint flags);
        public int Version { get; set; }
        public double[][] References { get; set; }
        public static string FilePath { get { return Path.Combine(Config.DirectoryPath, "my-voice.bin"); } }
        public void Validate()
        {
            if (Version != 1 || References == null || References.Length != 3 || References.Any(r => r == null || r.Length != 256 || r.Any(v => Double.IsNaN(v) || Double.IsInfinity(v)) || Math.Abs(r.Sum(v => v * v) - 1) > .15))
                throw new InvalidDataException("The voice profile is invalid. Enroll your voice again.");
        }
        public static MyVoiceProfile Load()
        {
            if (!File.Exists(FilePath)) throw new InvalidDataException("Enroll your voice in Preferences > Hands-free first.");
            if (new FileInfo(FilePath).Length > 65536) throw new InvalidDataException("The voice profile is invalid. Enroll again.");
            var plain = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), null, DataProtectionScope.CurrentUser);
            try { var result = new JavaScriptSerializer().Deserialize<MyVoiceProfile>(System.Text.Encoding.UTF8.GetString(plain)); result.Validate(); return result; }
            finally { Array.Clear(plain, 0, plain.Length); }
        }
        public void Save()
        {
            Validate(); Directory.CreateDirectory(Config.DirectoryPath);
            var plain = System.Text.Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(this));
            string temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                File.WriteAllBytes(temp, ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser));
                // Atomic replacement without File.Replace's extra ACL-copy permission requirement.
                if (!MoveFileEx(temp, FilePath, 1 | 8)) throw new IOException("Could not save the encrypted voice profile.", new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
            } finally { Array.Clear(plain, 0, plain.Length); if (File.Exists(temp)) File.Delete(temp); }
        }
    }
    sealed class VoiceMatchResult
    {
        public bool ready { get; set; }
        public bool accepted { get; set; }
        public double score { get; set; }
        public double[][] references { get; set; }
        public string error { get; set; }
    }
    static class MyVoiceMatcher
    {
        static readonly object gate = new object(); static Process worker; static string workerPython;
        public static string Python(Config config)
        {
            return new[] { config.HandsFreePython,
                Path.Combine(Config.DirectoryPath, "VoiceMatching", "Scripts", "python.exe"), config.QualityPython,
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".speech-venv", "Scripts", "python.exe")) }
                .FirstOrDefault(p => !String.IsNullOrWhiteSpace(p) && File.Exists(p));
        }
        static VoiceMatchResult Call(Config config, object request)
        {
            lock (gate) {
                string python = Python(config), script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "engine", "voice_match_worker.py");
                if (python == null || !File.Exists(script)) throw new InvalidDataException("Set up local voice matching in Preferences > Hands-free first.");
                if (worker == null || worker.HasExited || workerPython != python) {
                    Stop(); workerPython = python;
                    var info = new ProcessStartInfo(python, "-u \"" + script + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
                    info.EnvironmentVariables["NOTEWLL_VOICE_CACHE"] = Path.Combine(Config.DirectoryPath, "VoiceMatching", "cache");
                    worker = Process.Start(info);
                    worker.ErrorDataReceived += (s, e) => { }; worker.BeginErrorReadLine();
                }
                var json = new JavaScriptSerializer { MaxJsonLength = 3000000 };
                try {
                    worker.StandardInput.WriteLine(json.Serialize(request)); worker.StandardInput.Flush();
                    var read = Task.Run(() => worker.StandardOutput.ReadLine());
                    if (!read.Wait(60000) || read.Result == null) throw new IOException("Local voice matching stopped or timed out. Hands-free typing has paused.");
                    var result = json.Deserialize<VoiceMatchResult>(read.Result);
                    if (result == null || !String.IsNullOrEmpty(result.error)) throw new IOException(result == null ? "Invalid voice matching response." : result.error);
                    return result;
                } catch { Stop(); throw; }
            }
        }
        public static void Ready(Config config) { if (!Call(config, new { op = "ready" }).ready) throw new IOException("Voice matching is not ready."); }
        public static MyVoiceProfile Enroll(Config config, byte[][] clips)
        {
            var result = Call(config, new { op = "enroll", clips = clips.Select(Convert.ToBase64String).ToArray() });
            var profile = new MyVoiceProfile { Version = 1, References = result.references }; profile.Validate(); return profile;
        }
        public static bool Accept(Config config, MyVoiceProfile profile, byte[] pcm)
        {
            var result = Call(config, new { op = "verify", references = profile.References, pcm = Convert.ToBase64String(pcm) });
            // Never trust a bare accepted flag, NaN, or a low score from a failed worker.
            return result.accepted && !Double.IsNaN(result.score) && !Double.IsInfinity(result.score) && result.score >= .86;
        }
        static void Stop() { if (worker != null) { try { if (!worker.HasExited) worker.Kill(); } catch (InvalidOperationException) { } worker.Dispose(); worker = null; } }
        public static void Shutdown() { lock (gate) Stop(); }
    }

    sealed class HandsFreeTarget
    {
        [StructLayout(LayoutKind.Sequential)] struct GuiInfo { public int size, flags; public IntPtr active, focus, capture, menu, move, caret; public System.Drawing.Rectangle caretRect; }
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint process);
        [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint thread, ref GuiInfo info);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder name, int max);
        public string AppName; public IntPtr Window, Focus, Caret; public int Epoch; public System.Drawing.Rectangle CaretBounds;
        public static HandsFreeTarget Capture(int epoch)
        {
            IntPtr window = Native.GetForegroundWindow(); uint process;
            uint thread = GetWindowThreadProcessId(window, out process);
            if (window == IntPtr.Zero || thread == 0 || process == Process.GetCurrentProcess().Id) return null;
            var info = new GuiInfo { size = Marshal.SizeOf(typeof(GuiInfo)) };
            if (!GetGUIThreadInfo(thread, ref info) || info.focus == IntPtr.Zero) return null;
            var name = new System.Text.StringBuilder(128); GetClassName(info.focus, name, name.Capacity);
            if (name.ToString().IndexOf("edit", StringComparison.OrdinalIgnoreCase) >= 0 && (GetWindowLong(info.focus, -16) & 0x20) != 0) return null;
            string app; try { using (var targetProcess = Process.GetProcessById((int)process)) app = targetProcess.ProcessName; } catch { return null; }
            return new HandsFreeTarget { AppName = app, Window = window, Focus = info.focus, Caret = info.caret, CaretBounds = info.caretRect, Epoch = epoch };
        }
        public bool Same(HandsFreeTarget next) { return next != null && Window == next.Window && Focus == next.Focus && Caret == next.Caret && CaretBounds == next.CaretBounds && Epoch == next.Epoch; }
    }
    sealed class HandsFreePhrase
    {
        public byte[] Pcm; public HandsFreeTarget Target;
    }
    // Bounded 20 ms frame gate: no ever-growing always-on recording or saved ambient audio.
    sealed class HandsFreeBuffer
    {
        readonly List<byte[]> frames = new List<byte[]>();
        readonly Queue<byte[]> preRoll = new Queue<byte[]>();
        int voiced, silence, quietFrames; bool waitForSilence; HandsFreeTarget target, idleTarget;
        public int BufferedBytes { get { return (frames.Count + preRoll.Count) * 640; } }
        public void Reset() { frames.Clear(); preRoll.Clear(); voiced = silence = quietFrames = 0; waitForSilence = false; target = idleTarget = null; }
        public void DropUntilSilence() { Reset(); waitForSilence = true; }
        public HandsFreePhrase Frame(byte[] frame, HandsFreeTarget current)
        {
            if (frame.Length != 640) throw new ArgumentException("Expected a 20 ms PCM frame.");
            double energy = 0; int peak = 0;
            for (int i = 0; i < frame.Length; i += 2) { int v = BitConverter.ToInt16(frame, i); energy += (double)v * v; peak = Math.Max(peak, Math.Abs(v)); }
            bool speech = energy / 320 >= 240 * 240 && peak >= 550;
            if (waitForSilence) { quietFrames = speech ? 0 : quietFrames + 1; if (quietFrames >= 35) { waitForSilence = false; quietFrames = 0; } return null; }
            if (current == null) { if (speech) DropUntilSilence(); else Reset(); return null; }
            if (frames.Count == 0) {
                if (!speech) {
                    if (idleTarget == null || !idleTarget.Same(current)) preRoll.Clear();
                    idleTarget = current; preRoll.Enqueue((byte[])frame.Clone()); if (preRoll.Count > 10) preRoll.Dequeue(); return null;
                }
                target = current; if (idleTarget != null && idleTarget.Same(current)) frames.AddRange(preRoll); preRoll.Clear(); idleTarget = null;
            }
            if (!target.Same(current)) { DropUntilSilence(); return null; }
            frames.Add((byte[])frame.Clone()); if (speech) { voiced++; silence = 0; } else silence++;
            if (silence < 35 && frames.Count < 400) return null;
            HandsFreePhrase result = null;
            if (voiced >= 75) { int count = frames.Count - silence; var pcm = new byte[count * 640]; for (int i = 0; i < count; i++) Buffer.BlockCopy(frames[i], 0, pcm, i * 640, 640); result = new HandsFreePhrase { Pcm = pcm, Target = target }; }
            Reset(); return result;
        }
    }
    sealed class HandsFreeSession : IDisposable
    {
        readonly BlockingCollection<HandsFreePhrase> pending = new BlockingCollection<HandsFreePhrase>(1);
        readonly HandsFreeBuffer buffer = new HandsFreeBuffer(); readonly object gate = new object();
        readonly Config config; readonly MyVoiceProfile profile; readonly Func<HandsFreeTarget> target;
        readonly Action<HandsFreePhrase, string> accepted; readonly Action<string, bool> feedback;
        MicRecorder mic; volatile bool disposed; int busy;
        public int Level { get { return mic == null ? 0 : mic.SoundLevel; } }
        public HandsFreeSession(Config config, MyVoiceProfile profile, Func<HandsFreeTarget> target, Action<HandsFreePhrase, string> accepted, Action<string, bool> feedback)
        {
            this.config = config.Copy(); this.profile = profile; this.target = target; this.accepted = accepted; this.feedback = feedback;
        }
        public void Start() { mic = new MicRecorder(config.Microphone, Feed); try { mic.Start(); Task.Run((Action)Process); } catch { mic.Dispose(); mic = null; throw; } }
        void Feed(byte[] pcm)
        {
            if (disposed) return;
            lock (gate) {
                // Inference backpressure drops new audio instead of accumulating ambient speech.
                if (Volatile.Read(ref busy) != 0) { buffer.DropUntilSilence(); return; }
                for (int i = 0; i + 640 <= pcm.Length; i += 640) {
                    var frame = new byte[640]; Buffer.BlockCopy(pcm, i, frame, 0, 640);
                    var phrase = buffer.Frame(frame, target());
                    if (phrase != null && !pending.IsAddingCompleted) { buffer.DropUntilSilence(); Interlocked.Exchange(ref busy, 1); if (!pending.TryAdd(phrase)) { Interlocked.Exchange(ref busy, 0); Array.Clear(phrase.Pcm, 0, phrase.Pcm.Length); } break; }
                }
            }
        }
        void Process()
        {
            foreach (var phrase in pending.GetConsumingEnumerable()) {
                try {
                    if (disposed || !phrase.Target.Same(target())) continue;
                    feedback("Checking voice - no text is sent yet", false);
                    if (!MyVoiceMatcher.Accept(config, profile, phrase.Pcm)) { feedback("Voice not matched - phrase discarded", false); continue; }
                    if (disposed || !phrase.Target.Same(target())) continue;
                    string text = SmartCleanup.Complete(config, SpeechEngine.Transcribe(config, MicRecorder.ToWav(phrase.Pcm)), 4000);
                    if (!disposed && phrase.Target.Same(target()) && !String.IsNullOrWhiteSpace(text)) accepted(phrase, text);
                } catch (Exception ex) { if (!disposed) feedback("Hands-free paused: " + ex.Message, true); return; }
                finally { Array.Clear(phrase.Pcm, 0, phrase.Pcm.Length); Interlocked.Exchange(ref busy, 0); }
            }
        }
        public void Dispose()
        {
            disposed = true;
            if (mic != null) { mic.Dispose(); mic = null; }
            lock (gate) { buffer.Reset(); pending.CompleteAdding(); HandsFreePhrase item; while (pending.TryTake(out item)) Array.Clear(item.Pcm, 0, item.Pcm.Length); }
        }
    }
}
