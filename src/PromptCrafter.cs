using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Notewll
{
    // One provider's prompting guide: who it is for, how to recognise it, and the guidance text.
    sealed class PromptGuide
    {
        public string File, Name, Provider, Version, Body, Ask, Wrap;
        public string[] Wake = new string[0], Apps = new string[0], Titles = new string[0];

        public static PromptGuide Parse(string file, string text)
        {
            var guide = new PromptGuide { File = file, Name = Path.GetFileNameWithoutExtension(file) };
            text = (text ?? "").Replace("\r\n", "\n");
            int split = text.IndexOf("\n---", StringComparison.Ordinal);
            if (split < 0) { guide.Body = text.Trim(); return guide; }
            foreach (var line in text.Substring(0, split).Split('\n'))
            {
                int colon = line.IndexOf(':'); if (colon < 1) continue;
                string key = line.Substring(0, colon).Trim().ToLowerInvariant(), value = line.Substring(colon + 1).Trim();
                Func<string[]> list = () => value.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0).ToArray();
                if (key == "name") guide.Name = value; else if (key == "provider") guide.Provider = value; else if (key == "version") guide.Version = value;
                else if (key == "ask") guide.Ask = value; else if (key == "wrap") guide.Wrap = value.ToLowerInvariant();
                else if (key == "wake") guide.Wake = list(); else if (key == "apps") guide.Apps = list(); else if (key == "titles") guide.Titles = list();
            }
            guide.Body = text.Substring(split + 4).Trim();
            return guide;
        }
    }

    // What to do with one dictation: which guide applies, the spoken request without the wake word, and whether to rewrite it.
    sealed class PromptPlan { public PromptGuide Guide; public string Text; public bool Rewrite; public string Reason; }

    // Turns a dictated request into a prompt that follows the target AI's own prompting guidance (rewritten by the Notewll prompt service).
    // Guides live as editable files in Documents\Notewll\Prompt guides; nothing is fetched unless the user asks for an update.
    static class PromptCrafter
    {
        public const string UpdateSource = "https://api.github.com/repos/sudheer-050/notewll/contents/prompt-guides";
        public static string Folder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Notewll", "Prompt guides"); } }
        static string BaselineFolder { get { return Path.Combine(Folder, ".baseline"); } }
        static string BackupFolder { get { return Path.Combine(Folder, "Backups"); } }

        static Dictionary<string, string> Shipped()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); var assembly = Assembly.GetExecutingAssembly();
            foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith("PromptGuide.", StringComparison.Ordinal)))
                using (var reader = new StreamReader(assembly.GetManifestResourceStream(name), Encoding.UTF8)) result[name.Substring("PromptGuide.".Length)] = reader.ReadToEnd();
            return result;
        }

        public static List<PromptGuide> ShippedGuides() { return Shipped().Select(item => PromptGuide.Parse(item.Key, item.Value)).ToList(); }

        // Copy the built-in guides to the user's folder the first time; never overwrite a file that already exists.
        public static void EnsureGuides()
        {
            Directory.CreateDirectory(Folder); Directory.CreateDirectory(BaselineFolder);
            try { new DirectoryInfo(BaselineFolder).Attributes |= FileAttributes.Hidden; } catch { }
            foreach (var item in Shipped())
            {
                string path = Path.Combine(Folder, item.Key);
                if (!File.Exists(path)) { File.WriteAllText(path, item.Value.Replace("\r\n", "\n").Replace("\n", "\r\n"), new UTF8Encoding(false)); File.WriteAllText(Path.Combine(BaselineFolder, item.Key), Normalize(item.Value), new UTF8Encoding(false)); }
            }
        }

        public static List<PromptGuide> Load()
        {
            try { EnsureGuides(); } catch { }
            var guides = new List<PromptGuide>();
            if (Directory.Exists(Folder))
                foreach (var file in Directory.GetFiles(Folder, "*.md").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                { try { if (new FileInfo(file).Length <= 64 * 1024) guides.Add(PromptGuide.Parse(file, File.ReadAllText(file))); } catch { } }
            if (guides.Count == 0) guides.AddRange(Shipped().Select(item => PromptGuide.Parse(item.Key, item.Value)));
            return guides;
        }

        static readonly Regex Greeting = new Regex(@"^(hi|hello|hey|hiya|yo|thanks|thank you|thank you so much|ok|okay|yes|yeah|yep|no|nope|sure|cool|great|nice|good morning|good afternoon|good evening|good night|bye|goodbye|see you)[\s.!?,]*$", RegexOptions.IgnoreCase);
        const int MinimumWords = 8;

        // Decide what happens to a dictation. Returns null when the text should be typed exactly as dictated.
        public static PromptPlan Plan(string mode, string text, string processName, string windowTitle, List<PromptGuide> guides = null)
        {
            if (mode != "WakeWord" && mode != "Automatic" || String.IsNullOrWhiteSpace(text)) return null;
            guides = guides ?? Load();
            string rest; var guide = ByWakeWord(guides, text, out rest);
            if (guide == null && mode == "Automatic") { guide = ForWindow(guides, processName, windowTitle); rest = text.Trim(); }
            if (guide == null) return null;
            var plan = new PromptPlan { Guide = guide, Text = rest };
            int words = Regex.Matches(rest, @"[\p{L}\p{N}']+").Count;
            if (Greeting.IsMatch(rest.Trim())) plan.Reason = "greeting";
            else if (words < MinimumWords) plan.Reason = "short";
            else plan.Rewrite = true;
            return plan;
        }

        // "Claude, write a summary..." -> Claude guide and "write a summary...". The wake word must lead the dictation.
        public static PromptGuide ByWakeWord(List<PromptGuide> guides, string text, out string rest)
        {
            rest = text;
            foreach (var pair in guides.SelectMany(g => g.Wake.Select(w => new { Guide = g, Wake = w })).OrderByDescending(p => p.Wake.Length))
            {
                string pattern = @"^\W*(?:(?:hey|hi|ok|okay)\s+)?" + String.Join(@"[\s-]*", pair.Wake.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape)) + @"(?![\p{L}\p{N}])[\s,.:;!?-]*(.*)$";
                var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (!m.Success) continue;
                rest = m.Groups[1].Value.Trim();
                if (rest.Length > 0) rest = Char.ToUpperInvariant(rest[0]) + rest.Substring(1);
                return pair.Guide;
            }
            return null;
        }

        static readonly HashSet<string> Browsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "arc" };
        // Automatic mode: the AI desktop app in front, or a browser tab whose title names the AI.
        public static PromptGuide ForWindow(List<PromptGuide> guides, string processName, string windowTitle)
        {
            string process = processName ?? "", title = windowTitle ?? "";
            var byApp = guides.FirstOrDefault(g => g.Apps.Any(a => a.Equals(process, StringComparison.OrdinalIgnoreCase)));
            if (byApp != null) return byApp;
            if (!Browsers.Contains(process) && !process.Equals("msedgewebview2", StringComparison.OrdinalIgnoreCase)) return null;
            return guides.FirstOrDefault(g => g.Titles.Any(t => t.Length > 0 && Regex.IsMatch(title, (Char.IsLetterOrDigit(t[0]) ? @"(?<![\p{L}\p{N}])" : "") + Regex.Escape(t) + (Char.IsLetterOrDigit(t[t.Length - 1]) ? @"(?![\p{L}\p{N}])" : ""), RegexOptions.IgnoreCase)));
        }

        // Rewrite through the Notewll prompt service: the person's Google sign-in proves who they are, and Gemini
        // applies the guide. Their own (possibly edited) guide text is sent with the request.
        public static string Rewrite(Config config, PromptPlan plan)
        {
            string token = GoogleAuth.IdToken();
            var body = new Dictionary<string, object>
            {
                { "guide", new Dictionary<string, object> { { "name", plan.Guide.Name }, { "provider", plan.Guide.Provider ?? "Any" }, { "body", plan.Guide.Body.Length > 8000 ? plan.Guide.Body.Substring(0, 8000) : plan.Guide.Body } } },
                { "request", plan.Text.Length > 4000 ? plan.Text.Substring(0, 4000) : plan.Text }
            };
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create(NotewllCloud.ServiceUrl);
            request.Method = "POST"; request.ContentType = "application/json"; request.Timeout = request.ReadWriteTimeout = 30000;
            request.Headers["Authorization"] = "Bearer " + token; request.UserAgent = "Notewll/" + StoreDistribution.DisplayVersion;
            byte[] data = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(body));
            string text;
            try
            {
                using (var stream = request.GetRequestStream()) stream.Write(data, 0, data.Length);
                using (var response = request.GetResponse()) using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) text = reader.ReadToEnd();
            }
            catch (WebException ex)
            {
                if (ex.Response == null) throw new IOException("Could not reach the Notewll prompt service. Check your internet connection.");
                string message = null;
                try { using (var reader = new StreamReader(ex.Response.GetResponseStream(), Encoding.UTF8)) { var error = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(reader.ReadToEnd()); message = error != null && error.ContainsKey("error") ? error["error"] as string : null; } } catch { }
                throw new IOException(message ?? "The prompt service could not write the prompt.");
            }
            var json = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(text);
            string prompt = Clean(json != null && json.ContainsKey("prompt") ? json["prompt"] as string : null);
            if (String.IsNullOrWhiteSpace(prompt)) throw new IOException("The prompt service returned an empty prompt.");
            return prompt;
        }

        public static string Clean(string text)
        {
            text = Regex.Replace(text ?? "", @"<think>.*?</think>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase).Trim();
            text = Regex.Replace(text, @"^```[a-zA-Z]*\s*|\s*```$", "").Trim();
            text = Regex.Replace(text, @"^(Here is|Here's)[^\n]*prompt[^\n]*:\s*\n", "", RegexOptions.IgnoreCase).Trim();
            if (text.Length > 1 && (text[0] == '"' && text[text.Length - 1] == '"' || text[0] == '“' && text[text.Length - 1] == '”')) text = text.Substring(1, text.Length - 2).Trim();
            return Regex.Replace(text.Replace("\r\n", "\n"), @"\n{3,}", "\n\n");
        }

        static string Normalize(string text) { return (text ?? "").Replace("\r\n", "\n").Trim() + "\n"; }
        static string Hash(string text) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Normalize(text)))).Replace("-", ""); }

        // User-requested update from the Notewll repository. Edited guides are backed up before they are replaced.
        public static string Update()
        {
            EnsureGuides(); Directory.CreateDirectory(BackupFolder);
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var listing = Get(UpdateSource);
            var files = new JavaScriptSerializer().Deserialize<List<Dictionary<string, object>>>(listing);
            int updated = 0, added = 0, backedUp = 0, same = 0, kept = 0;
            foreach (var entry in files)
            {
                string name = entry.ContainsKey("name") ? entry["name"] as string : null, url = entry.ContainsKey("download_url") ? entry["download_url"] as string : null;
                if (name == null || url == null || !name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || name != Path.GetFileName(name) || !url.StartsWith("https://raw.githubusercontent.com/sudheer-050/notewll/", StringComparison.Ordinal)) continue;
                string latest = Get(url); if (latest.Length > 64 * 1024) continue;
                PromptGuide.Parse(name, latest); // must parse
                string path = Path.Combine(Folder, name), baseline = Path.Combine(BaselineFolder, name);
                if (File.Exists(path))
                {
                    string current = File.ReadAllText(path);
                    if (Hash(current) == Hash(latest)) { same++; File.WriteAllText(baseline, Normalize(latest), new UTF8Encoding(false)); continue; }
                    bool edited = !File.Exists(baseline) || Hash(current) != Hash(File.ReadAllText(baseline));
                    if (edited && File.Exists(baseline) && Hash(latest) == Hash(File.ReadAllText(baseline))) { kept++; continue; } // no newer guide: keep the user's edits untouched
                    if (edited) { File.Copy(path, Path.Combine(BackupFolder, Path.GetFileNameWithoutExtension(name) + " " + DateTime.Now.ToString("yyyy-MM-dd HHmm") + ".md"), true); backedUp++; }
                    updated++;
                }
                else added++;
                File.WriteAllText(path, Normalize(latest).Replace("\n", "\r\n"), new UTF8Encoding(false));
                File.WriteAllText(baseline, Normalize(latest), new UTF8Encoding(false));
            }
            return updated + " updated, " + added + " added, " + same + " already current" + (kept > 0 ? ", " + kept + " kept with your edits" : "") + (backedUp > 0 ? "; your edits to " + backedUp + " guide(s) were saved in Prompt guides\\Backups" : "") + ".";
        }

        static string Get(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url); request.UserAgent = "Notewll"; request.Timeout = request.ReadWriteTimeout = 20000;
            using (var response = (HttpWebResponse)request.GetResponse()) using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) return reader.ReadToEnd();
        }
    }
}
