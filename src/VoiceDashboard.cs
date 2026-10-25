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
        public int[] Days = new int[7];
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
            float width = Width / scale, gap = 10, first = (width - gap * 3) * .31f, other = (width - first - gap * 3) / 3;
            Color purple = ThemeColors.Accent, muted = ThemeColors.Muted, ink = ThemeColors.Ink;
            var hero = new RectangleF(0, 0, first, 132);
            using (var path = Surface.Shape(hero, 14)) using (var fill = new LinearGradientBrush(hero, purple, ThemeColors.Dark ? Color.FromArgb(65, 43, 94) : Color.FromArgb(143, 107, 196), 35)) g.FillPath(fill, path);
            Draw(g, "TIME SAVED", 18, 16, first - 30, 22, 9, Color.White, true);
            Draw(g, Duration(SavedMinutes(Words, TypingWpm)), 16, 41, first - 28, 51, 30, Color.White, true);
            Draw(g, "Estimated time back", 18, 105, first - 30, 20, 8, Color.White, false);
            float x = first + gap;
            Tile(g, new RectangleF(x, 0, other, 132));
            Draw(g, "WORDS", x + 16, 16, other - 28, 22, 9, muted, true);
            Draw(g, Words.ToString("N0"), x + 14, 43, other - 26, 48, 29, ink, true);
            Draw(g, Sessions + (Sessions == 1 ? " dictation" : " dictations") + " saved", x + 16, 105, other - 28, 20, 8, muted, false);
            x += other + gap; Tile(g, new RectangleF(x, 0, other, 132));
            Draw(g, "SENTENCES", x + 16, 16, other - 28, 22, 9, muted, true);
            Draw(g, Sentences.ToString("N0"), x + 14, 43, other - 26, 48, 29, ink, true);
            Draw(g, "From saved dictations", x + 16, 105, other - 28, 20, 8, muted, false);
            x += other + gap; Tile(g, new RectangleF(x, 0, other, 132));
            Draw(g, "ACTIVE DAYS", x + 16, 16, other - 28, 22, 9, muted, true);
            Draw(g, ActiveDays.ToString("N0"), x + 14, 43, other / 2, 48, 29, ink, true);
            int max = Math.Max(1, Days.Max()); float bx = x + other * .48f, bw = (other * .43f) / 7;
            for (int i = 0; i < 7; i++) { float h = 4 + Days[i] * 32f / max; using (var brush = new SolidBrush(Days[i] > 0 ? purple : ThemeColors.Border)) g.FillRectangle(brush, bx + i * bw, 92 - h, Math.Max(2, bw - 4), h); }
            Draw(g, "7-day activity", x + 16, 105, other - 28, 20, 8, muted, false);
            Draw(g, "THE SAME WORDS. LESS OF YOUR DAY.", 0, 149, width, 20, 9, muted, true);
            float labelWidth = 150, valueWidth = 95, barWidth = Math.Max(80, width - labelWidth - valueWidth - 8);
            double typed = (double)Words / TypingWpm, spoken = (double)Words / SpeakingWpm, maxTime = Math.Max(typed, spoken);
            Draw(g, "Typing · " + TypingWpm + " WPM", 0, 177, labelWidth, 24, 9, ink, false);
            Draw(g, "Speaking · 150 WPM", 0, 209, labelWidth, 24, 9, ink, false);
            Bar(g, labelWidth, 184, barWidth, maxTime > 0 ? typed / maxTime : 0, ThemeColors.Muted);
            Bar(g, labelWidth, 216, barWidth, maxTime > 0 ? spoken / maxTime : 0, purple);
            Draw(g, Duration(typed), width - valueWidth, 177, valueWidth, 24, 11, ink, true);
            Draw(g, Duration(spoken), width - valueWidth, 209, valueWidth, 24, 11, purple, true);
        }
        static void Tile(Graphics g, RectangleF rect) { using (var shape = Surface.Shape(rect, 14)) using (var brush = new SolidBrush(ThemeColors.Button)) g.FillPath(brush, shape); }
        static void Bar(Graphics g, float x, float y, float width, double fraction, Color color)
        {
            using (var brush = new SolidBrush(ThemeColors.Button)) g.FillRectangle(brush, x, y, width, 9);
            if (fraction > 0) using (var brush = new SolidBrush(color)) g.FillRectangle(brush, x, y, Math.Max(2, (float)(width * fraction)), 9);
        }
        internal static void Draw(Graphics g, string text, float x, float y, float w, float h, float size, Color color, bool bold)
        { using (var font = new Font("Segoe UI", size * 96f / 72, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel)) using (var brush = new SolidBrush(color)) using (var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }) g.DrawString(text, font, brush, new RectangleF(x, y, Math.Max(1, w), h), format); }
    }
}
