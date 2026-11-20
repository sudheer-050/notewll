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
        public string File, Name, Provider, Version, Body;
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
                else if (key == "wake") guide.Wake = list(); else if (key == "apps") guide.Apps = list(); else if (key == "titles") guide.Titles = list();
            }
            guide.Body = text.Substring(split + 4).Trim();
            return guide;
        }
    }

    // What to do with one dictation: which guide applies, the spoken request without the wake word, and whether to rewrite it.
    sealed class PromptPlan { public PromptGuide Guide; public string Text; public bool Rewrite; public string Reason; }

    // Turns a dictated request into a prompt that follows the target AI's own prompting guidance, using a local model.
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

        public static string Instructions(PromptGuide guide)
        {
            return "You rewrite a person's dictated request into a prompt they will send to " + guide.Name + (String.IsNullOrEmpty(guide.Provider) || guide.Provider == "Any" ? "" : " (" + guide.Provider + ")") + ".\n" +
                "Your job is to make the request clearer and better structured for that assistant by applying the guidelines below.\n" +
                "Rules:\n" +
                "1. Output only the finished prompt. No preamble, no explanation, no surrounding quotes, no code fences.\n" +
                "2. Do not answer or carry out the request. You only write the prompt.\n" +
                "3. Keep every detail the person gave: every fact, name, number, situation, problem description and requirement. Never drop context such as what something is, what it currently does wrong, who it is for or why it is needed.\n" +
                "4. Do not invent facts, requirements, audiences, examples or details the person did not give, and do not add qualities they did not ask for (such as engaging, relatable, detailed, official sources). Never turn relative times like \"this year\" into a different year; today is " + DateTime.Now.ToString("MMMM d, yyyy") + ". You may state the output form the request clearly implies (for example: return the updated code, give a bulleted list).\n" +
                "5. Apply the guidelines: lead with the task, then the context the person gave, then their constraints, then the output they expect.\n" +
                "6. Write in the person's voice, speaking to the assistant (\"I need...\", \"Write...\", \"Help me...\").\n" +
                "7. Keep the length proportional: a simple request stays two to four sentences; a long or multi-part request may use short labelled sections or a list.\n" +
                "8. Remove filler words, false starts and speech-recognition slips. Keep the person's language.\n\n" +
                "Example of the transformation (format only; never reuse its content):\n" +
                "Dictated: so my landing page loads slow on phones can you look at the images and tell me what to change, it's a react site\n" +
                "Prompt: My React landing page loads slowly on mobile phones, and I suspect the images are the cause. Review how the images are loaded and served, and tell me what to change to make the page load faster on mobile. Give the changes as a prioritized list, most impactful first.\n" +
                "Dictated: uh my node server that sends the invoices crashes when a customer has no email, make it skip them and log it, keep the same route names\n" +
                "Prompt: I have a Node.js server that sends invoices, and it crashes when a customer has no email address. Update it so it skips customers without an email and logs each one it skips. Keep the existing route names unchanged. Return the updated code.\n\n" +
                "Prompting guidelines for " + guide.Name + ":\n" + guide.Body;
        }

        public static void ValidateEndpoint(string endpoint)
        {
            Uri uri;
            if (!Uri.TryCreate(endpoint ?? "", UriKind.Absolute, out uri) || uri.Scheme != "http" || !(uri.IsLoopback)) throw new InvalidDataException("The prompt model must run on this computer (http://127.0.0.1 or http://localhost).");
        }

        // Ask the local model to load in the background so the first prompt is not slow.
        public static void Warm(Config config)
        {
            if (config.PromptMode == "Raw") return;
            System.Threading.ThreadPool.QueueUserWorkItem(_ => { try { Post(config, "/api/generate", new Dictionary<string, object> { { "model", config.PromptModel }, { "keep_alive", "30m" } }, 120000); } catch { } });
        }

        public static string Rewrite(Config config, PromptPlan plan)
        {
            var body = new Dictionary<string, object>
            {
                { "model", config.PromptModel }, { "stream", false }, { "think", false }, { "keep_alive", "30m" },
                { "options", new Dictionary<string, object> { { "temperature", 0.2 }, { "num_predict", 900 } } },
                { "messages", new object[] {
                    new Dictionary<string, object> { { "role", "system" }, { "content", Instructions(plan.Guide) } },
                    new Dictionary<string, object> { { "role", "user" }, { "content", "Dictated request:\n" + plan.Text } } } }
            };
            var response = Post(config, "/api/chat", body, 60000);
            var json = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(response);
            var message = json.ContainsKey("message") ? json["message"] as Dictionary<string, object> : null;
            string text = message != null && message.ContainsKey("content") ? message["content"] as string : null;
            text = Clean(text);
            if (String.IsNullOrWhiteSpace(text)) throw new InvalidDataException("The local model returned an empty prompt.");
            return text;
        }

        public static string Clean(string text)
        {
            text = Regex.Replace(text ?? "", @"<think>.*?</think>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase).Trim();
            text = Regex.Replace(text, @"^```[a-zA-Z]*\s*|\s*```$", "").Trim();
            text = Regex.Replace(text, @"^(Here is|Here's)[^\n]*prompt[^\n]*:\s*\n", "", RegexOptions.IgnoreCase).Trim();
            if (text.Length > 1 && (text[0] == '"' && text[text.Length - 1] == '"' || text[0] == '“' && text[text.Length - 1] == '”')) text = text.Substring(1, text.Length - 2).Trim();
            text = Regex.Replace(text.Replace("\r\n", "\n"), @"\n{3,}", "\n\n");
            return text;
        }

        static string Post(Config config, string path, object body, int timeout)
        {
            ValidateEndpoint(config.PromptEndpoint);
            var request = (HttpWebRequest)WebRequest.Create(config.PromptEndpoint.TrimEnd('/') + path);
            request.Method = "POST"; request.ContentType = "application/json"; request.Timeout = request.ReadWriteTimeout = timeout; request.Proxy = null;
            byte[] data = Encoding.UTF8.GetBytes(new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.Serialize(body));
            try
            {
                using (var stream = request.GetRequestStream()) stream.Write(data, 0, data.Length);
                using (var response = (HttpWebResponse)request.GetResponse()) using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) return reader.ReadToEnd();
            }
            catch (WebException ex)
            {
                if (ex.Response == null) throw new IOException("Ollama is not running at " + config.PromptEndpoint + ". Start Ollama, or switch AI prompts to Raw text.");
                using (var reader = new StreamReader(ex.Response.GetResponseStream())) throw new IOException("The local model could not write the prompt: " + reader.ReadToEnd());
            }
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
