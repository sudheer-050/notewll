using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
namespace LocalDictation
{
    sealed class NoteBookmark { public string Kind { get; set; } public double Seconds { get; set; } }
    sealed class NoteTask
    {
        public int Segment { get; set; }
        public string Text { get; set; }
        public string Owner { get; set; }
        public string Due { get; set; }
        public bool Confirmed { get; set; }
        public bool Done { get; set; }
    }
    sealed class ConversationHit
    {
        public NoteDocument Note; public int Segment; public double Score;
        public double Seconds { get { return Segment < 0 ? 0 : (Note.Segments[Segment].TranscriptStartFrame ?? Note.Segments[Segment].StartFrame) / 16000.0; } }
        public string Text { get { return Segment < 0 ? Note.WrittenNotes : Note.Segments[Segment].RawText ?? Note.Segments[Segment].Text; } }
        public override string ToString() { return Note.Title + " | " + TimeSpan.FromSeconds(Seconds).ToString(@"hh\:mm\:ss") + " | " + Text; }
    }
    static class ConversationTools
    {
        public static void Validate(NoteDocument note)
        {
            if (note.Bookmarks == null) note.Bookmarks = new List<NoteBookmark>();
            if (note.Tasks == null) note.Tasks = new List<NoteTask>();
            if (note.Bookmarks.Count > 10000 || note.Bookmarks.Any(b => b == null || Double.IsNaN(b.Seconds) || Double.IsInfinity(b.Seconds) || b.Seconds < 0 || !new[] { "Important", "Question", "Follow up" }.Contains(b.Kind))) throw new InvalidDataException("Invalid bookmarks.");
            if (note.Tasks.Count > 10000 || note.Tasks.Any(t => t == null || t.Segment < 0 || t.Segment >= note.Segments.Count || String.IsNullOrWhiteSpace(t.Text) || t.Text.Length > 8000 || (t.Owner ?? "").Length > 80 || (t.Due ?? "").Length > 120)) throw new InvalidDataException("Invalid action items.");
            if ((note.Project ?? "").Length > 200) throw new InvalidDataException("Use a project name under 200 characters.");
        }
        public static bool Matches(NoteDocument note, string query, string searchable)
        {
            foreach (Match term in Regex.Matches(query ?? "", "(?:project|tag|person):(?:\"[^\"]*\"|\\S+)|\\S+", RegexOptions.IgnoreCase))
            {
                string token = term.Value; int colon = token.IndexOf(':');
                string key = colon < 0 ? "" : token.Substring(0, colon).ToLowerInvariant();
                string value = colon < 0 ? token : token.Substring(colon + 1).Trim('"');
                string haystack = key == "project" ? note.Project : key == "tag" ? note.Tags : key == "person" ? note.UserName + " " + String.Join(" ", note.Participants) : searchable + " " + note.Project;
                if ((haystack ?? "").IndexOf(value, StringComparison.OrdinalIgnoreCase) < 0) return false;
            }
            return true;
        }
        public static List<ConversationHit> Passages(IEnumerable<NoteDocument> notes)
        {
            var result = new List<ConversationHit>();
            foreach (var note in notes) lock (note)
            {
                for (int i = 0; i < note.Segments.Count; i++) { var s = note.Segments[i]; if (s.Complete && String.IsNullOrEmpty(s.Error) && !String.IsNullOrWhiteSpace(s.RawText ?? s.Text)) result.Add(new ConversationHit { Note = note, Segment = i }); }
                if (!String.IsNullOrWhiteSpace(note.WrittenNotes)) result.Add(new ConversationHit { Note = note, Segment = -1 });
            }
            return result;
        }
        public static List<NoteTask> Suggest(NoteDocument note)
        {
            var tasks = new List<NoteTask>();
            for (int i = 0; i < note.Segments.Count; i++)
            {
                var s = note.Segments[i]; string text = s.RawText ?? s.Text ?? "";
                if (!s.Complete || !String.IsNullOrEmpty(s.Error) || note.Tasks.Any(t => t.Segment == i) || !Regex.IsMatch(text, @"(?i)\b(will|need to|must|action item|follow up|please)\b")) continue;
                var owner = Regex.Match(text, @"\b([A-Z][\p{L}'-]+)\s+(?:will|needs to|must)\b");
                var due = Regex.Match(text, @"(?i)\b(?:by|before|on)\s+(?:\w+[ ,/-]*){1,3}|\b(?:today|tomorrow|next week)\b");
                tasks.Add(new NoteTask { Segment = i, Text = text.Substring(0, Math.Min(8000, text.Length)), Owner = owner.Success ? owner.Groups[1].Value : "", Due = due.Value.Trim() });
            }
            return tasks;
        }
        static HashSet<string> Words(string text) { return new HashSet<string>(Regex.Matches((text ?? "").ToLowerInvariant(), @"[\p{L}\p{N}]{3,}").Cast<Match>().Select(m => m.Value).Where(w => !new[] { "the", "and", "that", "this", "with", "was", "for", "have", "are" }.Contains(w))); }
        public static List<ConversationHit> Highlights(NoteDocument note)
        {
            // ponytail: bound the quadratic graph to 400 evenly spaced passages; sample more if long-meeting profiling warrants it.
            var all = Passages(new[] { note }).Where(h => h.Segment >= 0).ToList();
            var hits = all.Count <= 400 ? all : Enumerable.Range(0, 400).Select(i => all[i * (all.Count - 1) / 399]).ToList();
            int n = hits.Count; if (n == 0) return hits;
            var words = hits.Select(h => Words(h.Text)).ToArray(); var edges = new double[n,n]; var sums = new double[n]; var rank = Enumerable.Repeat(1.0 / n, n).ToArray();
            for (int i = 0; i < n; i++) for (int j = i + 1; j < n; j++) { double weight = words[i].Intersect(words[j]).Count() / Math.Max(1.0, Math.Log(words[i].Count + 1) + Math.Log(words[j].Count + 1)); edges[i,j] = edges[j,i] = weight; sums[i] += weight; sums[j] += weight; }
            for (int k = 0; k < 25; k++) { var next = Enumerable.Repeat(.15 / n, n).ToArray(); for (int j = 0; j < n; j++) for (int i = 0; i < n; i++) next[i] += .85 * rank[j] * (sums[j] == 0 ? 1.0 / n : edges[j,i] / sums[j]); rank = next; }
            for (int i = 0; i < n; i++) hits[i].Score = rank[i];
            return hits.OrderByDescending(h => h.Score).Take(6).OrderBy(h => h.Seconds).ToList();
        }
    }
}
