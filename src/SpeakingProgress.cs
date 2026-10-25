using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace LocalDictation
{
    // Text changes are review opportunities, never a language proficiency score.
    sealed class SpeakingProgress
    {
        public int Sessions, Changed, Streak, Days, Level;
        public int Next;
        public NoteDocument LatestChange;
        static readonly int[] Goals = { 0, 3, 7, 14, 30, 60, 100 };
        static string Words(string value)
        {
            return String.Join(" ", Regex.Matches((value ?? "").ToLowerInvariant(), @"[\p{L}\p{N}]+(?:['’-][\p{L}\p{N}]+)*").Cast<Match>().Select(m => m.Value));
        }
        public static SpeakingProgress From(IEnumerable<NoteDocument> entries, DateTime today)
        {
            var result = new SpeakingProgress(); var dates = new HashSet<DateTime>();
            foreach (var note in entries.Where(n => n.Kind == "Dictation" && n.Status == "Complete"))
            {
                var segments = note.Segments.Where(s => s.Complete && String.IsNullOrEmpty(s.Error) && !String.IsNullOrWhiteSpace(s.RawText) && !String.IsNullOrWhiteSpace(s.Text)).ToList();
                if (segments.Count == 0) continue;
                DateTime created;
                if (!DateTime.TryParse(note.CreatedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out created)) continue;
                var day = created.ToLocalTime().Date; if (day > today.Date) continue;
                result.Sessions++; dates.Add(day);
                if (segments.Any(s => Words(s.RawText) != Words(s.Text)))
                {
                    result.Changed++;
                    if (result.LatestChange == null || ActivityHistory.LocalDate(result.LatestChange) < created.ToLocalTime()) result.LatestChange = note;
                }
            }
            result.Days = dates.Count;
            var cursor = dates.Contains(today.Date) ? today.Date : today.Date.AddDays(-1);
            while (dates.Contains(cursor)) { result.Streak++; cursor = cursor.AddDays(-1); }
            result.Level = Goals.Count(g => result.Days >= g);
            result.Next = Goals.FirstOrDefault(g => g > result.Days);
            return result;
        }
    }

    sealed partial class SettingsForm
    {
        Label speakingMetrics, speakingMilestone, speakingDetail;
        HubButton reviewSpeaking;
        SpeakingProgress speakingProgress;
        void BuildSpeakingProgress(Panel page)
        {
            var card = Card(page, 616, 248);
            Label(card, "Your dictation habit", 22, 12, 700, 30, 14, true, Ink);
            speakingMetrics = Label(card, "", 22, 50, 710, 32, 12, true, Ink);
            speakingMilestone = Label(card, "", 22, 88, 710, 30, 10, false, Ink);
            speakingDetail = Label(card, "", 22, 122, 710, 58, 9, false, Muted);
            reviewSpeaking = Button(card, "Review latest change", 22, 192, 218, (s, e) => {
                if (speakingProgress == null || speakingProgress.LatestChange == null) return;
                var segments = speakingProgress.LatestChange.Segments.Where(x => x.Complete && String.IsNullOrEmpty(x.Error));
                UpdateTranscript(String.Join(" ", segments.Select(x => x.RawText)), String.Join(" ", segments.Select(x => x.Text)));
                SetHomePractice(true, true);
            });
            Button(card, "Try dictation", 254, 192, 210, (s, e) => SetHomePractice(true, true));
            card.Resize += (s, e) => {
                int width = card.ClientSize.Width - (int)(44 * uiScale);
                speakingMetrics.Width = speakingMilestone.Width = speakingDetail.Width = width;
            };
        }
        void RefreshSpeakingProgress()
        {
            if (speakingMetrics == null) return;
            speakingProgress = SpeakingProgress.From(activityEntries, DateTime.Today);
            var p = speakingProgress;
            speakingMetrics.Text = String.Format("{0} day streak    ·    Activity level {1}    ·    {2} sessions changed", p.Streak, p.Level, p.Changed);
            speakingMilestone.Text = p.Next == 0 ? "100 active dictation days reached. Keep your dictation habit growing." : String.Format("Next milestone: {0} active dictation days  ·  {1} of {0} completed", p.Next, p.Days);
            speakingDetail.Text = p.Sessions == 0 ? "Start with a saved dictation. Compare the raw and modified text.\nLevels reward active days; quick tests below do not count." : String.Format("{0} of {1} saved dictations had no word changes. Compare the raw and modified text.\nCounts reflect your saved dictations and basic text cleanup.", p.Sessions - p.Changed, p.Sessions);
            reviewSpeaking.Enabled = p.LatestChange != null;
        }
    }
}
