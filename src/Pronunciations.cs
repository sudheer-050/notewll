using System;
using System.IO;
using System.Linq;
namespace Notewll
{
    static class Pronunciations
    {
        internal const int MaximumBytes = 16000 * 2 * 5;
        internal static string Root { get { return Path.Combine(Config.DirectoryPath, "Pronunciations"); } }
        internal static string PathFor(string id)
        { Guid parsed; if (!Guid.TryParseExact(id, "N", out parsed)) throw new InvalidDataException("Invalid pronunciation identifier."); return Path.Combine(Root, id + ".wav"); }
        internal static void Validate(byte[] pcm)
        { if (pcm == null || pcm.Length < 16000 || pcm.Length > MaximumBytes || pcm.Length % 2 != 0) throw new InvalidDataException("Record between half a second and five seconds of pronunciation audio."); }
        internal static byte[] Load(string id)
        { string path = PathFor(id); if (new FileInfo(path).Length > MaximumBytes + 44) throw new InvalidDataException("Pronunciation recording is too long."); var pcm = SpeechEngine.PcmFromWav(File.ReadAllBytes(path)); Validate(pcm); return pcm; }
        internal static void Commit(PersonalRule rule)
        {
            if (rule.PronunciationPcm == null) return;
            Validate(rule.PronunciationPcm); string path = PathFor(rule.PronunciationId); byte[] wav = MicRecorder.ToWav(rule.PronunciationPcm);
            Directory.CreateDirectory(Root);
            if (File.Exists(path)) { if (!File.ReadAllBytes(path).SequenceEqual(wav)) throw new IOException("The saved pronunciation does not match this recording. Record it again."); return; }
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllBytes(temporary, wav); File.Move(temporary, path); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
