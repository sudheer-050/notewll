using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace Notewll
{
    static partial class NotesStorageDialog
    {
        static void RunSetup(string name, string arguments = "")
        {
            string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "engine", name); if (!File.Exists(script)) throw new IOException("Setup script missing.");
            using (var process = Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" -DataRoot \"" + Config.DirectoryPath + "\"" + arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
            {
                var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync(); if (!process.WaitForExit(1200000)) { process.Kill(); throw new IOException("Setup timed out."); } Task.WaitAll(output, error); if (process.ExitCode != 0) throw new IOException("Setup failed: " + error.Result);
            }
        }
        static void AddFeatureManager(AppTabs tabs, Form owner, Config config)
        {
            var page = new TabPage("Optional features"); tabs.TabPages.Add(page);
            bool operating = false; owner.FormClosing += (s, e) => { if (operating) e.Cancel = true; };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16) }; page.Controls.Add(flow);
            var status = DialogUi.Text(""); status.MaximumSize = new Size(650, 0);
            Action refresh = () => {
                long installed = NotesData.Bytes(SemanticSearch.Root), cache = NotesData.Bytes(Path.Combine(SemanticSearch.Root, "downloads"));
                string measure = Path.Combine(SemanticSearch.Root, "last-measurement.json"), install = Path.Combine(SemanticSearch.Root, "installation.json");
                status.Text = "Semantic search: " + (File.Exists(SemanticSearch.Python) && Directory.Exists(Path.Combine(SemanticSearch.Root, "model")) ? "Installed" : "Not ready") + "\r\nInstalled disk: " + NotesData.Size(Math.Max(0, installed - cache)) + " | Download cache: " + NotesData.Size(cache) + "\r\n" + (File.Exists(install) ? File.ReadAllText(install) : "Download size: not yet measured. Setup measures package files; model network bytes are unavailable when hub cache is reused.") + "\r\n" + (File.Exists(measure) ? "Last measured worker RAM and search duration:\r\n" + File.ReadAllText(measure) : "Worker RAM: not yet measured. Run a semantic search first.") + "\r\nApp RAM now: " + NotesData.Size(Process.GetCurrentProcess().WorkingSet64) + ". Worker exits after each search.";
            }; flow.Controls.Add(status);
            flow.Controls.Add(DialogUi.Text("Install downloads the local runtime and embedding model. Search itself runs offline. Removing this feature preserves notes, recordings, tasks and bookmarks."));
            flow.Controls.Add(DialogUi.Button("Install / repair semantic search", async (s, e) => {
                var button = (Control)s; if (operating) return; operating = true; tabs.Enabled = false; button.Enabled = false;
                try {
                    status.Text = "Installing local semantic search. This can take several minutes...";
                    await Task.Run(() => RunSetup("setup_semantic.ps1")); refresh();
                } catch (Exception ex) { status.Text = ex.Message; } finally { operating = false; tabs.Enabled = true; if (!button.IsDisposed) button.Enabled = true; }
            }));
            flow.Controls.Add(DialogUi.Button("Remove semantic search", (s, e) => { if (!Directory.Exists(SemanticSearch.Root) || MessageBox.Show(owner, "Remove the semantic runtime, model and its download cache? Saved notes and recordings stay in place.", "Remove optional feature", MessageBoxButtons.YesNo) != DialogResult.Yes) return; try { NotesData.DeleteTree(SemanticSearch.Root, Config.DirectoryPath); refresh(); } catch (Exception ex) { MessageBox.Show(owner, ex.Message); } }));
            string voiceRoot = Path.Combine(Config.DirectoryPath, "VoiceMatching");
            var voiceStatus = DialogUi.Text("Voice matching managed runtime: " + NotesData.Size(NotesData.Bytes(voiceRoot)) + ". Shared Python packages and model caches are separate. Download and RAM measurements for this existing feature are unavailable."); voiceStatus.MaximumSize = new Size(650, 0); flow.Controls.Add(voiceStatus);
            flow.Controls.Add(DialogUi.Button("Install / repair voice matching", async (s, e) => {
                var button = (Control)s; if (operating) return; operating = true; tabs.Enabled = false; button.Enabled = false;
                try { await Task.Run(() => RunSetup("setup_handsfree.ps1")); voiceStatus.Text = "Voice matching is ready. Managed runtime: " + NotesData.Size(NotesData.Bytes(voiceRoot)); }
                catch (Exception ex) { voiceStatus.Text = ex.Message; } finally { operating = false; tabs.Enabled = true; if (!button.IsDisposed) button.Enabled = true; }
            }));
            flow.Controls.Add(DialogUi.Button("Remove managed voice matching runtime", async (s, e) => {
                if (!Directory.Exists(voiceRoot) || MessageBox.Show(owner, "Remove the managed voice matching runtime? Notes, recordings, voice profile and shared Python packages are kept.", "Remove optional feature", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                try { await Task.Run(() => { MyVoiceMatcher.Shutdown(); NotesData.DeleteTree(voiceRoot, Config.DirectoryPath); }); voiceStatus.Text = "Managed voice matching runtime removed. Shared environments, if configured, remain available."; }
                catch (Exception ex) { MessageBox.Show(owner, ex.Message); }
            }));
            var speechStatus = DialogUi.Text("Optional accurate dictation and enhanced meeting speech need Python 3.10 or later and a one-time model download. Accurate speech downloads about 2.5 GB. Standard English speech is already bundled. Setup also prepares any speech feature you already enabled in the shared runtime."); speechStatus.MaximumSize = new Size(650, 0); flow.Controls.Add(speechStatus);
            foreach (string feature in new[] { "Accurate", "Enhanced", "Multilingual" })
            {
                string selectedFeature = feature;
                flow.Controls.Add(DialogUi.Button(feature == "Accurate" ? "Set up accurate speech (about 2.5 GB)" : feature == "Enhanced" ? "Set up enhanced meeting speech" : "Set up multilingual speech (base model)", async (s, e) => {
                    if (operating) return; operating = true; tabs.Enabled = false;
                    try
                    {
                        speechStatus.Text = "Preparing " + selectedFeature.ToLowerInvariant() + " speech. Downloads may take several minutes...";
                        string arguments = " -Feature " + selectedFeature + (config.AccurateDictationEnabled ? " -IncludeAccurate" : "") + (config.QualityEnabled ? " -IncludeEnhanced" : "");
                        await Task.Run(() => RunSetup("setup_optional_speech.ps1", arguments));
                        string python = Path.Combine(Config.DirectoryPath, "SpeechRuntime", "Scripts", "python.exe");
                        if (!File.Exists(python)) throw new IOException("The optional speech runtime is incomplete. Run setup again.");
                        var candidate = config.Copy(); candidate.QualityPython = python; if (selectedFeature == "Multilingual") candidate.SpeechLanguage = "auto";
                        await Task.Run(() => { if (candidate.SpeechLanguage == "en" && (selectedFeature == "Accurate" || candidate.AccurateDictationEnabled)) AccurateSpeech.Warmup(candidate); if (selectedFeature == "Enhanced" || selectedFeature == "Multilingual" || candidate.QualityEnabled) { candidate.QualityEnabled = true; SpeechQuality.Recognize(candidate, new byte[0], false); } });
                        var saved = Config.Load(); saved.QualityPython = python; saved.Save(); config.QualityPython = python;
                        speechStatus.Text = selectedFeature + " speech is installed. Enable it in Preferences > Writing, then save.";
                    }
                    catch (Exception ex) { speechStatus.Text = ex.Message; }
                    finally { operating = false; tabs.Enabled = true; }
                }));
            }
            string external = config.QualityPython;
            if (!String.IsNullOrWhiteSpace(external) && File.Exists(external)) flow.Controls.Add(DialogUi.Text("Enhanced speech uses a shared Python environment: " + external + ". It is managed separately to preserve other features using it. Disable enhanced speech or accurate dictation in Preferences when you do not need them."));
            flow.Controls.Add(DialogUi.Button("Refresh measurements", (s, e) => refresh())); refresh();
        }
    }
}
