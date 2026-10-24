using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace LocalDictation
{
    sealed class MicException : Exception
    {
        public MicException(string message) : base(message) { }
    }

    static class WinMM
    {
        public const int WAVE_MAPPER = -1, CALLBACK_EVENT = 0x50000, WHDR_DONE = 1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WaveInCaps
        {
            public ushort mid, pid; public uint driverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string name;
            public uint formats; public ushort channels, reserved;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct WaveFormat { public ushort tag, channels; public uint samplesPerSec, avgBytesPerSec; public ushort blockAlign, bitsPerSample, size; }
        [StructLayout(LayoutKind.Sequential)]
        public struct WaveHdr { public IntPtr data; public uint length, recorded; public IntPtr user; public uint flags, loops; public IntPtr next, reserved; }

        [DllImport("winmm.dll")] public static extern int waveInGetNumDevs();
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)] public static extern int waveInGetDevCaps(IntPtr id, ref WaveInCaps caps, int size);
        [DllImport("winmm.dll")] public static extern int waveInOpen(out IntPtr h, int id, ref WaveFormat fmt, IntPtr cb, IntPtr inst, int flags);
        [DllImport("winmm.dll")] public static extern int waveInPrepareHeader(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] public static extern int waveInUnprepareHeader(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] public static extern int waveInAddBuffer(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] public static extern int waveInStart(IntPtr h);
        [DllImport("winmm.dll")] public static extern int waveInStop(IntPtr h);
        [DllImport("winmm.dll")] public static extern int waveInReset(IntPtr h);
        [DllImport("winmm.dll")] public static extern int waveInClose(IntPtr h);
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)] public static extern int waveInGetErrorText(int err, StringBuilder text, int size);
    }

    static class Mics
    {
        public static List<string> List()
        {
            var names = new List<string>();
            int count = WinMM.waveInGetNumDevs();
            for (int i = 0; i < count; i++)
            {
                var caps = new WinMM.WaveInCaps();
                names.Add(WinMM.waveInGetDevCaps((IntPtr)i, ref caps, Marshal.SizeOf(typeof(WinMM.WaveInCaps))) == 0 ? caps.name : "Device " + i);
            }
            return names;
        }

        public static string Describe(string saved, out int index, out bool fellBack)
        {
            var names = List();
            index = DeviceSelector.Resolve(saved, names, out fellBack);
            return index >= 0 ? names[index] : "Windows default microphone";
        }
    }

    // 16 kHz mono 16-bit PCM captured in memory only; nothing is written to disk.
    sealed class MicRecorder : IDisposable
    {
        const int Rate = 16000, BufferCount = 4, BufferBytes = 3200;
        readonly int deviceIndex;
        readonly string deviceName;
        readonly MemoryStream pcm = new MemoryStream();
        readonly Action<byte[]> chunks;
        readonly ManualResetEvent signal = new ManualResetEvent(false);
        readonly IntPtr[] headers = new IntPtr[BufferCount];
        readonly IntPtr[] buffers = new IntPtr[BufferCount];
        readonly int hdrSize = Marshal.SizeOf(typeof(WinMM.WaveHdr));
        IntPtr handle;
        Thread pump;
        volatile bool running;
        int level, peak, soundLevel;

        public string DeviceName { get { return deviceName; } }
        public bool FellBack { get; private set; }
        // 0..32767 peak of the most recent buffer, for level meters.
        public int Level { get { return level; } }
        public int SoundLevel { get { return soundLevel; } }
        public int Peak { get { return peak; } }
        public int Bytes { get { lock (pcm) return (int)pcm.Length; } }

        public MicRecorder(string saved, Action<byte[]> chunks = null)
        {
            this.chunks = chunks;
            bool fell;
            deviceName = Mics.Describe(saved, out deviceIndex, out fell);
            FellBack = fell;
        }

        string Err(int rc, string what)
        {
            var text = new StringBuilder(256);
            WinMM.waveInGetErrorText(rc, text, text.Capacity);
            return what + " on '" + deviceName + "' failed: " + text + " (MMSYSERR " + rc + ")";
        }

        public void Start()
        {
            if (WinMM.waveInGetNumDevs() == 0) throw new MicException("No audio capture devices were found. Plug in or enable a microphone in Windows Sound settings.");
            var fmt = new WinMM.WaveFormat { tag = 1, channels = 1, samplesPerSec = Rate, avgBytesPerSec = Rate * 2, blockAlign = 2, bitsPerSample = 16, size = 0 };
            int rc = WinMM.waveInOpen(out handle, deviceIndex, ref fmt, signal.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, WinMM.CALLBACK_EVENT);
            if (rc != 0) { handle = IntPtr.Zero; throw new MicException(Err(rc, "Opening the microphone")); }
            try
            {
                for (int i = 0; i < BufferCount; i++)
                {
                    buffers[i] = Marshal.AllocHGlobal(BufferBytes);
                    headers[i] = Marshal.AllocHGlobal(hdrSize);
                    Marshal.StructureToPtr(new WinMM.WaveHdr { data = buffers[i], length = BufferBytes }, headers[i], false);
                    if ((rc = WinMM.waveInPrepareHeader(handle, headers[i], hdrSize)) != 0) throw new MicException(Err(rc, "Preparing the capture buffer"));
                    if ((rc = WinMM.waveInAddBuffer(handle, headers[i], hdrSize)) != 0) throw new MicException(Err(rc, "Queueing the capture buffer"));
                }
                running = true;
                if ((rc = WinMM.waveInStart(handle)) != 0) { running = false; throw new MicException(Err(rc, "Starting capture")); }
            }
            catch { Release(); throw; }
            pump = new Thread(Pump) { IsBackground = true };
            pump.Start();
        }

        void Drain(bool requeue)
        {
            for (int i = 0; i < BufferCount; i++)
            {
                var hdr = (WinMM.WaveHdr)Marshal.PtrToStructure(headers[i], typeof(WinMM.WaveHdr));
                if ((hdr.flags & WinMM.WHDR_DONE) == 0) continue;
                int n = (int)hdr.recorded, top = 0;
                if (n > 0)
                {
                    var chunk = new byte[n];
                    Marshal.Copy(hdr.data, chunk, 0, n);
                    double squares = 0;
                    for (int s = 0; s + 1 < n; s += 2) { int v = Math.Abs((int)BitConverter.ToInt16(chunk, s)); if (v > top) top = v; squares += (double)v * v; }
                    soundLevel = (int)Math.Sqrt(squares / Math.Max(1, n / 2));
                    AppendChunk(chunk);
                    level = top; if (top > peak) peak = top;
                }
                if (requeue) { hdr.recorded = 0; Marshal.StructureToPtr(hdr, headers[i], false); WinMM.waveInAddBuffer(handle, headers[i], hdrSize); }
            }
        }

        void Pump()
        {
            while (running) { signal.WaitOne(100); signal.Reset(); if (running) Drain(true); }
        }

        internal void AppendChunk(byte[] chunk) { if (chunks == null) { lock (pcm) pcm.Write(chunk, 0, chunk.Length); } else chunks(chunk); }

        public byte[] Snapshot() { lock (pcm) return pcm.ToArray(); }

        // Stops capture and returns the PCM bytes (16 kHz mono 16-bit).
        public byte[] Stop()
        {
            running = false; signal.Set();
            if (pump != null) pump.Join(1000);
            if (handle != IntPtr.Zero) { WinMM.waveInStop(handle); WinMM.waveInReset(handle); Drain(false); }
            Release();
            lock (pcm) return pcm.ToArray();
        }

        void Release()
        {
            if (handle != IntPtr.Zero)
            {
                for (int i = 0; i < BufferCount; i++) if (headers[i] != IntPtr.Zero) WinMM.waveInUnprepareHeader(handle, headers[i], hdrSize);
                WinMM.waveInClose(handle); handle = IntPtr.Zero;
            }
            for (int i = 0; i < BufferCount; i++)
            {
                if (headers[i] != IntPtr.Zero) { Marshal.FreeHGlobal(headers[i]); headers[i] = IntPtr.Zero; }
                if (buffers[i] != IntPtr.Zero) { Marshal.FreeHGlobal(buffers[i]); buffers[i] = IntPtr.Zero; }
            }
        }

        public void Dispose() { if (running || handle != IntPtr.Zero) { try { Stop(); } catch { } } signal.Close(); }

        // Quiet microphones make the recognizer return nothing. Lift the peak toward ~75% of full scale
        // (gain capped at 10x so room noise is not boosted without limit). Louder audio is untouched.
        public static byte[] Normalize(byte[] pcmBytes, int peak)
        {
            if (peak < 150 || peak >= 12000) return pcmBytes;
            double gain = Math.Min(10.0, 24000.0 / peak);
            var output = new byte[pcmBytes.Length];
            for (int i = 0; i + 1 < pcmBytes.Length; i += 2)
            {
                int v = (int)Math.Round(BitConverter.ToInt16(pcmBytes, i) * gain);
                BitConverter.GetBytes((short)Math.Max(-32768, Math.Min(32767, v))).CopyTo(output, i);
            }
            return output;
        }

        public static byte[] ToWav(byte[] pcmBytes)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + pcmBytes.Length); w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data")); w.Write(pcmBytes.Length); w.Write(pcmBytes);
                return ms.ToArray();
            }
        }
    }
}
