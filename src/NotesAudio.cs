using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Notewll
{
    sealed class AudioEndpoint
    {
        public string Id, Name;
        public override string ToString() { return Name; }
    }
    // Native shared-mode WASAPI: Windows performs channel mixing and resampling to 16 kHz PCM.
    // Capture and ReleaseBuffer always execute on the same recording thread.
    static class CoreAudio
    {
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class EnumeratorClass { }
        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface Enumerator
        {
            void EnumAudioEndpoints(int flow, uint mask, out Devices result);
            void GetDefaultAudioEndpoint(int flow, int role, out Device result);
            void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out Device result);
        }
        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface Devices { void GetCount(out uint count); void Item(uint index, out Device device); }
        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface Device
        {
            void Activate(ref Guid iid, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object result);
            void OpenPropertyStore(uint mode, out Properties store);
            void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
            void GetState(out uint state);
        }
        [StructLayout(LayoutKind.Sequential)] struct PropertyKey { public Guid id; public uint pid; }
        [StructLayout(LayoutKind.Explicit, Size = 24)] struct Variant { [FieldOffset(0)] public ushort type; [FieldOffset(8)] public IntPtr text; }
        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface Properties { void GetCount(out uint count); void GetAt(uint index, out PropertyKey key); void GetValue(ref PropertyKey key, out Variant value); }
        [ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface Client
        {
            void Initialize(int share, uint flags, long duration, long periodicity, ref WinMM.WaveFormat format, IntPtr session);
            void GetBufferSize(out uint frames); void GetStreamLatency(out long latency); void GetCurrentPadding(out uint padding);
            [PreserveSig] int IsFormatSupported(int share, IntPtr format, out IntPtr closest);
            void GetMixFormat(out IntPtr format); void GetDevicePeriod(out long normal, out long minimum);
            void Start(); void Stop(); void Reset(); void SetEventHandle(IntPtr handle);
            void GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
        }
        [ComImport, Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface Capture
        {
            void GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong position, out ulong qpc);
            void ReleaseBuffer(uint frames); void GetNextPacketSize(out uint frames);
        }
        [DllImport("ole32.dll")] static extern int PropVariantClear(ref Variant value);
        public static void Release(object value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
        public static List<AudioEndpoint> List(bool output)
        {
            var list = new List<AudioEndpoint> { new AudioEndpoint { Id = "", Name = output ? "Windows default output" : "Windows default microphone" } };
            Enumerator enumerator = null; Devices devices = null;
            try
            {
                enumerator = (Enumerator)new EnumeratorClass(); enumerator.EnumAudioEndpoints(output ? 0 : 1, 1, out devices);
                uint count; devices.GetCount(out count);
                for (uint i = 0; i < count; i++)
                {
                    Device device = null; Properties properties = null; Variant value = new Variant();
                    try
                    {
                        devices.Item(i, out device); string id; device.GetId(out id); device.OpenPropertyStore(0, out properties);
                        var key = new PropertyKey { id = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), pid = 14 };
                        properties.GetValue(ref key, out value);
                        list.Add(new AudioEndpoint { Id = id, Name = value.type == 31 ? Marshal.PtrToStringUni(value.text) : "Audio device " + i });
                    }
                    finally { PropVariantClear(ref value); Release(properties); Release(device); }
                }
            }
            finally { Release(devices); Release(enumerator); }
            return list;
        }
        public static string DefaultId(bool output)
        {
            Enumerator enumerator = null; Device device = null;
            try { enumerator = (Enumerator)new EnumeratorClass(); enumerator.GetDefaultAudioEndpoint(output ? 0 : 1, 0, out device); string id; device.GetId(out id); return id; }
            finally { Release(device); Release(enumerator); }
        }
        public static Client Open(bool output, string endpoint)
        {
            Enumerator enumerator = null; Device device = null;
            try
            {
                enumerator = (Enumerator)new EnumeratorClass();
                if (String.IsNullOrEmpty(endpoint)) enumerator.GetDefaultAudioEndpoint(output ? 0 : 1, 0, out device);
                else enumerator.GetDevice(endpoint, out device);
                object obj; var iid = typeof(Client).GUID; device.Activate(ref iid, 23, IntPtr.Zero, out obj);
                return (Client)obj;
            }
            finally { Release(device); Release(enumerator); }
        }
    }
    sealed class NotesCapture : IDisposable
    {
        CoreAudio.Client client;
        CoreAudio.Capture capture;
        long nextFrame;
        readonly long epoch;
        public NotesCapture(bool output, string endpoint, long epochTicks)
        {
            epoch = epochTicks;
            try
            {
                client = CoreAudio.Open(output, endpoint);
                var format = new WinMM.WaveFormat { tag = 1, channels = 1, samplesPerSec = 16000, avgBytesPerSec = 32000, blockAlign = 2, bitsPerSample = 16 };
                client.Initialize(0, 0x88000000u | (output ? 0x20000u : 0), 10000000, 0, ref format, IntPtr.Zero);
                object obj; var iid = typeof(CoreAudio.Capture).GUID; client.GetService(ref iid, out obj); capture = (CoreAudio.Capture)obj;
            }
            catch { Dispose(); throw; }
        }
        public void Start() { client.Start(); }
        public void Drain(Action<long, byte[]> consume)
        {
            uint available; capture.GetNextPacketSize(out available);
            while (available > 0)
            {
                IntPtr data; uint frames, flags; ulong position, qpc;
                capture.GetBuffer(out data, out frames, out flags, out position, out qpc);
                try
                {
                    var pcm = new byte[checked((int)frames * 2)];
                    if ((flags & 2) == 0 && data != IntPtr.Zero) Marshal.Copy(data, pcm, 0, pcm.Length);
                    long start = (flags & 4) == 0 && qpc > 0 ? (long)Math.Round(((double)qpc / 10000000 - (double)epoch / Stopwatch.Frequency) * 16000) : nextFrame;
                    // Sub-millisecond timestamp jitter must not create overlapping fragments or gaps.
                    if (Math.Abs(start - nextFrame) < 160) start = nextFrame;
                    if (start < 0) start = 0;
                    consume(start, pcm); nextFrame = start + frames;
                }
                finally { capture.ReleaseBuffer(frames); }
                capture.GetNextPacketSize(out available);
            }
        }
        public void Dispose()
        {
            if (client != null) { try { client.Stop(); } catch { } }
            CoreAudio.Release(capture); CoreAudio.Release(client); capture = null; client = null;
        }
    }
}
