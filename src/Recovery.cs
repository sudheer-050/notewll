using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Notewll
{
    sealed class DictationRecovery
    {
        public byte[] Audio;
        public string Text = "", Message = "";
        public int Revision { get; private set; }
        public bool HasWork { get { return Audio != null && Audio.Length > 0 || !String.IsNullOrEmpty(Text); } }
        public void Discard() { Revision++; if (Audio != null) Array.Clear(Audio, 0, Audio.Length); Audio = null; Text = Message = ""; }
    }
    static class ClipboardPaste
    {
        [DllImport("user32.dll")] static extern uint GetClipboardSequenceNumber();
        public static void RestoreAfterPaste(IDataObject original, uint sequence)
        {
            if (original == null) return;
            var timer = new Timer { Interval = 1500 };
            timer.Tick += (s, e) => { timer.Stop(); try { if (GetClipboardSequenceNumber() == sequence) Clipboard.SetDataObject(original, true, 3, 100); } catch { } finally { timer.Dispose(); } }; timer.Start();
        }
        public static IDataObject Snapshot()
        {
            try
            {
                var source = Clipboard.GetDataObject(); if (source == null) return null;
                var copy = new DataObject();
                foreach (string format in source.GetFormats(false))
                {
                    object value = source.GetData(format, false);
                    if (value is string || value is string[] || value is byte[] || value is System.Drawing.Bitmap) copy.SetData(format, value is Bitmap ? ((Bitmap)value).Clone() : value);
                }
                return copy.GetFormats().Length > 0 ? copy : null;
            }
            catch { return null; }
        }
        public static uint Sequence { get { return GetClipboardSequenceNumber(); } }
    }
    sealed class RecoveryForm : Form
    {
        readonly TextBox text = new ThemedTextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
        readonly Label status = new Label { Dock = DockStyle.Top, Height = 85, Padding = new Padding(12) };
        readonly Button retry = new HubButton { Text = "Retry speech", AutoSize = true };
        public RecoveryForm(DictationRecovery recovery, Action<Action> retryAction)
        {
            AutoScaleMode = AutoScaleMode.None; MinimumSize = new Size(560, 400); Text = "Recover last dictation"; Icon = AppLogo.Icon; Size = new Size(660, 450); StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 10);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Height = 48, Padding = new Padding(6) };
            var copy = new HubButton { Text = "Copy text", AutoSize = true }; var discard = new HubButton { Text = "Discard", AutoSize = true };
            Action refresh = () => { text.Text = recovery.Text; status.Text = recovery.Message + "\nReview before pasting: some words may already be in your original field. Audio is held only in memory until discarded or the app exits."; retry.Enabled = recovery.Audio != null; copy.Enabled = text.TextLength > 0; };
            retry.Click += (s, e) => { retry.Enabled = false; status.Text = "Retrying locally..."; retryAction(() => { if (!IsDisposed) BeginInvoke((Action)refresh); }); };
            copy.Click += (s, e) => { try { if (text.TextLength > 0) Clipboard.SetText(text.Text); } catch (Exception ex) { status.Text = ex.Message; } };
            discard.Click += (s, e) => { recovery.Discard(); Close(); };
            var close = DialogUi.Button("Close", (s, e) => Close()); CancelButton = close;
            footer.Controls.AddRange(new Control[] { retry, copy, discard, close }); Controls.Add(text); Controls.Add(status); Controls.Add(footer); refresh(); ThemeColors.Apply(this, ThemeColors.Mode);
            using (var graphics = CreateGraphics()) { float scale = graphics.DpiX / 96f; if (scale != 1) Scale(new SizeF(scale, scale)); }
        }
    }
}
