using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Notewll
{
    // Native-resolution, per-pixel transparency: no bitmap stretching or jagged window region.
    sealed class Overlay : Form
    {
        readonly Timer animation = new Timer { Interval = 25 };
        readonly Font caption = new Font("Segoe UI", 10);
        string message = "";
        int mode;
        float phase, level, scale = 1;
        Color accent;
        public Func<int> InputLevel;
        public bool ReducedMotion;
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x00080000 | 0x20 | 0x80; return p; }
        }
        public Overlay()
        {
            FormBorderStyle = FormBorderStyle.None; TopMost = true; ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.None;
            animation.Tick += (s, e) => Advance();
        }
        public void Display(string text, Color color)
        {
            message = text; accent = color;
            mode = text.IndexOf("Recording from", StringComparison.OrdinalIgnoreCase) >= 0 ? 1
                : (text.IndexOf("Transcribing", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Starting Holly", StringComparison.OrdinalIgnoreCase) >= 0) ? 2 : 0;
            AccessibleName = mode == 1 ? "Recording speech" : mode == 2 ? "Transcribing speech" : text;
            scale = MonitorScale(Cursor.Position);
            Size logical = LogicalSize();
            Size = new Size((int)Math.Ceiling(logical.Width * scale), (int)Math.Ceiling(logical.Height * scale));
            var area = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(area.Left + (area.Width - Width) / 2, area.Bottom - Height - (int)(18 * scale));
            level = 0;
            if (!Visible) Show();
            Present();
            if (mode == 1 && !ReducedMotion) animation.Start(); else animation.Stop();
        }
        void Advance()
        {
            float sample = InputLevel == null ? 0 : Math.Max(0, InputLevel());
            // RMS gate suppresses quiet room noise; there is no idle sine/pulse.
            float wanted = sample <= 70 ? 0 : Math.Min(1f, (float)Math.Sqrt((sample - 70) / 1600f));
            float previous = level;
            level += (wanted - level) * (wanted > level ? 0.35f : 0.3f);
            if (wanted == 0 && level < 0.008f) level = 0;
            if (level > 0) phase += 0.075f;
            if (level > 0 || previous != level) Present();
        }
        Size LogicalSize()
        {
            if (mode != 0) return new Size(64, 64);
            var size = TextRenderer.MeasureText(message, caption, new Size(360, 0), TextFormatFlags.WordBreak);
            return new Size(Math.Max(100, size.Width + 34), Math.Max(32, size.Height + 18));
        }
        public Bitmap Snapshot(float dpiScale)
        {
            var logical = LogicalSize();
            var frame = new Bitmap((int)Math.Ceiling(logical.Width * dpiScale), (int)Math.Ceiling(logical.Height * dpiScale), PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(frame))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.ScaleTransform(dpiScale, dpiScale);
                if (mode != 0)
                {
                    // A stable, high-contrast surface keeps the signal legible on any app.
                    using (var shadow = new SolidBrush(Color.FromArgb(24, 0, 0, 0)))
                        g.FillEllipse(shadow, 5, 7, 54, 54);
                    using (var surface = new LinearGradientBrush(new PointF(0, 5), new PointF(0, 59),
                        Color.FromArgb(255, 39, 43, 49), Color.FromArgb(255, 17, 20, 25)))
                        g.FillEllipse(surface, 5, 5, 54, 54);
                    using (var rim = new Pen(Color.FromArgb(100, 105, 118, 134), 0.8f))
                        g.DrawEllipse(rim, 5.5f, 5.5f, 53, 53);
                    using (var highlight = new Pen(Color.FromArgb(36, 255, 255, 255), 0.8f))
                        g.DrawArc(highlight, 6.5f, 6.5f, 51, 51, 210, 120);
                    DrawSignal(g, new PointF(32, 32), mode == 1, level, phase);
                }
                else
                {
                    using (var path = Capsule(logical.Width, logical.Height))
                    using (var fill = new SolidBrush(Color.FromArgb(245, 23, 27, 34))) g.FillPath(fill, path);
                    using (var dot = new SolidBrush(accent)) g.FillEllipse(dot, 12, logical.Height / 2f - 2, 4, 4);
                    using (var brush = new SolidBrush(Color.FromArgb(237, 241, 247)))
                    using (var format = new StringFormat { LineAlignment = StringAlignment.Center })
                        g.DrawString(message, caption, brush, new RectangleF(24, 6, logical.Width - 32, logical.Height - 12), format);
                }
            }
            return frame;
        }
        internal static void DrawSignal(Graphics g, PointF center, bool recording, float level, float phase)
        {
            var state = g.Save();
            g.TranslateTransform(center.X - 32, center.Y - 32);
            if (recording)
            {
                for (int i = 0; i < 5; i++)
                {
                    float profile = 1f - Math.Abs(i - 2) * 0.2f;
                    float motion = 0.78f + 0.22f * (float)Math.Sin(phase * 2.5f + i * 1.15f);
                    float height = 3 + level * 23 * profile * motion;
                    float x = 32 + (i - 2) * 5.5f;
                    Color tint = level == 0 ? Color.FromArgb(161, 172, 186) : Color.FromArgb(174, 244, 224);
                    using (var bar = new Pen(tint, 3.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawLine(bar, x, 32 - height / 2, x, 32 + height / 2);
                }
            }
            else
            {
                // A distinct stationary processing state; no pretend microphone activity.
                for (int i = 0; i < 3; i++)
                using (var dot = new SolidBrush(Color.FromArgb(151 + i * 28, 169 + i * 20, 205 + i * 15)))
                    g.FillEllipse(dot, 21 + i * 9, 29.5f, 5, 5);
            }

            g.Restore(state);
        }
        static GraphicsPath Capsule(int width, int height)
        {
            var p = new GraphicsPath(); float d = Math.Min(32, height - 1);
            p.AddArc(0, 0, d, d, 180, 90); p.AddArc(width - d - 1, 0, d, d, 270, 90);
            p.AddArc(width - d - 1, height - d - 1, d, d, 0, 90); p.AddArc(0, height - d - 1, d, d, 90, 90); p.CloseFigure(); return p;
        }
        void Present()
        {
            using (var image = Snapshot(scale))
            {
                PresentFrame(this, image);
            }
        }
        internal static void PresentFrame(Form window, Bitmap image, Point? position = null)
        {
                IntPtr screen = GetDC(IntPtr.Zero), memory = IntPtr.Zero, bitmap = IntPtr.Zero, old = IntPtr.Zero;
                try
                {
                    memory = CreateCompatibleDC(screen); bitmap = image.GetHbitmap(Color.FromArgb(0)); old = SelectObject(memory, bitmap);
                    var target = position ?? window.Location; var source = new Point(); var size = image.Size;
                    var blend = new Blend { Alpha = 255, Format = 1 };
                    if (!UpdateLayeredWindow(window.Handle, screen, ref target, ref size, memory, ref source, 0, ref blend, 2))
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }
                finally
                {
                    if (old != IntPtr.Zero) SelectObject(memory, old);
                    if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
                    if (memory != IntPtr.Zero) DeleteDC(memory);
                    if (screen != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screen);
                }
        }
        static float MonitorScale(Point point)
        {
            try { uint x, y; if (GetDpiForMonitor(MonitorFromPoint(point, 2), 0, out x, out y) == 0) return x / 96f; }
            catch (EntryPointNotFoundException) { } catch (DllNotFoundException) { }
            return 1;
        }
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if (!Visible) animation.Stop(); }
        protected override void Dispose(bool disposing) { if (disposing) { animation.Dispose(); caption.Dispose(); } base.Dispose(disposing); }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] struct Blend { public byte Operation, Flags, Alpha, Format; }
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("user32.dll", SetLastError = true)] static extern bool UpdateLayeredWindow(IntPtr window, IntPtr targetDC, ref Point target, ref Size size, IntPtr sourceDC, ref Point source, int key, ref Blend blend, int flags);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(Point point, uint flags);
        [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
    }
}
