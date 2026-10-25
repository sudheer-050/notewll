using System;
using System.Linq;
using System.Text.RegularExpressions;
namespace Notewll
{
    static class WritingFormat
    {
        public static string Compose(Config config, string raw)
        {
            if (!config.CleanupEnabled) return raw ?? "";
            if (config.WritingStyle == "Code") return PersonalRule.Apply(config, (raw ?? "").Trim());
            string text = PersonalRule.Apply(config, TextCleanup.Clean(raw));
            text = Regex.Replace(text, @"\b(monday|tuesday|wednesday|thursday|friday|saturday|sunday)\b", m => Char.ToUpperInvariant(m.Value[0]) + m.Value.Substring(1).ToLowerInvariant(), RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bi\b", "I");
            text = Regex.Replace(text, @"^(hello|hi|hey)\s+([\p{L}][\p{L}'-]*)\s+(?=please\b|thanks\b|thank\b|can\b|could\b|I\b|we\b)", m => Char.ToUpperInvariant(m.Groups[1].Value[0]) + m.Groups[1].Value.Substring(1).ToLowerInvariant() + " " + (new[] { "team", "everyone", "there", "all" }.Contains(m.Groups[2].Value.ToLowerInvariant()) ? m.Groups[2].Value.ToLowerInvariant() : Char.ToUpperInvariant(m.Groups[2].Value[0]) + m.Groups[2].Value.Substring(1)) + ", " , RegexOptions.IgnoreCase);
            // ponytail: only an explicitly ordered, whole-input list is formatted; ordinary number mentions stay intact.
            var list = Regex.Match(text, @"^(?:(?:shopping )?list[: ]+)?(?:one|1)[.)]?\s+(.+?)\s+(?:two|2)[.)]?\s+(.+?)\s+(?:three|3)[.)]?\s+(.+?)[.!]?$", RegexOptions.IgnoreCase);
            if (list.Success) text = String.Join(Environment.NewLine, Enumerable.Range(1, 3).Select(i => i + ". " + Upper(list.Groups[i].Value.Trim().TrimEnd('.'))));
            if (config.WritingStyle == "Email") text = Regex.Replace(text, @"^((?:Hello|Hi|Hey) [^,\r\n]+,)\s+", "$1" + Environment.NewLine + Environment.NewLine);
            if (config.WritingStyle == "Chat" && text.Length < 180 && !text.Contains("\n") && text.EndsWith(".") && !Regex.IsMatch(text, @"\b\w\.\w\.$")) text = text.Substring(0, text.Length - 1);
            return text;
        }
        static string Upper(string value) { return value.Length == 0 ? value : Char.ToUpperInvariant(value[0]) + value.Substring(1); }
    }
}
