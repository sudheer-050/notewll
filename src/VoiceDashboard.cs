using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Notewll
{
    sealed class VoiceKpis : Control
    {
        internal const int SpeakingWpm = 150;
        public int Words, Sentences, ActiveDays, Sessions, TypingWpm = 40;
        public double MeasuredWpm { get; set; }
        public int[] Days = new int[7];
        public double[] DailySavedMinutes = new double[7];
        public VoiceKpis() { DoubleBuffered = true; AccessibleName = "Voice analytics"; }
        internal static double SavedMinutes(int words, int typing) { return Math.Max(0, words * (1.0 / typing - 1.0 / SpeakingWpm)); }
        internal static string WelcomeHeadline(int words, int typing)
        {
            double minutes = SavedMinutes(words, typing);
            if (words == 0) return "Make room for more.";
            if (minutes <= 0) return "Welcome back.";
            string duration;
            if (minutes < 1) { int seconds = Math.Max(1, (int)Math.Round(minutes * 60)); duration = seconds + (seconds == 1 ? " second" : " seconds"); }
            else if (minutes < 60) duration = minutes.ToString("0.#") + (minutes.ToString("0.#") == "1" ? " minute" : " minutes");
            else { int total = (int)Math.Round(minutes), hours = total / 60, rest = total % 60; duration = hours + (hours == 1 ? " hour" : " hours") + (rest == 0 ? "" : " and " + rest + (rest == 1 ? " minute" : " minutes")); }
            return "You've saved about " + duration + ".";
        }
        internal static string Duration(double minutes)
        {
            if (minutes <= 0) return "0 sec";
            if (minutes < 1) return Math.Max(1, (int)Math.Round(minutes * 60)) + " sec";
            if (minutes < 60) return minutes.ToString("0.#") + " min";
            return ((int)minutes / 60) + "h " + ((int)minutes % 60) + "m";
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); var g = e.Graphics; float scale = Height / 240f; g.ScaleTransform(scale, scale); g.SmoothingMode = SmoothingMode.AntiAlias;
            float gap = 10, tileWidth = (Width / scale - gap * 2) / 3;
            Kpi(g, new RectangleF(0, 0, tileWidth, 114), "TIME SAVED", Duration(SavedMinutes(Words, TypingWpm)), "Estimated time back", true);
            Kpi(g, new RectangleF(tileWidth + gap, 0, tileWidth, 114), "WORDS", Words.ToString("N0"), MeasuredWpm > 0 ? MeasuredWpm.ToString("0") + " spoken WPM · " + Sessions + " sessions" : Sessions + (Sessions == 1 ? " dictation" : " dictations") + " saved");
            Kpi(g, new RectangleF((tileWidth + gap) * 2, 0, tileWidth, 114), "SENTENCES", Sentences.ToString("N0"), "From saved dictations");
            Kpi(g, new RectangleF(0, 124, tileWidth, 114), "ACTIVE DAYS", ActiveDays.ToString("N0"), Days.Count(day => day > 0) + " of the last 7 days");
            Trend(g, new RectangleF(tileWidth + gap, 124, tileWidth * 2 + gap, 114));
        }
        static void Kpi(Graphics g, RectangleF rect, string label, string value, string detail, bool accent = false)
        {
            using (var shape = Surface.Shape(rect, 14))
            {
                if (accent) using (var fill = new LinearGradientBrush(rect, ThemeColors.Accent, Color.FromArgb(ThemeColors.Accent.R * 9 / 10, ThemeColors.Accent.G * 9 / 10, ThemeColors.Accent.B * 9 / 10), 35)) g.FillPath(fill, shape);
                else using (var fill = new SolidBrush(ThemeColors.Button)) g.FillPath(fill, shape);
            }
            Color labelColor = accent ? ThemeColors.AccentInk : ThemeColors.Muted, valueColor = accent ? ThemeColors.AccentInk : ThemeColors.Ink;
            Draw(g, label, rect.X + 16, rect.Y + 14, rect.Width - 28, 20, 9, labelColor, true);
            Draw(g, value, rect.X + 14, rect.Y + 39, rect.Width - 26, 43, 28, valueColor, true);
            Draw(g, detail, rect.X + 16, rect.Y + 90, rect.Width - 28, 18, 8, labelColor, false);
        }
        void Trend(Graphics g, RectangleF rect)
        {
            using (var shape = Surface.Shape(rect, 14)) using (var fill = new SolidBrush(ThemeColors.Button)) g.FillPath(fill, shape);
            Draw(g, "TIME SAVED THIS WEEK", rect.X + 16, rect.Y + 12, rect.Width / 2, 20, 9, ThemeColors.Muted, true);
            Draw(g, Duration(DailySavedMinutes.Sum()), rect.Right - 100, rect.Y + 10, 84, 23, 12, ThemeColors.Accent, true);
            double max = Math.Max(1.0 / 60, DailySavedMinutes.Max());
            float left = rect.X + 18, right = rect.Right - 18, top = rect.Y + 42, bottom = rect.Y + 85;
            using (var pen = new Pen(ThemeColors.Accent, 2.5f))
                for (int i = 1; i < 7; i++)
                    g.DrawLine(pen, left + (right - left) * (i - 1) / 6, bottom - (float)(DailySavedMinutes[i - 1] / max) * (bottom - top), left + (right - left) * i / 6, bottom - (float)(DailySavedMinutes[i] / max) * (bottom - top));
            Draw(g, "7 DAYS AGO", left, rect.Y + 91, 100, 16, 8, ThemeColors.Muted, false);
            Draw(g, "TODAY", right - 48, rect.Y + 91, 48, 16, 8, ThemeColors.Muted, false);
        }
        internal static void Draw(Graphics g, string text, float x, float y, float w, float h, float size, Color color, bool bold)
        { using (var font = new Font("Segoe UI", size * 96f / 72, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel)) using (var brush = new SolidBrush(color)) using (var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }) g.DrawString(text, font, brush, new RectangleF(x, y, Math.Max(1, w), h), format); }
    }
}
