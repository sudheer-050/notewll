using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Notewll
{
    sealed partial class SettingsForm
    {
        static readonly string[] PromptModes = { "Raw", "WakeWord", "Automatic" };
        readonly ComboBox promptMode = new ThemedComboBox { DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "AI prompts" };
        readonly ComboBox promptEngine = new ThemedComboBox { DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "AI that writes prompts", Width = 600 };
        readonly CheckBox promptReview = new ThemedCheckBox { Text = "Let me review and edit each prompt before it is typed", AutoSize = true };
        readonly TextBox engineKey = new TextBox { UseSystemPasswordChar = true, Width = 300, AccessibleName = "API key" };
        readonly TextBox engineModel = new TextBox { Width = 300, AccessibleName = "Model" };
        readonly TextBox engineUrl = new TextBox { Width = 300, AccessibleName = "Provider base URL" };
        Label googleStatus, engineNote, keyStatus, keyLabel, modelLabel, urlLabel; Control googleButton, saveKeyButton;

        void BuildPromptControls(Panel page, int top)
        {
            Label(page, "AI prompts", 20, top, 620, 26, 11, true, Ink);
            promptMode.Items.AddRange(new object[] { "Raw text - type exactly what I say", "Wake word - say \"Claude, ...\" or \"ChatGPT, ...\" first", "Automatic - when typing into an AI app or tab" });
            Setting(page, promptMode, top + 32);
            Label(page, "Rewrites your request into a prompt that follows that AI's own guidelines. Greetings and short messages are typed as spoken, and the wake word is never typed.", 20, top + 78, 620, 44, 9, false, Muted);

            Label(page, "AI that writes prompts", 20, top + 128, 620, 26, 10, true, Ink);
            promptEngine.Items.AddRange(AiEngines.Labels); Setting(page, promptEngine, top + 158);
            engineNote = Label(page, "", 20, top + 204, 620, 44, 9, false, Muted);
            // Notewll engine: Google sign-in.
            googleStatus = Label(page, "", 20, top + 256, 400, 26, 10, false, Ink);
            googleButton = Button(page, "Sign in with Google", 420, top + 250, 220, async (s, e) =>
            {
                googleButton.Enabled = false;
                try { if (GoogleAuth.SignedInEmail != null) await Task.Run(() => GoogleAuth.SignOut()); else { googleStatus.Text = "Finish signing in in your browser..."; await Task.Run(() => GoogleAuth.SignIn(TimeSpan.FromMinutes(3))); } }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Sign in with Google"); }
                finally { googleButton.Enabled = true; RefreshGoogleStatus(); }
            });
            // Own-key engines: key, model, base URL.
            keyLabel = Label(page, "API key", 20, top + 256, 120, 26, 10, false, Ink); engineKey.Location = new Point(140, top + 254); page.Controls.Add(engineKey);
            saveKeyButton = Button(page, "Save key", 460, top + 248, 180, (s, e) =>
            {
                string id = SelectedEngine();
                try { AiEngines.SetKey(id, engineKey.Text); engineKey.Text = ""; } catch (Exception ex) { MessageBox.Show(this, ex.Message, "API key"); }
                RefreshEngineControls();
            });
            keyStatus = Label(page, "", 140, top + 284, 500, 22, 9, false, Muted);
            modelLabel = Label(page, "Model", 20, top + 316, 120, 26, 10, false, Ink); engineModel.Location = new Point(140, top + 314); page.Controls.Add(engineModel);
            urlLabel = Label(page, "Base URL", 20, top + 352, 120, 26, 10, false, Ink); engineUrl.Location = new Point(140, top + 350); page.Controls.Add(engineUrl);
            promptEngine.SelectedIndexChanged += (s, e) => { if (!loadingEngine) { engineModel.Text = ""; MarkDirty(); } RefreshEngineControls(); };
            engineModel.TextChanged += (s, e) => { if (!loadingEngine) MarkDirty(); }; engineUrl.TextChanged += (s, e) => { if (!loadingEngine) MarkDirty(); };
            promptMode.SelectedIndexChanged += (s, e) => { if (!loadingEngine) MarkDirty(); };

            Setting(page, promptReview, top + 392); promptReview.CheckedChanged += (s, e) => { if (!loadingEngine) MarkDirty(); };
            Label(page, "Prompt guides are files on this PC. Checking for updates or adding an AI sends only the AI's name to the AI chosen above.", 20, top + 432, 620, 40, 9, false, Muted);
            Button(page, "Open prompt guides", 20, top + 476, 200, (s, e) => { try { PromptCrafter.EnsureGuides(); Process.Start(new ProcessStartInfo { FileName = PromptCrafter.Folder, UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Prompt guides"); } });
            Button(page, "Check all AIs for updates", 230, top + 476, 230, (s, e) => CheckAllGuides());
            Button(page, "Add an AI...", 470, top + 476, 170, (s, e) => AddAiGuide());
            LoadPromptSettings(); RefreshGoogleStatus();
        }
        bool loadingEngine;
        string SelectedEngine() { return AiEngines.Ids[Math.Max(0, promptEngine.SelectedIndex)]; }
        Config EngineConfig() { var c = source.Copy(); c.PromptEngine = SelectedEngine(); c.PromptEngineModel = engineModel.Text.Trim(); c.PromptEngineUrl = engineUrl.Text.Trim(); return c; }
        void RefreshEngineControls()
        {
            if (engineNote == null || promptEngine.SelectedIndex < 0) return;
            string id = SelectedEngine(); bool own = AiEngines.NeedsKey(id);
            engineNote.Text = id == "Notewll" ? "Free. Your request is sent to the Notewll prompt service and Google Gemini to be rewritten, and is not stored." :
                id == "Gemini" ? "Your request goes straight from this PC to Google. Free keys at aistudio.google.com; on Google's free tier, Google may use requests to improve its products." :
                id == "Compatible" ? "Your request goes straight from this PC to the provider at the URL below. This option cannot search the web for new guides." :
                "Your request goes straight from this PC to the provider using your key. Notewll's server is not involved.";
            foreach (var c in new Control[] { googleStatus, googleButton }) c.Visible = !own;
            foreach (var c in new Control[] { keyLabel, engineKey, saveKeyButton, keyStatus, modelLabel, engineModel }) c.Visible = own;
            urlLabel.Visible = engineUrl.Visible = id == "Compatible";
            keyStatus.Text = own ? (AiEngines.HasKey(id) ? "A key is saved, encrypted for your Windows account. Paste a new one to replace it, or save empty to remove it." : "No key saved yet.") : "";
            if (own && engineModel.Text.Length == 0) engineModel.Text = AiEngines.DefaultModel(id);
        }
        HubButton sidebarGoogle; bool googleBusy; ToolTip sidebarTip;
        // Account button at the bottom of the sidebar: "Google sign-in", or the signed-in name with a Sign out menu.
        void BuildSidebarGoogle(Panel sidebar)
        {
            sidebarGoogle = new HubButton { NavigationIcon = "", Left = 18, Width = 158, Height = 34, AccessibleName = "Google account" };
            sidebarGoogle.Click += async (s, e) =>
            {
                if (googleBusy) return;
                string email = GoogleAuth.SignedInEmail;
                if (email != null)
                {
                    var menu = DialogUi.PopupMenu();
                    menu.Items.Add("Signed in as " + email).Enabled = false;
                    menu.Items.Add("Sign out", null, async (m, a) => { googleBusy = true; try { await Task.Run(() => GoogleAuth.SignOut()); } finally { googleBusy = false; RefreshGoogleStatus(); } });
                    menu.Show(sidebarGoogle, new Point(0, -menu.PreferredSize.Height)); return;
                }
                if (!NotewllCloud.Configured) { MessageBox.Show(this, "Google sign-in is not available in this build of Notewll yet.", "Sign in with Google"); return; }
                googleBusy = true; sidebarGoogle.Text = "Check browser…";
                try { await Task.Run(() => GoogleAuth.SignIn(TimeSpan.FromMinutes(3))); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Sign in with Google"); }
                finally { googleBusy = false; RefreshGoogleStatus(); }
            };
            sidebar.Controls.Add(sidebarGoogle); RefreshGoogleStatus();
        }
        void RefreshGoogleStatus()
        {
            if (sidebarGoogle != null)
            {
                string signedIn = GoogleAuth.SignedInEmail;
                string name = signedIn == null ? null : signedIn.Split('@')[0];
                sidebarGoogle.Text = signedIn == null ? "Google sign-in" : name.Length > 13 ? name.Substring(0, 12) + "…" : name;
                if (sidebarTip == null) sidebarTip = new ToolTip();
                sidebarTip.SetToolTip(sidebarGoogle, signedIn == null ? "Sign in with Google to turn spoken requests into AI prompts" : "Signed in as " + signedIn + ". Click to sign out.");
            }
            if (googleStatus == null) return;
            string email = GoogleAuth.SignedInEmail;
            googleStatus.Text = !NotewllCloud.Configured ? "Google sign-in is not available in this build yet." : email == null ? "Not signed in." : "Signed in as " + email;
            googleButton.Text = email == null ? "Sign in with Google" : "Sign out"; googleButton.Enabled = NotewllCloud.Configured || email != null;
        }
        void LoadPromptSettings()
        {
            if (promptMode.Items.Count == 0) return;
            loadingEngine = true;
            promptMode.SelectedIndex = Math.Max(0, Array.IndexOf(PromptModes, source.PromptMode)); promptReview.Checked = source.PromptReview;
            promptEngine.SelectedIndex = Math.Max(0, Array.IndexOf(AiEngines.Ids, source.PromptEngine ?? "Notewll"));
            engineModel.Text = source.PromptEngineModel ?? ""; engineUrl.Text = source.PromptEngineUrl ?? "";
            RefreshEngineControls(); loadingEngine = false;
        }
        void SavePromptSettings(Config result)
        {
            result.PromptMode = PromptModes[Math.Max(0, promptMode.SelectedIndex)]; result.PromptReview = promptReview.Checked;
            result.PromptEngine = SelectedEngine(); result.PromptEngineModel = engineModel.Text.Trim(); result.PromptEngineUrl = engineUrl.Text.Trim();
            if (result.PromptEngineModel == AiEngines.DefaultModel(result.PromptEngine)) result.PromptEngineModel = "";
        }

        // ---- Guide research ----
        void AddAiGuide()
        {
            var cfg = EngineConfig(); string why = AiEngines.NotReady(cfg);
            if (why != null) { MessageBox.Show(this, why, "Add an AI"); return; }
            string name;
            using (var dialog = DialogUi.Create("Add an AI", new Size(520, 220)))
            {
                var box = new TextBox { Dock = DockStyle.Top, Font = new Font("Segoe UI", 11), AccessibleName = "AI name" };
                var info = DialogUi.Text("Which AI assistant? For example DeepSeek, Mistral Le Chat or Midjourney. Notewll looks up its prompting guidance and saves a guide on this PC."); info.Dock = DockStyle.Top; info.AutoSize = false; info.Height = 64;
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
                var ok = DialogUi.Button("Get guide", (s, e) => dialog.DialogResult = DialogResult.OK, true); buttons.Controls.Add(ok); buttons.Controls.Add(DialogUi.Button("Cancel", (s, e) => dialog.Close()));
                dialog.AcceptButton = ok; dialog.Controls.Add(buttons); dialog.Controls.Add(box); dialog.Controls.Add(info); ThemeColors.Apply(dialog, source.Theme);
                if (dialog.ShowDialog(this) != DialogResult.OK) return; name = box.Text.Trim();
            }
            if (name.Length == 0 || name.Length > 60) return;
            RunGuideWork("Looking up " + name + "…", () =>
            {
                var guide = PromptCrafter.SaveGuide(AiEngines.Research(cfg, name, null));
                return "Added a prompt guide for " + guide.Name + ". Say \"" + (guide.Wake.FirstOrDefault() ?? guide.Name) + ", ...\" to use it.";
            });
        }
        void CheckAllGuides()
        {
            var cfg = EngineConfig(); string why = AiEngines.NotReady(cfg);
            if (why != null) { MessageBox.Show(this, why, "Check for updates"); return; }
            var guides = PromptCrafter.Load().Where(g => g.Name != "General AI").ToList();
            var updates = new List<KeyValuePair<PromptGuide, AiEngines.Guide>>(); var failed = new List<string>();
            RunGuideWork("Checking " + guides.Count + " AIs…", () =>
            {
                foreach (var g in guides)
                {
                    try { var latest = AiEngines.Research(cfg, g.Name, System.IO.File.ReadAllText(g.File)); if (PromptCrafter.Changed(g, latest)) updates.Add(new KeyValuePair<PromptGuide, AiEngines.Guide>(g, latest)); }
                    catch (Exception ex) { failed.Add(g.Name + " (" + ex.Message + ")"); }
                }
                return null;
            }, () =>
            {
                if (updates.Count == 0) { MessageBox.Show(this, "All prompt guides are up to date." + (failed.Count > 0 ? "\n\nCould not check: " + String.Join(", ", failed) : ""), "Prompt guides"); return; }
                using (var dialog = DialogUi.Create("Prompt guide updates", new Size(560, 420)))
                {
                    var list = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, Font = new Font("Segoe UI", 10) };
                    foreach (var u in updates) list.Items.Add(u.Key.Name + (u.Value.Sources.Count > 0 ? "  (from " + String.Join(", ", u.Value.Sources.Take(2)) + ")" : ""), true);
                    var info = DialogUi.Text("New guidance was found for these AIs. Checked guides will be updated; your current versions are saved in Prompt guides\\Backups." + (failed.Count > 0 ? " Could not check: " + String.Join(", ", failed) + "." : "")); info.Dock = DockStyle.Top; info.AutoSize = false; info.Height = 70;
                    var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
                    buttons.Controls.Add(DialogUi.Button("Update checked", (s, e) =>
                    {
                        int n = 0; for (int i = 0; i < updates.Count; i++) if (list.GetItemChecked(i)) { updates[i].Value.Name = updates[i].Key.Name; PromptCrafter.SaveGuide(updates[i].Value); n++; }
                        dialog.Close(); MessageBox.Show(this, n + " prompt guide(s) updated.", "Prompt guides");
                    }, true));
                    buttons.Controls.Add(DialogUi.Button("Cancel", (s, e) => dialog.Close()));
                    dialog.Controls.Add(list); dialog.Controls.Add(info); dialog.Controls.Add(buttons); ThemeColors.Apply(dialog, source.Theme); dialog.ShowDialog(this);
                }
            });
        }
        async void RunGuideWork(string busy, Func<string> work, Action done = null)
        {
            Cursor = Cursors.WaitCursor; string result = null; Exception error = null;
            var status = sidebarStatus; string old = status.Text; status.Text = busy; status.Visible = true;
            try { result = await Task.Run(work); } catch (Exception ex) { error = ex; }
            finally { Cursor = Cursors.Default; status.Text = old; }
            if (error != null) { MessageBox.Show(this, error.Message, "Prompt guides"); return; }
            if (result != null) MessageBox.Show(this, result, "Prompt guides");
            if (done != null) done();
        }
    }
}
