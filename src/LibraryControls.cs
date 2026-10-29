using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace Notewll
{
    sealed class LibraryTable : ListView
    {
        readonly ImageList rowSpacing = new ImageList();
        internal int RowHeight { get { using (var g = CreateGraphics()) return Math.Max(Font.Height + (int)(14 * g.DpiX / 96), (int)(36 * g.DpiX / 96)); } }
        public LibraryTable()
        {
            DoubleBuffered = true; OwnerDraw = true; HeaderStyle = ColumnHeaderStyle.Nonclickable;
            SmallImageList = rowSpacing;
            ColumnWidthChanging += (s, e) => { e.Cancel = true; e.NewWidth = Columns[e.ColumnIndex].Width; };
        }
        void Fit()
        {
            if (Columns.Count != 2 || ClientSize.Width < 2) return;
            Columns[0].Width = (ClientSize.Width - 1) / 2; Columns[1].Width = ClientSize.Width - Columns[0].Width;
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); rowSpacing.ImageSize = new Size(1, RowHeight); Fit(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); if (rowSpacing != null) rowSpacing.ImageSize = new Size(1, RowHeight); Invalidate(); }
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); Fit(); Invalidate(); }
        void Cell(Graphics g, Rectangle bounds, string text, bool header, bool selected, HorizontalAlignment alignment)
        {
            float scale = g.DpiX / 96f; int pad = (int)(16 * scale);
            using (var fill = new SolidBrush(selected ? ThemeColors.Selected : header ? ThemeColors.Button : ThemeColors.Card)) g.FillRectangle(fill, bounds);
            var rect = new Rectangle(bounds.Left + pad, bounds.Top, Math.Max(1, bounds.Width - pad * 2), bounds.Height);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | (alignment == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left);
            Color ink = SystemInformation.HighContrast && selected ? SystemColors.HighlightText : ThemeColors.Ink;
            if (header) { using (var font = new Font(Font, FontStyle.Bold)) TextRenderer.DrawText(g, text, font, rect, ink, flags); }
            else TextRenderer.DrawText(g, text, Font, rect, ink, flags);
            using (var pen = new Pen(ThemeColors.Border)) g.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
        }
        protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
        {
            Cell(e.Graphics, e.Bounds, e.Header.Text, true, false, e.Header.TextAlign);
            if (e.ColumnIndex == 1) using (var pen = new Pen(ThemeColors.Border)) e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Top, e.Bounds.Left, e.Bounds.Bottom);
        }
        protected override void OnDrawItem(DrawListViewItemEventArgs e) { e.DrawDefault = false; }
        protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e) { Cell(e.Graphics, e.Bounds, e.SubItem.Text, false, e.Item.Selected, Columns[e.ColumnIndex].TextAlign); }
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if ((m.Msg != 0xF && m.Msg != 0x317 && m.Msg != 0x318) || !IsHandleCreated || Columns.Count != 2) return;
            using (var g = m.Msg == 0xF ? Graphics.FromHwnd(Handle) : Graphics.FromHdc(m.WParam))
            using (var pen = new Pen(ThemeColors.Border)) g.DrawLine(pen, Columns[0].Width, 0, Columns[0].Width, ClientSize.Height);
        }
        protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) rowSpacing.Dispose(); }
    }
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
