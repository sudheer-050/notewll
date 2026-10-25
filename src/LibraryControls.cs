using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace Notewll
{
    sealed class LibraryFilter : Button
    {
        public bool Selected;
        public int Count;
        public LibraryFilter() { FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand; Font = new Font("Segoe UI", 10); SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent.BackColor);
            var rect = Rectangle.Inflate(ClientRectangle, -4, -4);
            TextRenderer.DrawText(e.Graphics, Text + "  " + Count, Font, rect, Selected ? ThemeColors.Accent : ThemeColors.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            if (Selected) { using (var pen = new Pen(ThemeColors.Accent, Math.Max(2, Height / 16))) e.Graphics.DrawLine(pen, 8, Height - 2, Width - 8, Height - 2); }
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -5));
        }
    }
    sealed class LibraryBadge : Label
    {
        public LibraryBadge() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); Font = new Font("Segoe UI", 8.5f); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Surface.CanvasColor(Parent)); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Surface.Shape(new RectangleF(0, 0, Width - 1, Height - 1), Math.Min(Height / 2, 12))) using (var brush = new SolidBrush(ThemeColors.Selected)) e.Graphics.FillPath(brush, path);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ThemeColors.Accent, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
        }
    }
}
