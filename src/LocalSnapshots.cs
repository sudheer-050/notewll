using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
namespace LocalDictation
{
    static class LocalSnapshots
    {
        static int running; static DateTime nextCheck;
        public static string Root { get { return Path.Combine(Config.DirectoryPath, "Snapshots"); } }
        public static string LastError = "";
        public static void Schedule(Config config, bool busy)
        {
            if (!config.ScheduledBackups || busy || DateTime.UtcNow < nextCheck || Interlocked.CompareExchange(ref running, 1, 0) != 0) return;
            nextCheck = DateTime.UtcNow.AddMinutes(5);
            Task.Run(() => { try { Directory.CreateDirectory(Root); var latest = Directory.GetFiles(Root, "*.nwb").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault(); if (latest == null || File.GetLastWriteTimeUtc(latest) < DateTime.UtcNow.AddDays(-1)) Save(new NoteStore(), Path.Combine(Root, "Notewll-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".nwb")); LastError = ""; } catch (Exception ex) { LastError = ex.Message; } finally { Interlocked.Exchange(ref running, 0); } });
        }
        // AES encrypt-then-MAC. DPAPI protects the random encryption and MAC keys for this Windows account.
        public static void Save(NoteStore store, string path)
        {
            Directory.CreateDirectory(Config.DirectoryPath); string zip = Path.Combine(Config.DirectoryPath, Guid.NewGuid().ToString("N") + ".zip"), temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                NotesData.Backup(store, zip);
                using (var aes = Aes.Create()) using (var rng = RandomNumberGenerator.Create())
                {
                    aes.GenerateKey(); aes.GenerateIV(); byte[] keys = new byte[64]; Buffer.BlockCopy(aes.Key, 0, keys, 0, 32); rng.GetBytes(keys); aes.Key = keys.Take(32).ToArray();
                    try
                    {
                        byte[] wrapped = ProtectedData.Protect(keys, null, DataProtectionScope.CurrentUser);
                        using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.ReadWrite))
                        {
                            var header = new BinaryWriter(output); header.Write(0x3142574E); header.Write(wrapped.Length); header.Write(wrapped); header.Write(aes.IV); header.Flush();
                            using (var crypto = new CryptoStream(output, aes.CreateEncryptor(), CryptoStreamMode.Write, true)) using (var input = File.OpenRead(zip)) { input.CopyTo(crypto); crypto.FlushFinalBlock(); }
                            output.Position = 0; byte[] mac; using (var hmac = new HMACSHA256(keys.Skip(32).ToArray())) mac = hmac.ComputeHash(output); output.Write(mac, 0, mac.Length); output.Flush(true);
                        }
                        if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
                    }
                    finally { Array.Clear(keys, 0, keys.Length); }
                }
            }
            finally { if (File.Exists(zip)) File.Delete(zip); if (File.Exists(temp)) File.Delete(temp); }
        }
        public static T Read<T>(string path, Func<string,T> work)
        {
            if (Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase)) return work(path);
            Directory.CreateDirectory(Config.DirectoryPath); string zip = Path.Combine(Config.DirectoryPath, Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                using (var input = File.OpenRead(path))
                {
                    if (input.Length > 2L * 1024 * 1024 * 1024 || input.Length < 64) throw new IOException("Snapshot size is invalid.");
                    var header = new BinaryReader(input); if (header.ReadInt32() != 0x3142574E) throw new IOException("Unknown snapshot format."); int length = header.ReadInt32(); if (length < 1 || length > 4096) throw new IOException("Invalid snapshot key.");
                    byte[] keys = ProtectedData.Unprotect(header.ReadBytes(length), null, DataProtectionScope.CurrentUser);
                    try
                    {
                        if (keys.Length != 64) throw new IOException("Invalid snapshot key."); byte[] iv = header.ReadBytes(16); long start = input.Position, end = input.Length - 32;
                        input.Position = 0; byte[] actual; using (var hmac = new HMACSHA256(keys.Skip(32).ToArray())) { var buffer = new byte[81920]; long remaining = end; while (remaining > 0) { int n = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining)); if (n == 0) throw new EndOfStreamException(); hmac.TransformBlock(buffer, 0, n, null, 0); remaining -= n; } hmac.TransformFinalBlock(new byte[0], 0, 0); actual = hmac.Hash; }
                        byte[] expected = header.ReadBytes(32); int mismatch = expected.Length ^ actual.Length; for (int i = 0; i < Math.Min(expected.Length, actual.Length); i++) mismatch |= expected[i] ^ actual[i]; if (mismatch != 0) throw new IOException("Snapshot authentication failed. No notes restored.");
                        input.Position = start;
                        using (var aes = Aes.Create()) { aes.Key = keys.Take(32).ToArray(); aes.IV = iv; using (var output = File.Create(zip)) using (var crypto = new CryptoStream(output, aes.CreateDecryptor(), CryptoStreamMode.Write)) { var buffer = new byte[81920]; long remaining = end - start; while (remaining > 0) { int n = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining)); if (n == 0) throw new EndOfStreamException(); crypto.Write(buffer, 0, n); remaining -= n; } crypto.FlushFinalBlock(); } }
                    }
                    finally { Array.Clear(keys, 0, keys.Length); }
                }
                return work(zip);
            }
            finally { if (File.Exists(zip)) File.Delete(zip); }
        }
    }
}
