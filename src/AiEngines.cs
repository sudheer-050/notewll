using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Notewll
{
    // Which AI writes prompts and researches prompt guides. "Notewll" uses the free Notewll service (Sign in with Google,
    // Gemini on the developer's key). The others call the provider directly from this PC with the user's own API key.
    static class AiEngines
    {
        // "InChat": the AI the person is typing into writes the prompt in their own chat (free, private, nothing to set up).
        // "Notewll" (the hosted Gemini service) is kept in code but no longer offered; old settings move to InChat.
        public static readonly string[] Ids = { "InChat", "Gemini", "Anthropic", "OpenAI", "Compatible" };
        public static readonly string[] Labels = {
            "The AI I'm typing into (free, private) - improved prompt, missing details, then the answer",
            "Google Gemini - my API key",
            "Anthropic Claude - my API key",
            "OpenAI - my API key",
            "OpenAI-compatible (OpenRouter, Groq, Mistral, DeepSeek, LM Studio...) - my API key",
        };
        public static string DefaultModel(string id)
        {
            switch (id) { case "Gemini": return "gemini-2.5-flash"; case "Anthropic": return "claude-opus-5-5"; case "OpenAI": return "gpt-5-mini"; default: return ""; }
        }
        public static bool NeedsKey(string id) { return id != "Notewll" && id != "InChat"; }
        public static bool CanSearchWeb(string id) { return id != "Compatible" && id != "InChat"; }
        public static bool Researches(string id) { return id != "InChat"; }
        public static string Model(Config c) { return String.IsNullOrWhiteSpace(c.PromptEngineModel) ? DefaultModel(c.PromptEngine) : c.PromptEngineModel.Trim(); }

        // ---- API keys: stored per provider, encrypted with Windows DPAPI for the current user only ----
        static string KeyPath { get { return Path.Combine(Config.DirectoryPath, "ai-keys.bin"); } }
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Notewll AI provider keys");
        static Dictionary<string, string> Keys()
        {
            try { if (!File.Exists(KeyPath)) return new Dictionary<string, string>(); var plain = ProtectedData.Unprotect(File.ReadAllBytes(KeyPath), Entropy, DataProtectionScope.CurrentUser); return new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(Encoding.UTF8.GetString(plain)) ?? new Dictionary<string, string>(); }
            catch { return new Dictionary<string, string>(); }
        }
        public static bool HasKey(string id) { string k; return Keys().TryGetValue(id, out k) && !String.IsNullOrEmpty(k); }
        public static void SetKey(string id, string key)
        {
            var keys = Keys(); if (String.IsNullOrWhiteSpace(key)) keys.Remove(id); else keys[id] = key.Trim();
            Directory.CreateDirectory(Config.DirectoryPath);
            File.WriteAllBytes(KeyPath, ProtectedData.Protect(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(keys)), Entropy, DataProtectionScope.CurrentUser));
        }
        static string Key(string id)
        {
            string k; if (Keys().TryGetValue(id, out k) && !String.IsNullOrEmpty(k)) return k;
            throw new InvalidOperationException("Add your " + Labels[Array.IndexOf(Ids, id)].Split('-')[0].Trim() + " API key in Preferences > Writing > AI prompts.");
        }

        // Is the chosen engine ready? Returns a reason when it is not.
        public static string NotReady(Config c)
        {
            if (c.PromptEngine == "InChat") return null;
            if (c.PromptEngine == "Notewll") return GoogleAuth.SignedInEmail == null ? "Sign in with Google in Preferences > Writing to use AI prompts." : null;
            if (!HasKey(c.PromptEngine)) return "Add your API key in Preferences > Writing > AI prompts.";
            if (c.PromptEngine == "Compatible" && (String.IsNullOrWhiteSpace(c.PromptEngineUrl) || String.IsNullOrWhiteSpace(c.PromptEngineModel))) return "Enter the base URL and model for your OpenAI-compatible provider.";
            return null;
        }

        // ---- The two jobs ----
        public static string Rewrite(Config c, PromptGuide guide, string request)
        {
            if (c.PromptEngine == "InChat") return PromptCrafter.InChatRequest(guide, request); // no network: the chat's own AI does it
            if (c.PromptEngine == "Notewll") return PromptCrafter.RewriteViaService(guide, request);
            string system = PromptCrafter.Instructions(guide), user = "Dictated request:\n" + request;
            return PromptCrafter.Clean(Complete(c, system, user, false));
        }
        public sealed class Guide { public string Name, Text; public List<string> Sources = new List<string>(); }
        public static Guide Research(Config c, string ai, string current)
        {
            if (c.PromptEngine == "InChat") throw new InvalidOperationException("Looking up guides needs an AI with your own key; with the AI you're typing into, it applies its own best practices.");
            if (c.PromptEngine == "Notewll") return PromptCrafter.ResearchViaService(ai, current);
            var sources = new List<string>();
            string text = Complete(c, null, PromptCrafter.ResearchInstructions(ai, current, CanSearchWeb(c.PromptEngine)), CanSearchWeb(c.PromptEngine), sources);
            var parsed = PromptCrafter.ParseResearched(text);
            if (parsed == null) throw new IOException("Could not find reliable prompting guidance for \"" + ai + "\".");
            parsed.Sources = sources; return parsed;
        }

        static string Complete(Config c, string system, string user, bool webSearch, List<string> sources = null)
        {
            switch (c.PromptEngine)
            {
                case "Gemini": return Gemini(c, system, user, webSearch, sources);
                case "Anthropic": return Claude(c, system, user, webSearch, sources);
                case "OpenAI": return webSearch ? OpenAiResponses(c, system, user, sources) : OpenAiChat(c, "https://api.openai.com/v1", system, user);
                case "Compatible": return OpenAiChat(c, c.PromptEngineUrl.TrimEnd('/'), system, user);
                default: throw new InvalidOperationException("Choose an AI for prompts in Preferences > Writing.");
            }
        }

        // Google Gemini (generateContent); Google Search grounding for research.
        static string Gemini(Config c, string system, string user, bool webSearch, List<string> sources)
        {
            var body = new Dictionary<string, object> { { "contents", new object[] { new Dictionary<string, object> { { "role", "user" }, { "parts", new object[] { new Dictionary<string, object> { { "text", user } } } } } } } };
            if (system != null) body["systemInstruction"] = new Dictionary<string, object> { { "parts", new object[] { new Dictionary<string, object> { { "text", system } } } } };
            if (webSearch) body["tools"] = new object[] { new Dictionary<string, object> { { "google_search", new Dictionary<string, object>() } } };
            body["generationConfig"] = new Dictionary<string, object> { { "temperature", 0.2 }, { "maxOutputTokens", 2000 } };
            var json = Post("https://generativelanguage.googleapis.com/v1beta/models/" + Uri.EscapeDataString(Model(c)) + ":generateContent", body, new Dictionary<string, string> { { "x-goog-api-key", Key("Gemini") } }, webSearch ? 90000 : 30000);
            var cand = List(json, "candidates").Select(Dict).FirstOrDefault();
            if (cand == null) throw new IOException("Gemini returned no answer.");
            if (sources != null) foreach (var chunk in List(Dict(Get(cand, "groundingMetadata")), "groundingChunks").Select(Dict)) { var web = Dict(Get(chunk, "web")); var title = Get(web, "title") as string; if (!String.IsNullOrEmpty(title) && !sources.Contains(title)) sources.Add(title); }
            return String.Join("", List(Dict(Get(cand, "content")), "parts").Select(p => Get(Dict(p), "text") as string ?? ""));
        }

        // Anthropic Messages API (raw HTTPS: this app builds on .NET Framework without NuGet, so the C# SDK is not available).
        static string Claude(Config c, string system, string user, bool webSearch, List<string> sources)
        {
            var messages = new List<object> { new Dictionary<string, object> { { "role", "user" }, { "content", user } } };
            var headers = new Dictionary<string, string> { { "x-api-key", Key("Anthropic") }, { "anthropic-version", "2026-10-21" } };
            string model = Model(c);
            bool fallback = new[] { "claude-opus-5-5", "claude-opus-5", "claude-fable-5-2", "claude-fable-5-1", "claude-sonnet-5-5" }.Contains(model);
            if (fallback) headers["anthropic-beta"] = "server-side-fallback-2026-07-01";
            for (int turn = 0; turn < 4; turn++) // web search may pause a long turn; resend to let it continue
            {
                var body = new Dictionary<string, object> { { "model", model }, { "max_tokens", 16000 }, { "messages", messages }, { "output_config", new Dictionary<string, object> { { "effort", webSearch ? "medium" : "low" } } } };
                if (system != null) body["system"] = system;
                if (fallback) body["fallbacks"] = "default";
                if (webSearch) body["tools"] = new object[] { new Dictionary<string, object> { { "type", "web_search_20260209" }, { "name", "web_search" }, { "max_uses", 5 } } };
                var json = Post("https://api.anthropic.com/v1/messages", body, headers, webSearch ? 180000 : 60000);
                string stop = Get(json, "stop_reason") as string;
                var content = List(json, "content").Select(Dict).ToList();
                if (stop == "refusal") throw new IOException("Claude declined to write this prompt. Your words were typed as spoken.");
                if (sources != null)
                    foreach (var block in content.Where(b => (Get(b, "type") as string) == "web_search_tool_result"))
                        foreach (var result in (Get(block, "content") as System.Collections.ArrayList ?? new System.Collections.ArrayList()).Cast<object>().Select(Dict))
                        { var title = Get(result, "title") as string; if (!String.IsNullOrEmpty(title) && !sources.Contains(title) && sources.Count < 6) sources.Add(title); }
                if (stop == "pause_turn") { messages.Add(new Dictionary<string, object> { { "role", "assistant" }, { "content", List(json, "content") } }); continue; }
                // The answer is the text after the last tool activity.
                int last = content.FindLastIndex(b => (Get(b, "type") as string) != "text");
                return String.Join("", content.Skip(last + 1).Where(b => (Get(b, "type") as string) == "text").Select(b => Get(b, "text") as string ?? ""));
            }
            throw new IOException("Claude did not finish researching. Try again.");
        }

        // OpenAI Chat Completions (also any OpenAI-compatible endpoint).
        static string OpenAiChat(Config c, string baseUrl, string system, string user)
        {
            var messages = new List<object>(); if (system != null) messages.Add(new Dictionary<string, object> { { "role", "system" }, { "content", system } });
            messages.Add(new Dictionary<string, object> { { "role", "user" }, { "content", user } });
            string id = c.PromptEngine == "Compatible" ? "Compatible" : "OpenAI";
            var json = Post(baseUrl + "/chat/completions", new Dictionary<string, object> { { "model", Model(c) }, { "messages", messages } }, new Dictionary<string, string> { { "Authorization", "Bearer " + Key(id) } }, 60000);
            var choice = List(json, "choices").Select(Dict).FirstOrDefault();
            return choice == null ? "" : Get(Dict(Get(choice, "message")), "content") as string ?? "";
        }
        // OpenAI Responses API with web search, for guide research.
        static string OpenAiResponses(Config c, string system, string user, List<string> sources)
        {
            var body = new Dictionary<string, object> { { "model", Model(c) }, { "input", user }, { "tools", new object[] { new Dictionary<string, object> { { "type", "web_search" } } } } };
            if (system != null) body["instructions"] = system;
            var json = Post("https://api.openai.com/v1/responses", body, new Dictionary<string, string> { { "Authorization", "Bearer " + Key("OpenAI") } }, 180000);
            var text = new StringBuilder();
            foreach (var item in List(json, "output").Select(Dict).Where(o => (Get(o, "type") as string) == "message"))
                foreach (var part in List(item, "content").Select(Dict).Where(p => (Get(p, "type") as string) == "output_text"))
                {
                    text.Append(Get(part, "text") as string);
                    if (sources != null) foreach (var a in List(part, "annotations").Select(Dict)) { var title = Get(a, "title") as string; if (!String.IsNullOrEmpty(title) && !sources.Contains(title) && sources.Count < 6) sources.Add(title); }
                }
            return text.ToString();
        }

        // ---- HTTP + JSON helpers ----
        static Dictionary<string, object> Post(string url, object body, Dictionary<string, string> headers, int timeout)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            string testBase = Environment.GetEnvironmentVariable("NOTEWLL_TEST_API_BASE"); // tests only: send provider calls to a local fake
            if (!String.IsNullOrEmpty(testBase)) { var u = new Uri(url); url = testBase.TrimEnd('/') + "/" + u.Host + u.PathAndQuery; }
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST"; request.ContentType = "application/json"; request.Timeout = request.ReadWriteTimeout = timeout; request.UserAgent = "Notewll";
            foreach (var h in headers) request.Headers[h.Key] = h.Value;
            byte[] data = Encoding.UTF8.GetBytes(new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.Serialize(body));
            try
            {
                using (var stream = request.GetRequestStream()) stream.Write(data, 0, data.Length);
                using (var response = request.GetResponse()) using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    return new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response == null) throw new IOException("Could not reach " + new Uri(url).Host + ". Check your internet connection.");
                string detail = ""; try { using (var reader = new StreamReader(response.GetResponseStream())) detail = reader.ReadToEnd(); } catch { }
                var m = Regex.Match(detail, "\"message\"\\s*:\\s*\"([^\"]{1,240})");
                int code = (int)response.StatusCode;
                throw new IOException(code == 401 || code == 403 ? "The API key was rejected. Check it in Preferences > Writing." : code == 429 ? "The AI provider is rate limiting or out of credit. Try again later." : new Uri(url).Host + " returned " + code + (m.Success ? ": " + m.Groups[1].Value : "."));
            }
        }
        static Dictionary<string, object> Dict(object o) { return o as Dictionary<string, object> ?? new Dictionary<string, object>(); }
        static object Get(Dictionary<string, object> d, string key) { object v; return d != null && d.TryGetValue(key, out v) ? v : null; }
        static IEnumerable<object> List(Dictionary<string, object> d, string key) { return (Get(d, key) as System.Collections.ArrayList ?? new System.Collections.ArrayList()).Cast<object>(); }
    }
}
