using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Notewll
{
    // Small, selectable app previews; drawing another palette never changes the active theme.
    sealed class ThemeCard : Button
    {
        internal readonly string ThemeName;
        bool selected;
        internal bool Selected { get { return selected; } set { selected = value; AccessibleDescription = value ? "Selected theme. Save preferences to keep it." : "Select to preview this theme. Save preferences to keep it."; Invalidate(); } }
        internal ThemeCard(string name)
        {
            ThemeName = name; Text = name; AccessibleName = "Preview " + name + " theme";
            AccessibleDescription = "Select to preview this theme. Save preferences to keep it.";
            Dock = DockStyle.Fill; Margin = new Padding(6); Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(ThemeColors.Paper); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var colors = ThemeColors.PreviewColors(ThemeName);
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            float unit = g.DpiX / 96f, pad = 8 * unit;
            using (var shape = Surface.Shape(new RectangleF(2, 2, Math.Max(1, Width - 4), Math.Max(1, Height - 4)), 10 * unit))
            using (var fill = new SolidBrush(colors[0]))
            using (var edge = new Pen(Selected ? ThemeColors.Accent : colors[7], Selected ? 2 * unit : 1))
            { g.FillPath(fill, shape); g.DrawPath(edge, shape); }
            var saved = g.Save();
            g.TranslateTransform(pad, pad);
            g.ScaleTransform(Math.Max(1, Width - 2 * pad) / 120f, Math.Max(1, Height - Font.Height - 3 * pad) / 53f);
            g.TranslateTransform(-10, -10);
            using (var background = new SolidBrush(colors[1])) g.FillRectangle(background, 10, 10, 120, 53);
            using (var sidebar = new SolidBrush(colors[6])) g.FillRectangle(sidebar, 10, 10, 29, 53);
            using (var selected = new SolidBrush(colors[5])) g.FillRectangle(selected, 14, 25, 21, 7);
            using (var ink = new Pen(colors[2], 2)) { g.DrawLine(ink, 47, 19, 111, 19); g.DrawLine(ink, 47, 27, 98, 27); }
            using (var muted = new Pen(colors[3], 1.5f)) { g.DrawLine(muted, 15, 18, 29, 18); g.DrawLine(muted, 15, 39, 31, 39); g.DrawLine(muted, 15, 49, 27, 49); }
            using (var action = new SolidBrush(colors[8])) g.FillRectangle(action, 47, 42, 31, 12);
            using (var secondary = new SolidBrush(colors[4])) g.FillRectangle(secondary, 84, 42, 35, 12);
            g.Restore(saved);
            using (var font = new Font(Font, FontStyle.Bold))
            using (var ink = new SolidBrush(colors[2]))
            using (var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap }) g.DrawString(ThemeName, font, ink, new RectangleF(pad, Height - Font.Height - pad, Math.Max(1, Width - 2 * pad - 18 * unit), Font.Height + 2), format);
            if (Selected)
            {
                float x = Width - pad - 14 * unit, y = Height - pad - Font.Height / 2f;
                using (var pen = new Pen(ThemeColors.Accent, 2 * unit)) { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawLines(pen, new[] { new PointF(x, y), new PointF(x + 4 * unit, y + 4 * unit), new PointF(x + 12 * unit, y - 5 * unit) }); }
            }
            if (Focused) using (var pen = new Pen(colors[2], 1) { DashStyle = DashStyle.Dot }) g.DrawRectangle(pen, 5, 5, Math.Max(1, Width - 10), Math.Max(1, Height - 10));
        }
    }
}
