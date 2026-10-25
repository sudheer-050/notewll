using System;
using System.Linq;

namespace Notewll
{
    static class MeetingVoices
    {
        static double Similarity(double[] a, double[] b)
        {
            if (a == null || b == null || a.Length != 256 || b.Length != 256) return -1;
            double dot = 0, aa = 0, bb = 0;
            for (int i = 0; i < a.Length; i++) { dot += a[i] * b[i]; aa += a[i] * a[i]; bb += b[i] * b[i]; }
            return aa == 0 || bb == 0 ? -1 : dot / Math.Sqrt(aa * bb);
        }
        public static void Assign(NoteDocument note, NoteSegment turn)
        {
            // ponytail: online cosine clustering; overlap and very short turns remain unassigned.
            if (turn.VoiceEmbedding == null) { turn.VoiceId = ""; turn.SpeakerLabel = "Unassigned speaker"; return; }
            var match = note.Segments.Where(s => s != turn && s.Source == turn.Source && !String.IsNullOrEmpty(s.VoiceId))
                .Select(s => new { Segment = s, Score = Similarity(s.VoiceEmbedding, turn.VoiceEmbedding) }).OrderByDescending(s => s.Score).FirstOrDefault();
            turn.VoiceId = match != null && match.Score >= 0.78 ? match.Segment.VoiceId : "Speaker " + (note.Segments.Where(s => !String.IsNullOrEmpty(s.VoiceId)).Select(s => s.VoiceId).Distinct().Count() + 1);
            if (match != null && match.Score >= 0.78) turn.SpeakerLabel = match.Segment.SpeakerLabel;
            var names = Introductions.Find(turn.RawText);
            if (names.Count == 1)
            {
                turn.Introduction = names[0]; turn.SpeakerLabel = names[0];
                foreach (var previous in note.Segments.Where(s => s.VoiceId == turn.VoiceId)) previous.SpeakerLabel = names[0];
                if (!note.Participants.Contains(names[0])) note.Participants.Add(names[0]);
            }
        }
        public static string ReviewIndex(NoteDocument note)
        {
            var voices = note.Segments.Where(s => !String.IsNullOrEmpty(s.VoiceId)).GroupBy(s => s.VoiceId).ToArray();
            if (voices.Length == 0) return "";
            return "Speakers - review names and voice samples in Correct speaker labels\n" + String.Join("\n", voices.Select(g =>
            {
                var first = g.OrderBy(s => s.StartFrame).First();
                return (String.IsNullOrWhiteSpace(first.SpeakerLabel) ? g.Key + " (name needed)" : first.SpeakerLabel + " (suggested)") + ": " + String.Join(" ", g.Take(2).Select(s => s.RawText));
            })) + "\n\n";
        }
    }
}
