using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Notewll
{
    sealed partial class SettingsForm
    {
        static readonly string[] PromptModes = { "Raw", "WakeWord", "Automatic" };
        readonly ComboBox promptMode = new ThemedComboBox { DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "AI prompts" };
        readonly CheckBox promptReview = new ThemedCheckBox { Text = "Let me review and edit each prompt before it is typed", AutoSize = true };
        Label googleStatus; Control googleButton;

        void BuildPromptControls(Panel page, int top)
        {
            Label(page, "AI prompts", 20, top, 620, 26, 11, true, Ink);
            promptMode.Items.AddRange(new object[] { "Raw text - type exactly what I say", "Wake word - say \"Claude, ...\" or \"ChatGPT, ...\" first", "Automatic - when typing into an AI app or tab" });
            Setting(page, promptMode, top + 32);
            Label(page, "Rewrites your request into a prompt that follows that AI's own guidelines. Requires Sign in with Google; your request is sent to the Notewll prompt service and Google Gemini to be rewritten, and is not stored. Short messages and greetings are typed as spoken.", 20, top + 80, 620, 66, 9, false, Muted);
            googleStatus = Label(page, "", 20, top + 152, 400, 26, 10, false, Ink);
            googleButton = Button(page, "Sign in with Google", 420, top + 146, 220, async (s, e) =>
            {
                googleButton.Enabled = false;
                try
                {
                    if (GoogleAuth.SignedInEmail != null) { await Task.Run(() => GoogleAuth.SignOut()); }
                    else { googleStatus.Text = "Finish signing in in your browser..."; await Task.Run(() => GoogleAuth.SignIn(TimeSpan.FromMinutes(3))); }
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Sign in with Google"); }
                finally { googleButton.Enabled = true; RefreshGoogleStatus(); }
            });
            promptMode.SelectedIndexChanged += (s, e) => MarkDirty();
            Setting(page, promptReview, top + 192); promptReview.CheckedChanged += (s, e) => MarkDirty();
            Button(page, "Open prompt guides", 20, top + 236, 220, (s, e) => { try { PromptCrafter.EnsureGuides(); Process.Start(new ProcessStartInfo { FileName = PromptCrafter.Folder, UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Prompt guides"); } });
            Button(page, "Update prompt guides", 260, top + 236, 240, async (s, e) =>
            {
                var button = (Control)s; button.Enabled = false; string text = button.Text; button.Text = "Updating...";
                try { string result = await Task.Run(() => PromptCrafter.Update()); MessageBox.Show(this, "Prompt guides: " + result, "Prompt guides updated"); }
                catch (Exception ex) { MessageBox.Show(this, "Could not update the prompt guides. Your current guides are unchanged.\n\n" + ex.Message, "Prompt guides"); }
                finally { button.Enabled = true; button.Text = text; }
            });
            LoadPromptSettings(); RefreshGoogleStatus();
        }
        HubButton sidebarGoogle; bool googleBusy; ToolTip sidebarTip;
        // Account button at the bottom of the sidebar: "Sign in with Google", or the signed-in email with a Sign out menu.
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
                    menu.Show(sidebarGoogle, new System.Drawing.Point(0, -menu.PreferredSize.Height)); return;
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
            googleStatus.Text = !NotewllCloud.Configured ? "Google sign-in is not available in this build yet." : email == null ? "Not signed in. AI prompts are typed as spoken." : "Signed in as " + email;
            googleButton.Text = email == null ? "Sign in with Google" : "Sign out"; googleButton.Enabled = NotewllCloud.Configured || email != null;
        }
        void LoadPromptSettings()
        {
            if (promptMode.Items.Count == 0) return;
            promptMode.SelectedIndex = Math.Max(0, Array.IndexOf(PromptModes, source.PromptMode)); promptReview.Checked = source.PromptReview;
        }
        void SavePromptSettings(Config result)
        {
            result.PromptMode = PromptModes[Math.Max(0, promptMode.SelectedIndex)]; result.PromptReview = promptReview.Checked;
        }
    }
}
