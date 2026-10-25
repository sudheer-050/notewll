using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace Notewll
{
    static class SemanticSearch
    {
        public static string Root { get { return Path.Combine(Config.DirectoryPath, "SemanticSearch"); } }
        public static string Python { get { return Path.Combine(Root, "runtime", "Scripts", "python.exe"); } }
        public static List<ConversationHit> Find(List<ConversationHit> passages, string query)
        {
            if (!File.Exists(Python) || !Directory.Exists(Path.Combine(Root, "model"))) throw new IOException("Install semantic search in Preferences > Storage > Optional features first. Keyword search remains available.");
            if (passages.Count == 0) return new List<ConversationHit>();
            var json = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
            string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "engine", "semantic_worker.py");
            var info = new ProcessStartInfo(Python, "-u \"" + script + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = System.Text.Encoding.UTF8 };
            info.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            using (var process = Process.Start(info))
            {
                var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                try
                {
                    process.StandardInput.Write(json.Serialize(new { root = Root, query = query, texts = passages.Select(h => h.Text).ToArray() })); process.StandardInput.Close();
                    if (!process.WaitForExit(180000)) throw new IOException("Semantic search timed out. Narrow the library filter and retry.");
                    Task.WaitAll(output, error); var result = json.Deserialize<Result>(output.Result);
                    if (process.ExitCode != 0 || result == null || result.error != null) throw new IOException("Semantic search failed: " + (result == null ? "No result" : result.error));
                    return (result.hits ?? new List<Hit>()).Where(h => h.index >= 0 && h.index < passages.Count).Select(h => { passages[h.index].Score = h.score; return passages[h.index]; }).ToList();
                }
                finally { if (!process.HasExited) process.Kill(); }
            }
        }
        sealed class Result { public string error { get; set; } public List<Hit> hits { get; set; } }
        sealed class Hit { public int index { get; set; } public double score { get; set; } }
    }
}
