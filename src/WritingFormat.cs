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
            var snippet = RichSnippets.Exact(config, raw); if (snippet != null) return snippet.Replacement;
            if (config.SpeechLanguage != "en") return PersonalRule.Apply(config, (raw ?? "").Trim());
            if (config.WritingStyle == "Code") return PersonalRule.Apply(config, (raw ?? "").Trim());
            string explicitList = ExplicitList(raw);
            if (explicitList != null) return PersonalRule.Apply(config, explicitList);
            string text = PersonalRule.Apply(config, TextCleanup.Clean(raw, config.CleanupLevel != "Light"));
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
        static string ExplicitList(string raw)
        {
            if (String.IsNullOrWhiteSpace(raw) || raw.IndexOf('"') >= 0 || raw.IndexOf('\u201c') >= 0) return null;
            var prefix = Regex.Match(raw, @"^\s*(numbered list|bullet list)\s*[:,]?\s*", RegexOptions.IgnoreCase);
            if (!prefix.Success) return null;
            string body = raw.Substring(prefix.Length);
            if (prefix.Groups[1].Value.Equals("bullet list", StringComparison.OrdinalIgnoreCase))
            {
                var items = Regex.Split(body, @"\s+(?:next item|new item)\s+", RegexOptions.IgnoreCase).Select(x => x.Trim()).ToArray();
                return items.Length >= 2 && items.Length <= 50 && items.All(x => x.Length > 0) ? String.Join(Environment.NewLine, items.Select(x => "\u2022 " + Upper(x))) : null;
            }
            string[] numbers = { "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" };
            var matches = Regex.Matches(body, @"(?:^|\s)(one|two|three|four|five|six|seven|eight|nine|ten|[1-9]|10)[.)]?\s+", RegexOptions.IgnoreCase).Cast<Match>().ToArray();
            if (matches.Length < 2 || matches[0].Index != 0) return null;
            var result = new System.Collections.Generic.List<string>();
            for (int i = 0; i < matches.Length; i++)
            {
                int value; if (!Int32.TryParse(matches[i].Groups[1].Value, out value)) value = Array.FindIndex(numbers, n => n.Equals(matches[i].Groups[1].Value, StringComparison.OrdinalIgnoreCase)) + 1;
                if (value != i + 1) return null;
                int start = matches[i].Index + matches[i].Length, end = i + 1 < matches.Length ? matches[i + 1].Index : body.Length;
                string item = body.Substring(start, end - start).Trim(); if (item.Length == 0) return null;
                result.Add(value + ". " + Upper(item));
            }
            return String.Join(Environment.NewLine, result);
        }
    }
}
