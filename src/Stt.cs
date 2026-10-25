using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

namespace Notewll
{
    sealed class SttException : Exception { public SttException(string message) : base(message) { } }
    static class VoskNative
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
        [DllImport("libvosk.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void vosk_set_log_level(int level);
        [DllImport("libvosk.dll", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr vosk_model_new(byte[] path);
        [DllImport("libvosk.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void vosk_model_free(IntPtr model);
        [DllImport("libvosk.dll", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr vosk_recognizer_new(IntPtr model, float rate);
        [DllImport("libvosk.dll", CallingConvention = CallingConvention.Cdecl)] public static extern int vosk_recognizer_accept_waveform(IntPtr recognizer, byte[] pcm, int length);
        [DllImport("libvosk.dll", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr vosk_recognizer_result(IntPtr recognizer);
        [DllImport("libvosk.dll", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr vosk_recognizer_partial_result(IntPtr recognizer);
        [DllImport("libvosk.dll", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr vosk_recognizer_final_result(IntPtr recognizer);
        [DllImport("libvosk.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void vosk_recognizer_free(IntPtr recognizer);
        public static string Text(IntPtr json, string key)
        {
            int size = 0; while (size < 1000000 && Marshal.ReadByte(json, size) != 0) size++;
            var bytes = new byte[size]; Marshal.Copy(json, bytes, 0, size);
            var result = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(bytes));
            object value; return result.TryGetValue(key, out value) ? Convert.ToString(value) : "";
        }
    }
    // The native model is shared; each recording owns a separate streaming recognizer.
    static class SpeechEngine
    {
        static readonly object gate = new object();
        static IntPtr model;
        static int streams;
        static bool closing;
        static string root = AppDomain.CurrentDomain.BaseDirectory;
        internal static void DiagnosticRoot(string path) { lock (gate) { if (streams != 0 || model != IntPtr.Zero) throw new InvalidOperationException("The engine is already in use."); root = Path.GetFullPath(path); } }
        internal static int ActiveStreams { get { lock (gate) return streams; } }
        public static bool Available { get { return File.Exists(Path.Combine(root, "native", "libvosk.dll")) && File.Exists(Path.Combine(root, "model-small-en-us", "am", "final.mdl")); } }
        public static bool Healthy(Config config, out string detail)
        {
            if (config != null && !Config.IsLoopback(config.SttUrl)) { detail = "Legacy STT addresses must be loopback; this app transcribes locally."; return false; }
            detail = Available ? "Vosk Small English is installed (offline, CPU)." : "The small English speech model is missing. Run Notewll Setup to install it.";
            return Available;
        }
        public static NativeSpeechStream CreateStream()
        {
            lock (gate)
            {
                if (closing) throw new SttException("The speech engine is shutting down.");
                if (model == IntPtr.Zero)
                {
                    string detail; if (!Healthy(null, out detail)) throw new SttException(detail);
                    string library = Path.Combine(root, "native", "libvosk.dll");
                    if (VoskNative.LoadLibraryEx(library, IntPtr.Zero, 8) == IntPtr.Zero) throw new SttException("Could not load the bundled speech engine (Windows error " + Marshal.GetLastWin32Error() + "). Run Setup again.");
                    VoskNative.vosk_set_log_level(-1);
                    model = VoskNative.vosk_model_new(Encoding.UTF8.GetBytes(Path.Combine(root, "model-small-en-us") + "\0"));
                    if (model == IntPtr.Zero) throw new SttException("Could not load the English speech model. Run Setup again.");
                }
                var stream = new NativeSpeechStream(model, ReleaseStream); streams++; return stream;
            }
        }
        public static string EnsureRunning(Config config, int seconds, Action<string> progress = null)
        {
            try { if (progress != null) progress("Loading the small English speech model..."); using (var stream = CreateStream()) { } return null; }
            catch (Exception ex) { return ex.Message; }
        }
        public static byte[] PcmFromWav(byte[] wav)
        {
            using (var reader = new BinaryReader(new MemoryStream(wav)))
            {
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF") throw new SttException("Expected PCM WAV audio.");
                reader.ReadUInt32(); if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE") throw new SttException("Expected PCM WAV audio.");
                bool valid = false;
                while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                {
                    string id = Encoding.ASCII.GetString(reader.ReadBytes(4)); uint length = reader.ReadUInt32(); long end = reader.BaseStream.Position + length;
                    if (end > reader.BaseStream.Length) throw new SttException("Incomplete WAV audio.");
                    if (id == "fmt " && length >= 16) { valid = reader.ReadUInt16() == 1 && reader.ReadUInt16() == 1 && reader.ReadUInt32() == 16000; reader.ReadUInt32(); reader.ReadUInt16(); valid &= reader.ReadUInt16() == 16; }
                    if (id == "data") { if (!valid) throw new SttException("Expected 16 kHz mono 16-bit PCM audio."); return reader.ReadBytes(checked((int)length)); }
                    reader.BaseStream.Position = end + (length % 2);
                }
                throw new SttException("WAV audio has no data.");
            }
        }
        public static string Transcribe(Config config, byte[] wav)
        {
            byte[] pcm = PcmFromWav(wav);
            using (var stream = CreateStream())
            {
                for (int i = 0; i < pcm.Length; i += 8000) { var chunk = new byte[Math.Min(8000, pcm.Length - i)]; Buffer.BlockCopy(pcm, i, chunk, 0, chunk.Length); stream.Feed(chunk); }
                return stream.Finish();
            }
        }
        public static string TranscribeEnsured(Config config, byte[] wav, Action<string> progress = null) { return Transcribe(config, wav); }
        static void ReleaseStream()
        {
            lock (gate) { streams--; if (closing && streams == 0) FreeModel(); }
        }
        static void FreeModel() { if (model != IntPtr.Zero) { VoskNative.vosk_model_free(model); model = IntPtr.Zero; } }
        public static void Shutdown() { lock (gate) { closing = true; if (streams == 0) FreeModel(); } }
    }
    sealed class NativeSpeechStream : IDisposable
    {
        IntPtr recognizer;
        string complete = "";
        Action released;
        public NativeSpeechStream(IntPtr model, Action released) { this.released = released; recognizer = VoskNative.vosk_recognizer_new(model, 16000); if (recognizer == IntPtr.Zero) throw new SttException("Could not create a speech recognizer."); }
        public string Feed(byte[] pcm)
        {
            int result = VoskNative.vosk_recognizer_accept_waveform(recognizer, pcm, pcm.Length);
            if (result < 0) throw new SttException("The speech recognizer could not process audio.");
            if (result == 1) complete = (complete + " " + VoskNative.Text(VoskNative.vosk_recognizer_result(recognizer), "text")).Trim();
            return (complete + " " + VoskNative.Text(VoskNative.vosk_recognizer_partial_result(recognizer), "partial")).Trim();
        }
        public string DrainCompleted() { string result = complete; complete = ""; return result; }
        public string Finish() { return (complete + " " + VoskNative.Text(VoskNative.vosk_recognizer_final_result(recognizer), "text")).Trim(); }
        public void Dispose() { if (recognizer != IntPtr.Zero) { VoskNative.vosk_recognizer_free(recognizer); recognizer = IntPtr.Zero; if (released != null) { released(); released = null; } } }
    }
    sealed class LiveRecognition : IDisposable
    {
        readonly object gate = new object(); NativeSpeechStream stream; int position; volatile bool disposed;
        readonly Config accurate;
        public LiveRecognition(Config accurate = null) { this.accurate = accurate; }
        public string Snapshot(byte[] pcm, int peak, bool final)
        {
            lock (gate)
            {
                if (disposed) return "";
                if (accurate != null) return AccurateSpeech.Transcribe(accurate, pcm);
                if (stream == null) stream = SpeechEngine.CreateStream();
                var delta = new byte[Math.Max(0, pcm.Length - position)]; Buffer.BlockCopy(pcm, position, delta, 0, delta.Length); position = pcm.Length;
                string partial = stream.Feed(MicRecorder.Normalize(delta, peak)); return final ? stream.Finish() : partial;
            }
        }
        public void Dispose()
        {
            disposed = true;
            // Cancel must not freeze the UI behind an in-flight local inference.
            if (System.Threading.Monitor.TryEnter(gate))
            { try { if (stream != null) stream.Dispose(); } finally { System.Threading.Monitor.Exit(gate); } }
            else System.Threading.ThreadPool.QueueUserWorkItem(_ => { lock (gate) { if (stream != null) stream.Dispose(); } });
        }
    }
}
