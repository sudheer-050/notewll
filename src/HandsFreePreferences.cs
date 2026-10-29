using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Notewll
{
    sealed partial class SettingsForm
    {
        public Action ToggleHandsFree, PauseHandsFree;
        Label handsFreeStatus; HubButton handsFreeToggle;
        public void SetHandsFreeStatus(bool listening, string message)
        {
            if (handsFreeStatus != null) handsFreeStatus.Text = message;
            if (handsFreeToggle != null) handsFreeToggle.Text = listening ? "Pause hands-free" : "Start hands-free";
        }
        void BuildHandsFreePreferences()
        {
            var panel = SettingsTab("Hands-free");
            Label(panel, "Hands-free - my voice only", 20, 18, 630, 34, 15, true, Ink);
            Label(panel, "Keep the microphone listening and type matched phrases into the text field you click.\nShort or uncertain voices are discarded. Hovering alone does not place the typing cursor.\nThis mode starts only when you choose Start; it never listens automatically at launch.", 20, 68, 630, 90, 10, false, Muted);
            handsFreeStatus = Label(panel, File.Exists(MyVoiceProfile.FilePath) ? "Voice profile saved. Start when you are ready." : "Paused. Set up voice matching, then enroll your voice.", 20, 166, 630, 68, 10, false, Ink);
            Button(panel, "Set up local voice matching", 20, 240, 294, async (s, e) => {
                if (PauseHandsFree != null) PauseHandsFree();
                var button = (Control)s; button.Enabled = false;
                try {
                    // Reuse an existing enhanced-speech environment when its voice encoder is ready.
                    try { await Task.Run(() => MyVoiceMatcher.Ready(source)); handsFreeStatus.Text = "Local voice matching is ready. You can enroll now."; return; } catch { }
                    if (MessageBox.Show(this, "Download and install the optional local voice-matching packages? This may download a CPU runtime and model. Python 3.10 or later is required. Your normal shortcut dictation stays available.", "Set up voice matching", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "engine", "setup_handsfree.ps1");
                    if (!File.Exists(script)) throw new IOException("Install this Notewll build to include voice matching setup.");
                    handsFreeStatus.Text = "Setting up local voice matching. This can take several minutes...";
                    await Task.Run(() => {
                        using (var process = Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" -DataRoot \"" + Config.DirectoryPath + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })) {
                            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                            if (!process.WaitForExit(900000)) { process.Kill(); throw new IOException("Voice setup timed out. You can run it again."); }
                            Task.WaitAll(output, error); if (process.ExitCode != 0) throw new IOException("Voice setup did not finish. Check Python and internet availability, then retry. " + error.Result.Substring(0, Math.Min(200, error.Result.Length)));
                        }
                    });
                    await Task.Run(() => MyVoiceMatcher.Ready(source));
                    if (!IsDisposed) handsFreeStatus.Text = "Voice matching is ready. Enroll your voice next.";
                } catch (Exception ex) { if (!IsDisposed) handsFreeStatus.Text = ex.Message; }
                finally { if (!button.IsDisposed) button.Enabled = true; }
            });
            Button(panel, "Enroll / replace my voice", 334, 240, 296, (s, e) => {
                if (PauseHandsFree != null) PauseHandsFree();
                if (IsTesting || CanTest != null && !CanTest()) { handsFreeStatus.Text = "Finish the current recording or test first."; return; }
                if (SelectedMic() != source.Microphone) { handsFreeStatus.Text = "Save your selected microphone in preferences before enrolling."; return; }
                var draft = source.Copy();
                using (var dialog = new VoiceEnrollmentDialog(draft)) dialog.ShowDialog(this);
                handsFreeStatus.Text = File.Exists(MyVoiceProfile.FilePath) ? "Voice profile saved locally. Start when ready." : "No voice profile saved. Enroll before starting.";
            });
            handsFreeToggle = Button(panel, "Start hands-free", 20, 300, 294, (s, e) => { if (ToggleHandsFree != null) ToggleHandsFree(); else handsFreeStatus.Text = "Start this mode from the running Notewll app."; });
            Button(panel, "Delete my voice profile", 334, 300, 296, (s, e) => {
                if (PauseHandsFree != null) PauseHandsFree();
                try { if (File.Exists(MyVoiceProfile.FilePath)) File.Delete(MyVoiceProfile.FilePath); Task.Run(() => MyVoiceMatcher.Shutdown()); handsFreeStatus.Text = "Voice profile deleted. Hands-free is paused."; }
                catch (Exception ex) { handsFreeStatus.Text = "Could not delete the profile: " + ex.Message; }
            });
            Label(panel, "Allowed apps", 20, 350, 630, 28, 11, true, Ink);
            var allowedApps = new ThemedTextBox { Left = 20, Top = 394, Width = 440, AccessibleName = "Allowed app executable names", Text = source.HandsFreeApps };
            panel.Controls.Add(allowedApps);
            Button(panel, "Save allowed apps", 470, 390, 160, (s, e) => { if (PauseHandsFree != null) PauseHandsFree(); source.HandsFreeApps = allowedApps.Text.Trim(); source.Save(); handsFreeStatus.Text = "Allowed apps saved. Start hands-free when ready."; });
            Label(panel, "Allowed executable names, separated by commas (for example: notepad, chrome). An empty list blocks all apps. Screen lock and microphone changes pause listening.", 20, 435, 630, 60, 9, false, Muted);
            Label(panel, "Click the listening ball or press Escape to pause immediately. Shortcut dictation and note recording pause this mode.\n\nWhile listening, anything you say may be typed. Voice matching can make mistakes; overlapping speech and recordings of your voice are not reliably excluded. Native password fields are blocked; browser password fields cannot be reliably detected.\n\nThe mic hears surrounding audio to check the speaker. Rejected audio is discarded. Enrollment audio is not saved; your voice profile is encrypted for this Windows account.", 20, 505, 630, 190, 10, false, Muted);
        }
    }
    sealed class VoiceEnrollmentDialog : Form
    {
        public static int OpenCount;
        internal static readonly string[] Readings = {
            "At midnight, a tiny bookshop appeared at the end of my street.",
            "Every book inside described a journey its reader had not yet taken.",
            "I opened one, and a map of tomorrow unfolded across the page."
        };
        readonly Config config; readonly List<byte[]> samples = new List<byte[]>();
        readonly bool preview;
        readonly Label status = new Label { Left = 24, Top = 66, Width = 592, Height = 52 };
        readonly Label passage = new Label { Left = 24, Top = 126, Width = 592, Height = 110, Padding = new Padding(14), Font = new Font("Segoe UI", 13), BackColor = ThemeColors.Card, ForeColor = ThemeColors.Ink, AccessibleName = "Passage to read aloud" };
        readonly Button record = new HubButton { Left = 24, Top = 314, Width = 280, Height = 42, Text = "Record sample 1", Enabled = false };
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 100 };
        MicRecorder mic; DateTime started; bool saving, disposed;
        public VoiceEnrollmentDialog(Config config, bool preview = false)
        {
            this.config = config; this.preview = preview; OpenCount++; Icon = AppLogo.Icon; ShowInTaskbar = false; Text = "Enroll my voice"; ClientSize = new Size(640, 380); Font = new Font("Segoe UI", 10); StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
            var title = new Label { Left = 24, Top = 20, Width = 592, Height = 36, Text = "A bookshop from tomorrow", Font = new Font("Segoe UI", 15, FontStyle.Bold) };
            var help = new Label { Left = 24, Top = 250, Width = 592, Height = 56, Text = "Read naturally until the timer ends.\nUse a quiet room. Enrollment audio is not saved." };
            var close = new HubButton { Left = 336, Top = 314, Width = 280, Height = 42, Text = "Cancel" }; close.Click += (s, e) => Close(); CancelButton = close;
            Controls.AddRange(new Control[] { title, status, passage, help, record, close });
            ThemeColors.Apply(this, config.Theme); passage.BackColor = ThemeColors.Card;
            passage.Text = Readings[0];
            status.Text = "Preparing local voice matching... No microphone is open yet.";
            AutoScaleMode = AutoScaleMode.None;
            using (var graphics = CreateGraphics()) { float scale = graphics.DpiX / 96f; if (scale != 1) Scale(new SizeF(scale, scale)); }
            Shown += async (s, e) => { if (preview) { record.Enabled = true; Prompt(); return; } try { await Task.Run(() => MyVoiceMatcher.Ready(config)); if (!IsDisposed) { record.Enabled = true; Prompt(); } } catch (Exception ex) { if (!IsDisposed) status.Text = ex.Message; } };
            record.Click += (s, e) => {
                if (preview) return;
                try { mic = new MicRecorder(config.Microphone); mic.Start(); started = DateTime.UtcNow; record.Enabled = false; timer.Start(); }
                catch (Exception ex) { if (mic != null) mic.Dispose(); mic = null; status.Text = ex.Message; }
            };
            timer.Tick += async (s, e) => {
                if (mic == null || saving) return;
                double seconds = (DateTime.UtcNow - started).TotalSeconds;
                status.Text = "Recording sample " + (samples.Count + 1) + " of 3 - " + Math.Max(0, 6 - (int)seconds) + " seconds left.\nKeep reading the passage below naturally.";
                if (seconds < 6) return;
                timer.Stop(); samples.Add(mic.Stop()); mic.Dispose(); mic = null;
                if (samples.Count < 3) { Prompt(); record.Enabled = true; return; }
                saving = true; status.Text = "Checking that all three samples match...";
                try { var profile = await Task.Run(() => MyVoiceMatcher.Enroll(config, samples.ToArray())); if (!IsDisposed) { profile.Save(); DialogResult = DialogResult.OK; Close(); } }
                catch (Exception ex) { if (!IsDisposed) { foreach (var sample in samples) Array.Clear(sample, 0, sample.Length); samples.Clear(); saving = false; record.Text = "Record sample 1"; record.Enabled = true; passage.Text = Readings[0]; status.Text = ex.Message + "\nTry three new samples."; } }
            };
        }
        void Prompt() { record.Text = "Record sample " + (samples.Count + 1); passage.Text = Readings[samples.Count]; status.Text = "Sample " + (samples.Count + 1) + " of 3: read this part of the story aloud.\nClick Record when ready. Each sample lasts six seconds."; }
        internal void PreviewSample(int index, bool recording)
        {
            if (!preview) throw new InvalidOperationException("Preview is available only without microphone capture.");
            passage.Text = Readings[index]; record.Text = "Record sample " + (index + 1); record.Enabled = !recording;
            status.Text = recording ? "Recording sample " + (index + 1) + " of 3 - 4 seconds left.\nKeep reading the passage below naturally." : "Sample " + (index + 1) + " of 3: read this part of the story aloud.\nClick Record when ready. Each sample lasts six seconds.";
        }
        protected override void Dispose(bool disposing) { if (disposing && !disposed) { disposed = true; timer.Dispose(); if (mic != null) mic.Dispose(); foreach (var sample in samples) Array.Clear(sample, 0, sample.Length); samples.Clear(); OpenCount--; } base.Dispose(disposing); }
    }
}
