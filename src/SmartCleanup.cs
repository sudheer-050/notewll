using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
namespace LocalDictation
{
    static class SmartCleanup
    {
        static readonly HashSet<string> RestartWords = new HashSet<string>(("i you we he she they it what which how where when why who is are am was were be been being do does did have has can could would should will may might a an the to of in on at for with about regarding and but or so just wanted want need like please well mean actually sorry let me us" ).Split(' '));
        static readonly HashSet<string> NeverDrop = new HashSet<string>(("not no never neither nor without unless except only before after until must cannot can't don't doesn't didn't won't wouldn't shouldn't isn't aren't wasn't weren't hasn't haven't hadn't couldn't mustn't" ).Split(' '));
        static readonly HashSet<string> Fillers = new HashSet<string>(new[] { "um", "uh", "uhm", "erm", "hmm" });
        public static string LastCandidate = null;
        public static string Status = "Modified text cleanup is ready. No model or download required.";
        static string[] Tokens(string value) { return Regex.Matches(value ?? "", @"\d+(?:[.,]\d+)*|[^\W\d]+(?:['\u2019][^\W\d]+)*").Cast<Match>().Select(m => m.Value.ToLowerInvariant().Replace('\u2019', '\'')).ToArray(); }
        static string WordKey(string value) { return value.ToLowerInvariant().Replace('\u2019', '\''); }
        static bool IsQuestionWord(string word) { return new[] { "what", "which", "how", "where", "when", "why", "who" }.Contains(word.Split('\'')[0]); }
        static bool IsRestartWord(string word) { return RestartWords.Contains(word) || Regex.IsMatch(word, @"^(?:what|which|how|where|when|why|who)'(?:s|re|d|ll|ve)$"); }
        public static bool Enabled(Config config) { return false; }
        public static string Basic(string raw)
        {
            // Repeated grammatical prefixes of arbitrary length, rather than a list of complete sentences.
            string text = raw ?? "";
            var matches = Regex.Matches(text, @"[\p{L}]+(?:['\u2019][\p{L}]+)*").Cast<Match>().ToArray();
            if (matches.Length > 512) return text;
            for (int i = 0; i < matches.Length; i++) for (int size = Math.Min(8, (matches.Length - i) / 2); size >= 2; size--)
            {
                var first = matches.Skip(i).Take(size).Select(m => WordKey(m.Value)).ToArray();
                if (!first.All(IsRestartWord) || first.Any(NeverDrop.Contains) || !first.Any(w => new[] { "i", "you", "we", "can", "could", "would", "what", "which" }.Contains(w) || IsQuestionWord(w))) continue;
                if (!first.SequenceEqual(matches.Skip(i + size).Take(size).Select(m => WordKey(m.Value)))) continue;
                int from = matches[i].Index, to = matches[i + size].Index;
                if (Regex.IsMatch(text.Substring(from, matches[i + size * 2 - 1].Index + matches[i + size * 2 - 1].Length - from), @"[^\p{L}\p{N}\s,'\u2019-]")) continue;
                return Basic(text.Remove(from, to - from));
            }
            // An anchor repeated around an abandoned grammatical fragment. Choose the latest restart.
            for (int i = 0; i < matches.Length; i++)
            {
                string anchor = matches[i].Value.ToLowerInvariant();
                if (!new[] { "what", "which", "about", "regarding" }.Contains(anchor)) continue;
                for (int j = Math.Min(matches.Length - 2, i + 12); j >= i + 2; j--)
                {
                    if (!String.Equals(matches[j].Value, anchor, StringComparison.OrdinalIgnoreCase)) continue;
                    var fragment = matches.Skip(i).Take(j - i).Select(m => WordKey(m.Value)).ToArray();
                    if (!fragment.All(IsRestartWord) || !fragment.Any(w => new[] { "is", "are", "was", "were" }.Contains(w))) continue;
                    if (anchor == "about" || anchor == "regarding") { if (fragment.Count(w => w == "what" || w == "which") < 2) continue; }
                    else
                    {
                        var context = matches.Skip(Math.Max(0, i - 2)).Take(Math.Min(i, 2)).Select(m => WordKey(m.Value)).ToArray();
                        if (!context.Any(w => new[] { "list", "show", "tell", "explain", "describe", "summarize", "outline", "ask" }.Contains(w))) continue;
                        string next = matches[j + 1].Value.ToLowerInvariant();
                        if (RestartWords.Contains(next) && !new[] { "a", "an", "the" }.Contains(next) || new[] { "makes", "made", "happens", "happened", "matters", "counts", "remains", "comes", "goes", "that", "this", "these", "those" }.Contains(next)) continue;
                    }
                    int from = matches[i].Index, to = matches[j].Index;
                    if (Regex.IsMatch(text.Substring(from, to - from), @"[^\p{L}\p{N}\s,'\u2019-]")) continue;
                    return Basic(text.Remove(from, to - from));
                }
            }
            return text;
        }
        public static bool Accept(string original, string candidate)
        {
            return !String.IsNullOrWhiteSpace(candidate) && Tokens(original).SequenceEqual(Tokens(candidate));
        }
        public static void Warm() { }
        public static string Refine(Config config, string original) { return original; }
        public static string Complete(Config config, string raw, int waitMilliseconds)
        {
            return WritingFormat.Compose(config, raw);
        }
    }
}
