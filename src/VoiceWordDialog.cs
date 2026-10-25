using System;
using System.Drawing;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace Notewll
{
    sealed class VoiceWordDialog : Form
    {
        internal static int OpenCount;
        readonly Config config;
        readonly HubButton record = new HubButton { Text = "Record pronunciation", Primary = true, AccessibleName = "Record pronunciation" };
        readonly HubButton play = new HubButton { Text = "Listen", Enabled = false };
        readonly HubButton use = new HubButton { Text = "Use word", Primary = true, Enabled = false };
        readonly TextBox spelling = new TextBox { AccessibleName = "Correct spelling", MaxLength = 80, Font = new Font("Segoe UI", 12) };
        readonly Label feedback = new Label(), heardLabel = new Label(), elapsed = new Label();
        readonly ProgressBar level = new ProgressBar { Minimum = 0, Maximum = 12000 };
        readonly Timer timer = new Timer { Interval = 100 };
        MicRecorder recorder;
        SoundPlayer player;
        MemoryStream playbackStream;
        byte[] sample;
        string heard = "", pronunciationId;
        DateTime started;
        bool recognizing, counted, released;
        internal PersonalRule Result { get; private set; }
        static Label Caption(Control parent, string text, int x, int y, int width, int height, float size, bool bold = false)
        { var label = new Label { Text = text, Left = x, Top = y, Width = width, Height = height, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), UseMnemonic = false }; parent.Controls.Add(label); return label; }
        internal VoiceWordDialog(Config config, PersonalRule initial)
        {
            this.config = config.Copy();
            Text = "Record a dictionary word"; Icon = AppLogo.Icon; Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.None; ClientSize = new Size(640, 548); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent;
            Caption(this, "Your voice. The right spelling.", 24, 18, 592, 40, 20, true);
            Caption(this, "Say the word in your normal voice, then enter how it should be written.", 24, 65, 592, 38, 9);
            var card = new Surface { Left = 24, Top = 112, Width = 592, Height = 228 }; Controls.Add(card);
            Caption(card, "Your pronunciation", 20, 16, 552, 26, 11, true);
            record.SetBounds(20, 54, 200, 38); play.SetBounds(232, 54, 130, 38); elapsed.SetBounds(382, 58, 170, 30); level.SetBounds(20, 108, 552, 12); feedback.SetBounds(20, 132, 552, 36); heardLabel.SetBounds(20, 177, 552, 34);
            card.Controls.AddRange(new Control[] { record, play, elapsed, level, feedback, heardLabel });
            Caption(this, "Correct spelling", 24, 354, 592, 26, 11, true); spelling.SetBounds(24, 386, 592, 32); Controls.Add(spelling);
            Caption(this, "Your recording stays on this device. Save preferences to activate the word.", 24, 430, 592, 36, 9);
            var cancel = new HubButton { Text = "Cancel", Left = 294, Top = 480, Width = 126, Height = 42 }; use.SetBounds(432, 480, 184, 42); Controls.AddRange(new Control[] { cancel, use }); CancelButton = cancel;
            feedback.Text = "Press Record and say one word or name. Up to five seconds."; heardLabel.Text = "No pronunciation recorded yet.";
            if (initial != null)
            {
                spelling.Text = initial.Replacement ?? ""; heard = initial.Trigger ?? ""; pronunciationId = initial.PronunciationId;
                try { if (initial.PronunciationPcm != null) sample = (byte[])initial.PronunciationPcm.Clone(); else if (!String.IsNullOrEmpty(pronunciationId)) sample = Pronunciations.Load(pronunciationId); }
                catch (Exception) { feedback.Text = "The previous recording is unavailable. Please record the word again."; }
                if (sample != null) { heardLabel.Text = "Heard: " + heard; feedback.Text = "Pronunciation loaded. Listen, record again, or update the spelling."; }
            }
            record.Click += async (s, e) => { if (recorder == null) StartRecording(); else await StopRecording(); };
            timer.Tick += async (s, e) => { if (recorder == null) return; level.Value = Math.Min(level.Maximum, recorder.Peak); elapsed.Text = Math.Min(5, (DateTime.UtcNow - started).TotalSeconds).ToString("0.0") + " / 5 seconds"; if (DateTime.UtcNow - started >= TimeSpan.FromSeconds(5)) await StopRecording(); };
            play.Click += (s, e) => { try { StopPlayback(); if (sample == null) return; playbackStream = new MemoryStream(MicRecorder.ToWav(sample)); player = new SoundPlayer(playbackStream); player.Load(); player.Play(); } catch (Exception ex) { feedback.Text = "Could not play: " + ex.Message; } };
            spelling.TextChanged += (s, e) => UpdateState(); cancel.Click += (s, e) => Close();
            use.Click += (s, e) => { try { Result = BuildRule(); DialogResult = DialogResult.OK; Close(); } catch (Exception ex) { feedback.Text = ex.Message; } };
            ThemeColors.Apply(this, config.Theme); float scale; using (var graphics = CreateGraphics()) scale = graphics.DpiX / 96f; if (scale != 1) Scale(new SizeF(scale, scale)); UpdateState(); OpenCount++; counted = true;
        }
        internal static PersonalRule Prompt(IWin32Window owner, Config config, PersonalRule initial)
        { using (var dialog = new VoiceWordDialog(config, initial)) return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.Result : null; }
        void StartRecording()
        {
            try { StopPlayback(); recorder = new MicRecorder(config.Microphone); recorder.Start(); started = DateTime.UtcNow; feedback.Text = "Listening. Say the word, then press Stop."; record.Text = "Stop recording"; timer.Start(); UpdateState(); }
            catch (Exception ex) { if (recorder != null) { recorder.Dispose(); recorder = null; } feedback.Text = "Microphone: " + ex.Message; UpdateState(); }
        }
        async Task StopRecording()
        {
            if (recorder == null || recognizing) return;
            timer.Stop(); var capture = recorder; recorder = null; byte[] pcm = null;
            try
            {
                pcm = capture.Stop(); capture.Dispose();
                if (pcm.Length > Pronunciations.MaximumBytes) Array.Resize(ref pcm, Pronunciations.MaximumBytes);
                Pronunciations.Validate(pcm); recognizing = true; feedback.Text = "Recognizing your word locally..."; record.Text = "Recognizing..."; UpdateState();
                string recognized = await Task.Run(() => DictationSpeech.Transcribe(config, pcm));
                if (IsDisposed || Disposing) return;
                AcceptRecording(pcm, recognized); feedback.Text = "Review what was heard, then enter the correct spelling.";
            }
            catch (Exception ex) { if (!IsDisposed && !Disposing) feedback.Text = "Could not capture the word: " + ex.Message + " Try recording again."; }
            finally { capture.Dispose(); if (pcm != null) Array.Clear(pcm, 0, pcm.Length); recognizing = false; if (!IsDisposed && !Disposing) { record.Text = sample == null ? "Record pronunciation" : "Record again"; level.Value = 0; UpdateState(); } }
        }
        internal void AcceptRecording(byte[] pcm, string recognized)
        {
            Pronunciations.Validate(pcm);
            string value = (recognized ?? "").Trim().TrimEnd('.', ',', '!', '?', ':', ';').Trim();
            if (String.IsNullOrWhiteSpace(value)) throw new InvalidDataException("No word was recognized. Try speaking a little more clearly.");
            if (value.Length > 120) throw new InvalidDataException("Record only the word or name, not a full sentence.");
            StopPlayback(); if (sample != null) Array.Clear(sample, 0, sample.Length); sample = (byte[])pcm.Clone(); heard = value; pronunciationId = Guid.NewGuid().ToString("N"); heardLabel.Text = "Heard: " + heard; elapsed.Text = (sample.Length / 32000.0).ToString("0.0") + " seconds recorded"; record.Text = "Record again"; UpdateState();
        }
        internal PersonalRule BuildRule()
        {
            if (sample == null || recognizing || recorder != null) throw new InvalidDataException("Record the word before saving it.");
            var rule = new PersonalRule { Kind = "Word", Trigger = heard, Replacement = spelling.Text.Trim(), PronunciationId = pronunciationId, PronunciationPcm = (byte[])sample.Clone() };
            PersonalRule.Validate(new[] { rule }); return rule;
        }
        void UpdateState() { bool busy = recognizing || recorder != null; record.Enabled = !recognizing; play.Enabled = sample != null && !busy; use.Enabled = sample != null && !busy && !String.IsNullOrWhiteSpace(spelling.Text); }
        void StopPlayback() { if (player != null) { player.Stop(); player.Dispose(); player = null; } if (playbackStream != null) { playbackStream.Dispose(); playbackStream = null; } }
        void ReleaseCaptureResources()
        {
            if (released) return; released = true; timer.Stop();
            if (recorder != null) { var pcm = recorder.Stop(); Array.Clear(pcm, 0, pcm.Length); recorder.Dispose(); recorder = null; }
            StopPlayback(); if (sample != null) { Array.Clear(sample, 0, sample.Length); sample = null; }
            if (counted) { OpenCount--; counted = false; }
        }
        protected override void OnFormClosed(FormClosedEventArgs e) { ReleaseCaptureResources(); base.OnFormClosed(e); }
        protected override void Dispose(bool disposing) { if (disposing) { ReleaseCaptureResources(); timer.Dispose(); } base.Dispose(disposing); }
    }
}
