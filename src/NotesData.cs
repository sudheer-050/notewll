using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace Notewll
{
    static class NotesData
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        static extern bool MoveFileEx(string source, string destination, int flags);
        internal static void CommitFile(string source, string destination)
        { if (!MoveFileEx(source, destination, 1 | 8)) throw new IOException("Could not commit the saved file: " + new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error()).Message); }
        public static void WriteRules(string path, List<PersonalRule> rules)
        {
            var copy = rules.Select(r => r.Copy()).ToList(); PersonalRule.Validate(copy);
            string temporary = Path.GetFullPath(path) + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(copy), new UTF8Encoding(false)); CommitFile(temporary, path); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public static List<PersonalRule> ReadRules(string path)
        {
            if (new FileInfo(path).Length > 3 * 1024 * 1024) throw new IOException("Import limit is 3 MB.");
            var rules = new JavaScriptSerializer().Deserialize<List<PersonalRule>>(File.ReadAllText(path));
            if (rules == null || rules.Count > 1000) throw new IOException("Import at most 1,000 entries.");
            PersonalRule.Validate(rules); return rules;
        }
        public static long Bytes(string path)
        {
            if (!Directory.Exists(path)) return 0;
            long total = 0;
            foreach (var item in new DirectoryInfo(path).EnumerateFileSystemInfos())
            {
                if ((item.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                var file = item as FileInfo; total += file == null ? Bytes(item.FullName) : file.Length;
            }
            return total;
        }
        internal static long ApplicationBytes(string root)
        {
            long total = Bytes(Path.Combine(root, "native")) + Bytes(Path.Combine(root, "model-small-en-us"));
            foreach (string file in new[] { "Notewll.exe", "LocalDictation.exe", "Uninstall.exe", "THIRD_PARTY_NOTICES.md" }) { string path = Path.Combine(root, file); if (File.Exists(path)) total += new FileInfo(path).Length; }
            return total;
        }
        public static string Size(long bytes) { return (bytes / 1048576.0).ToString("0.0") + " MB"; }
        public static void DeleteTree(string path, string allowedRoot)
        {
            path = Path.GetFullPath(path); allowedRoot = Path.GetFullPath(allowedRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Unsafe cleanup path.");
            foreach (string child in Directory.GetDirectories(path)) DeleteTree(child, allowedRoot);
            foreach (string file in Directory.GetFiles(path)) { if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked files cannot be removed here."); File.Delete(file); }
            Directory.Delete(path);
        }
        public static void Backup(NoteStore store, string path)
        {
            string full = Path.GetFullPath(path), temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write)) using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
                foreach (var note in store.List(false).Where(n => n.Status != "Recording" && n.Status != "Paused" && n.Status != "Transcribing")) lock (note)
                    foreach (string source in Directory.GetFiles(store.Folder(note)))
                        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) == 0) zip.CreateEntryFromFile(source, note.Id + "/" + Path.GetFileName(source), CompressionLevel.Fastest);
            if (File.Exists(full)) File.Replace(temporary, full, null, true); else File.Move(temporary, full);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public static string RestorePreview(NoteStore store, string path)
        {
            int count = Restore(store, path, true);
            using (var zip = ZipFile.OpenRead(path))
            {
                var titles = new List<string>();
                foreach (var entry in zip.Entries.Where(e => e.FullName.Replace('\\', '/').EndsWith("/note.json", StringComparison.Ordinal))) using (var reader = new StreamReader(entry.Open()))
                {
                    var note = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.Deserialize<NoteDocument>(reader.ReadToEnd());
                    titles.Add((Directory.Exists(store.Folder(note)) ? "Skip existing: " : "Restore: ") + note.Title);
                }
                return count + " new notes; " + NotesData.Size(zip.Entries.Sum(e => e.Length)) + " uncompressed. Existing notes are kept.\r\n\r\n" + String.Join("\r\n", titles.Take(20)) + (titles.Count > 20 ? "\r\n...and " + (titles.Count - 20) + " more." : "");
            }
        }
        public static int Restore(NoteStore store, string path, bool preview = false)
        {
            Directory.CreateDirectory(store.Root); string staging = Path.Combine(store.Root, ".import-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(staging);
            int imported = 0;
            try
            {
                using (var zip = ZipFile.OpenRead(path))
                {
                    if (zip.Entries.Count > 10000 || zip.Entries.Sum(e => e.Length) > 2L * 1024 * 1024 * 1024) throw new IOException("Backup exceeds the 2 GB / 10,000-file import limit.");
                    foreach (var entry in zip.Entries)
                    {
                        string[] parts = entry.FullName.Replace('\\', '/').Split('/'); Guid id;
                        if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out id) || parts[1] != Path.GetFileName(parts[1]) || parts[1].IndexOf(':') >= 0 || !new[] { "note.json", "note.json.tmp", "notes.md", "notes.md.tmp", "microphone.wav", "computer.wav" }.Contains(parts[1])) throw new IOException("Backup contains an invalid entry.");
                        string folder = Path.Combine(staging, parts[0]); Directory.CreateDirectory(folder); entry.ExtractToFile(Path.Combine(folder, parts[1]), false);
                    }
                }
                foreach (string folder in Directory.GetDirectories(staging))
                {
                    string metadata = Path.Combine(folder, "note.json"); if (!File.Exists(metadata)) throw new IOException("A backup note has no metadata.");
                    if (new FileInfo(metadata).Length > 32 * 1024 * 1024) throw new IOException("A backup note has oversized metadata.");
                    var note = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.Deserialize<NoteDocument>(File.ReadAllText(metadata));
                    if (note == null || note.Id != Path.GetFileName(folder) || note.Segments == null || note.Participants == null) throw new IOException("Backup note metadata is invalid.");
                    NoteStore.Validate(note);
                    // Ignore recovery snapshots supplied by imports; validated note.json is authoritative.
                    foreach (string snapshot in Directory.GetFiles(folder, "*.tmp")) File.Delete(snapshot);
                }
                foreach (string folder in Directory.GetDirectories(staging))
                {
                    string target = Path.Combine(store.Root, Path.GetFileName(folder)); if (Directory.Exists(target)) continue;
                    if (!preview) Directory.Move(folder, target); imported++;
                }
                return imported;
            }
            finally { if (Directory.Exists(staging)) DeleteTree(staging, store.Root); }
        }
    }
}
