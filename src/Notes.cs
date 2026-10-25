using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Notewll
{
    sealed class NoteSegment
    {
        public string Source { get; set; }
        public long StartFrame { get; set; }
        public long? TranscriptStartFrame { get; set; }
        public int Frames { get; set; }
        public string Text { get; set; }
        public string RawText { get; set; }
        public string Introduction { get; set; }
        public string SpeakerLabel { get; set; }
        public string VoiceId { get; set; }
        public double[] VoiceEmbedding { get; set; }
        public string Error { get; set; }
        public bool Complete { get; set; }
    }
    sealed class NoteDocument
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string CreatedUtc { get; set; }
        public bool AutoTitle { get; set; }
        public bool NameEdited { get; set; }
        public string Mode { get; set; }
        public string Kind { get; set; }
        public string Platform { get; set; }
        public string TargetWindowTitle { get; set; }
        public string UserName { get; set; }
        public string WrittenNotes { get; set; }
        public bool EditableTextInitialized { get; set; }
        public string Summary { get; set; }
        public string Tags { get; set; }
        public string Project { get; set; }
        public List<NoteBookmark> Bookmarks { get; set; }
        public List<NoteTask> Tasks { get; set; }
        public string DeletedUtc { get; set; }
        public string Status { get; set; }
        public string CaptureError { get; set; }
        public double DurationSeconds { get; set; }
        public List<string> Participants { get; set; }
        public List<NoteSegment> Segments { get; set; }
        public NoteDocument() { Bookmarks = new List<NoteBookmark>(); Tasks = new List<NoteTask>(); Project = ""; Participants = new List<string>(); Segments = new List<NoteSegment>(); WrittenNotes = ""; Summary = Tags = ""; UserName = ""; }
    }
    static class Introductions
    {
        static readonly HashSet<string> NotNames = new HashSet<string>(new[] { "a", "an", "the", "not", "here", "going", "doing", "working", "happy", "glad", "ready", "sorry", "sure", "fine", "good", "well", "late", "back", "looking", "trying", "speaking", "calling", "recording", "taking", "thinking", "from", "in", "on", "at", "with", "and", "just", "very", "so", "really", "still", "also", "having", "being", "getting", "waiting", "excited", "tired", "interested", "available", "unable", "responsible", "aware", "done", "new", "only", "about", "using", "afraid", "confused", "concerned", "meeting", "offline", "online", "free", "busy", "okay", "ok" }, StringComparer.OrdinalIgnoreCase);
        public static List<string> Find(string text)
        {
            var names = new List<string>();
            // Deliberately use explicit introductions. This is name extraction, not voice identification.
            foreach (Match match in Regex.Matches(text ?? "", @"(?i)\b(?:my name is|i am|i['\u2019]m)\s+([\p{L}][\p{L}\p{M}'\u2019-]{1,39})\b"))
            {
                string word = match.Groups[1].Value;
                if (NotNames.Contains(word) || word.EndsWith("ing", StringComparison.OrdinalIgnoreCase)) continue;
                string name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(word.ToLowerInvariant());
                if (!names.Contains(name)) names.Add(name);
            }
            return names;
        }
    }
    static class NoteMetadata
    {
        public static void UpdateTitle(NoteDocument note)
        {
            if (!note.AutoTitle) return;
            string text = String.Join(" ", note.Segments.OrderBy(s => s.StartFrame).Where(s => s.Complete).Select(s => s.RawText ?? s.Text));
            if (String.IsNullOrWhiteSpace(text)) text = note.WrittenNotes ?? "";
            text = Regex.Replace(text, @"(?i)^(?:(?:hi|hello|hey)[,.!]?\s+)?(?:my name is|i am|i['?]m)\s+[\p{L}\p{M}'?-]+[,.!]?\s*", "");
            text = Regex.Replace(text, @"(?i)^(?:um|uh|okay|so|well)[,.!]?\s+", "").Trim();
            var words = Regex.Matches(text, @"[\p{L}\p{N}][\p{L}\p{N}'?-]*").Cast<Match>().Select(m => m.Value).Take(8).ToArray();
            if (words.Length < 3) return;
            string title = String.Join(" ", words); title = Char.ToUpperInvariant(title[0]) + title.Substring(1);
            note.Title = title.Length > 60 ? title.Substring(0, 57).TrimEnd() + "..." : title;
        }
    }
    sealed class NoteStore
    {
        public readonly string Root;
        readonly object gate = new object();
        public NoteStore(string root = null) { Root = Path.GetFullPath(root ?? Path.Combine(Config.DirectoryPath, "Notes")); }
        public string Folder(NoteDocument note)
        {
            Guid id; if (!Guid.TryParseExact(note.Id, "N", out id)) throw new InvalidDataException("Invalid note identifier.");
            return Path.Combine(Root, note.Id);
        }
        public string AudioPath(NoteDocument note, string source)
        {
            if (source != "Microphone" && source != "Computer") throw new InvalidDataException("Invalid audio source.");
            return Path.Combine(Folder(note), source.ToLowerInvariant() + ".wav");
        }
        public NoteDocument Create(string title, string mode, string userName)
        {
            if (mode != "Microphone" && mode != "Computer" && mode != "Both") throw new InvalidDataException("Choose an audio source.");
            var note = new NoteDocument { Id = Guid.NewGuid().ToString("N"), CreatedUtc = DateTime.UtcNow.ToString("o"), AutoTitle = String.IsNullOrWhiteSpace(title), Title = String.IsNullOrWhiteSpace(title) ? (mode == "Microphone" ? "Voice note" : "Meeting") + " \u00b7 " + DateTime.Now.ToString("MMM d, HH:mm") : title.Trim(), Mode = mode, Kind = mode == "Microphone" ? "Note" : "Meeting", UserName = userName ?? "", Status = "Recording" };
            Save(note); return note;
        }
        public void Save(NoteDocument note)
        {
            lock (note) lock (gate)
            {
                Validate(note);
                Directory.CreateDirectory(Folder(note));
                string path = Path.Combine(Folder(note), "note.json");
                AtomicWrite(path, new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.Serialize(note));
                AtomicWrite(Path.Combine(Folder(note), "notes.md"), Markdown(note));
            }
        }
        static void AtomicWrite(string path, string text)
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, text, new UTF8Encoding(false));
            // Keep the completed snapshot as a recovery copy if an overwrite is interrupted.
            // File.Replace also copies ACL metadata, which restricted Windows accounts cannot do.
            File.Copy(temporary, path, true);
        }
        public List<NoteDocument> List(bool recover = true)
        {
            var notes = new List<NoteDocument>(); if (!Directory.Exists(Root)) return notes;
            foreach (string folder in Directory.GetDirectories(Root).Where(f => { Guid id; return Guid.TryParseExact(Path.GetFileName(f), "N", out id); }))
            {
                string path = Path.Combine(folder, "note.json"); if (!File.Exists(path)) continue;
                try
                {
                    NoteDocument note;
                    try { note = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.Deserialize<NoteDocument>(File.ReadAllText(File.Exists(path + ".tmp") ? path + ".tmp" : path)); }
                    catch (ArgumentException) { note = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.Deserialize<NoteDocument>(File.ReadAllText(path)); }
                    Validate(note); Folder(note);
                    if (recover && (note.Status == "Recording" || note.Status == "Paused" || note.Status == "Transcribing")) { RecoverAudio(note); note.Status = "Interrupted - audio kept"; Save(note); }
                    notes.Add(note);
                }
                catch (IOException) { }
                catch (ArgumentException) { }
                catch (InvalidOperationException) { }
            }
            return notes.OrderByDescending(n => n.CreatedUtc).ToList();
        }
        public void Delete(NoteDocument note)
        {
            lock (note) lock (gate)
            {
                note.DeletedUtc = DateTime.UtcNow.ToString("o"); Save(note);
                string source = Path.GetFullPath(Folder(note));
                string deleted = Path.Combine(Root, ".deleted");
                Directory.CreateDirectory(deleted);
                string target = Path.Combine(deleted, note.Id + "-" + DateTime.UtcNow.Ticks);
                if (!source.StartsWith(Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid note folder.");
                Directory.Move(source, target);
            }
        }
        internal static void Validate(NoteDocument note)
        {
            Guid id; DateTime created;
            if (note == null || !Guid.TryParseExact(note.Id, "N", out id) || !DateTime.TryParse(note.CreatedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out created) || note.Participants == null || note.Segments == null || note.Segments.Count > 100000 || note.Mode != "Microphone" && note.Mode != "Computer" && note.Mode != "Both" || Double.IsNaN(note.DurationSeconds) || Double.IsInfinity(note.DurationSeconds) || note.DurationSeconds < 0) throw new InvalidDataException("Note metadata is invalid.");
            ConversationTools.Validate(note);
            if (note.Segments.Any(s => s == null || s.StartFrame < 0 || s.Frames < 0 || s.StartFrame > Int64.MaxValue - s.Frames || s.TranscriptStartFrame < 0 || s.Source != "Microphone" && s.Source != "Computer" || s.SpeakerLabel != null && (s.SpeakerLabel.Length > 80 || s.SpeakerLabel.Any(Char.IsControl)))) throw new InvalidDataException("Note audio sections are invalid.");
            if (note.Segments.Any(s => s.VoiceId != null && s.VoiceId.Length > 80 || s.VoiceEmbedding != null && (s.VoiceEmbedding.Length != 256 || s.VoiceEmbedding.Any(v => Double.IsNaN(v) || Double.IsInfinity(v))))) throw new InvalidDataException("Note voice profiles are invalid.");
        }
        public int CleanupDeleted(int days)
        {
            if (days <= 0) return 0; int count = 0; string root = Path.Combine(Root, ".deleted"); if (!Directory.Exists(root)) return 0;
            foreach (string folder in Directory.GetDirectories(root))
            {
                try { var note = new JavaScriptSerializer().Deserialize<NoteDocument>(File.ReadAllText(Path.Combine(folder, "note.json"))); DateTime deleted; if (DateTime.TryParse(note.DeletedUtc, out deleted) && deleted.ToUniversalTime() < DateTime.UtcNow.AddDays(-days)) { NotesData.DeleteTree(folder, root); count++; } }
                catch (IOException) { }
                catch (ArgumentException) { }
                catch (InvalidOperationException) { }
            }
            return count;
        }
        void RecoverAudio(NoteDocument note)
        {
            foreach (string source in new[] { "Microphone", "Computer" })
            {
                string path = AudioPath(note, source); if (!File.Exists(path)) continue;
                long frames;
                using (var audio = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                using (var writer = new BinaryWriter(audio))
                {
                    if (audio.Length < 44) continue;
                    frames = (audio.Length - 44) / 2;
                    audio.Position = 4; writer.Write((uint)(36 + frames * 2)); audio.Position = 40; writer.Write((uint)(frames * 2));
                }
                long queued = note.Segments.Where(s => s.Source == source).Select(s => s.StartFrame + s.Frames).DefaultIfEmpty(0).Max();
                while (queued < frames)
                {
                    int count = (int)Math.Min(320000, frames - queued);
                    note.Segments.Add(new NoteSegment { Source = source, StartFrame = queued, Frames = count }); queued += count;
                }
                note.DurationSeconds = Math.Max(note.DurationSeconds, frames / 16000.0);
            }
        }
        public string SavedName
        {
            get { string path = Path.Combine(Root, "name.txt"); lock (gate) return File.Exists(path) ? File.ReadAllText(path).Trim() : ""; }
            set { lock (gate) { Directory.CreateDirectory(Root); AtomicWrite(Path.Combine(Root, "name.txt"), value ?? ""); } }
        }
        public static string Transcript(NoteDocument note)
        {
            lock (note)
            {
                return MeetingVoices.ReviewIndex(note) + String.Join(Environment.NewLine + Environment.NewLine, note.Segments.Where(s => s.Complete && String.IsNullOrEmpty(s.Error) && !String.IsNullOrWhiteSpace(s.RawText ?? s.Text)).OrderBy(s => s.StartFrame).ThenBy(s => s.Source).Select(s =>
                {
                    string name = Speaker(note, s);
                    if (!String.IsNullOrEmpty(s.Introduction)) name += " (introduction: " + s.Introduction + ")";
                    return "[" + TimeSpan.FromSeconds((s.TranscriptStartFrame ?? s.StartFrame) / 16000.0).ToString(@"hh\:mm\:ss") + "] " + name + Environment.NewLine +
                        (s.RawText ?? s.Text);
                }));
            }
        }
        public static string Speaker(NoteDocument note, NoteSegment segment)
        {
            if (!String.IsNullOrWhiteSpace(segment.VoiceId) && String.IsNullOrWhiteSpace(segment.SpeakerLabel)) return segment.VoiceId;
            return !String.IsNullOrWhiteSpace(segment.SpeakerLabel) ? segment.SpeakerLabel + " [" + segment.Source + "]" : segment.Source == "Microphone" && !String.IsNullOrWhiteSpace(note.UserName) ? note.UserName : segment.Source == "Computer" ? "Computer audio" : "Microphone";
        }
        public static string Markdown(NoteDocument note)
        {
            return "# " + note.Title + "\n\n" + note.CreatedUtc + " | " + note.Mode + " | " + note.Status + "\n\n" +
                (String.IsNullOrWhiteSpace(note.UserName) ? "" : "Your name: " + note.UserName + "\n\n") +
                (note.Participants.Count == 0 ? "" : "Introductions: " + String.Join(", ", note.Participants) + "\n\n") +
                "## Notes\n\n" + note.WrittenNotes + "\n\n## Transcript\n\n" + Transcript(note) + "\n" + (String.IsNullOrWhiteSpace(note.Summary) ? "" : "\n## Local review (source excerpts)\n\n" + note.Summary + "\n");
        }
    }
    // A seekable timeline: gaps in loopback capture become silence, not compressed meeting time.
    sealed class WaveTrack : IDisposable
    {
        readonly FileStream stream;
        readonly BinaryWriter writer;
        public long Frames { get; private set; }
        public WaveTrack(string path, bool append = false)
        {
            stream = new FileStream(path, append && File.Exists(path) ? FileMode.Open : FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite);
            writer = new BinaryWriter(stream); if (append && stream.Length >= 44) Frames = (stream.Length - 44) / 2; else { writer.Write(MicRecorder.ToWav(new byte[0])); writer.Flush(); }
        }
        public void WriteAt(long startFrame, byte[] pcm)
        {
            int skip = (int)Math.Min(pcm.Length, Math.Max(0, Frames - startFrame) * 2);
            if (skip >= pcm.Length) return;
            if (startFrame > Frames) PadTo(startFrame);
            if ((Frames * 2 + pcm.Length - skip) > UInt32.MaxValue - 44) throw new IOException("Recording reached the WAV size limit. Start a new note.");
            stream.Position = 44 + Frames * 2; writer.Write(pcm, skip, pcm.Length - skip); Frames += (pcm.Length - skip) / 2;
        }
        public void PadTo(long endFrame)
        {
            if (endFrame * 2 > UInt32.MaxValue - 44) throw new IOException("Recording reached the WAV size limit. Start a new note.");
            var zero = new byte[32000]; stream.Position = 44 + Frames * 2;
            while (Frames < endFrame) { int count = (int)Math.Min(zero.Length, (endFrame - Frames) * 2); writer.Write(zero, 0, count); Frames += count / 2; }
        }
        public void Flush()
        {
            stream.Position = 4; writer.Write((uint)(36 + Frames * 2)); stream.Position = 40; writer.Write((uint)(Frames * 2)); writer.Flush();
        }
        public void Dispose() { Flush(); writer.Dispose(); }
        public static byte[] ReadPcm(string path, long start, int frames)
        {
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long available = Math.Max(0, input.Length - 44 - start * 2); int size = (int)Math.Min(frames * 2L, available);
                var pcm = new byte[size]; input.Position = 44 + start * 2; int offset = 0, n;
                while (offset < size && (n = input.Read(pcm, offset, size - offset)) > 0) offset += n;
                return pcm;
            }
        }
    }
}
