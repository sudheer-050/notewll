using System;
using System.Linq;
using System.Text.RegularExpressions;
namespace Notewll
{
    static class CorrectionLearning
    {
        public static PersonalRule Suggest(string original, string edited)
        {
            var before = Regex.Matches(original ?? "", @"[\p{L}]+(?:['\u2019-][\p{L}]+)*").Cast<Match>().ToArray();
            var after = Regex.Matches(edited ?? "", @"[\p{L}]+(?:['\u2019-][\p{L}]+)*").Cast<Match>().ToArray();
            int start = 0; while (start < before.Length && start < after.Length && before[start].Value.Equals(after[start].Value, StringComparison.OrdinalIgnoreCase)) start++;
            int a = before.Length, b = after.Length;
            while (a > start && b > start && before[a - 1].Value.Equals(after[b - 1].Value, StringComparison.OrdinalIgnoreCase)) { a--; b--; }
            if (a - start < 1 || a - start > 4 || b - start < 1 || b - start > 4) return null;
            string from = original.Substring(before[start].Index, before[a - 1].Index + before[a - 1].Length - before[start].Index);
            string to = edited.Substring(after[start].Index, after[b - 1].Index + after[b - 1].Length - after[start].Index);
            if (Regex.IsMatch(from + " " + to, @"\d|\b(?:not|no|never|cannot|don't|doesn't|didn't|won't)\b", RegexOptions.IgnoreCase)) return null;
            return new PersonalRule { Kind = "Word", Trigger = from, Replacement = to, Origin = "Confirmed correction" };
        }
    }
    sealed partial class SettingsForm
    {
        PersonalRule correctionSuggestion;
        void OfferCorrection(string before, string after)
        {
            correctionSuggestion = CorrectionLearning.Suggest(before, after);
            if (correctionSuggestion != null && pendingRules.Any(r => r.Trigger.Equals(correctionSuggestion.Trigger, StringComparison.OrdinalIgnoreCase))) correctionSuggestion = null;
            if (correctionSuggestion != null) sttStatus.Text = "Possible word correction: " + correctionSuggestion.Trigger + " → " + correctionSuggestion.Replacement + ". Choose Remember a correction to review and save it.";
        }
    }
}
