using System;

namespace Notewll
{
    // Conservative level gate, not speaker identification or a full speech detector.
    // Keep the original timeline and avoid amplifying room noise into words.
    static class QuietAudio
    {
        public static byte[] Filter(byte[] pcm)
        {
            var result = (byte[])pcm.Clone();
            const int window = 640; // 20 ms of mono PCM16 at 16 kHz.
            var active = new bool[(pcm.Length + window - 1) / window];
            for (int n = 0; n < active.Length; n++)
            {
                double energy = 0; int count = 0;
                for (int i = n * window; i + 1 < Math.Min(pcm.Length, (n + 1) * window); i += 2)
                {
                    int sample = BitConverter.ToInt16(pcm, i); energy += (double)sample * sample; count++;
                }
                active[n] = count > 0 && Math.Sqrt(energy / count) >= 100;
            }
            for (int n = 0; n < active.Length; n++)
            {
                bool keep = false;
                // Preserve 100 ms of context at speech boundaries.
                for (int j = Math.Max(0, n - 5); j <= Math.Min(active.Length - 1, n + 5); j++) keep |= active[j];
                if (!keep) Array.Clear(result, n * window, Math.Min(window, result.Length - n * window));
            }
            return result;
        }
        public static bool HasSignal(byte[] pcm)
        {
            int samples = 0;
            for (int i = 0; i + 1 < pcm.Length; i += 2) if (Math.Abs((int)BitConverter.ToInt16(pcm, i)) >= 100) samples++;
            return samples >= 800; // Reject isolated clicks under 50 ms.
        }
    }
}
