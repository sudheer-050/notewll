using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Notewll
{
    // Keep native chrome out of custom surfaces; BufferedScrollPanel supplies a themed scroll rail.
    sealed class HiddenScrollbars : NativeWindow
    {
        static readonly ConditionalWeakTable<Control, HiddenScrollbars> windows = new ConditionalWeakTable<Control, HiddenScrollbars>();
        [DllImport("user32.dll")] static extern bool ShowScrollBar(IntPtr handle, int bar, bool show);
        [StructLayout(LayoutKind.Sequential)] struct ComboInfo { public int Size; public Rectangle Item, Button; public int State; public IntPtr Combo, Edit, List; }
        [DllImport("user32.dll")] static extern bool GetComboBoxInfo(IntPtr handle, ref ComboInfo info);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr handle, out PopupRect rect);
        [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);
        [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr handle, IntPtr region, bool redraw);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr handle);
        [StructLayout(LayoutKind.Sequential)] struct PopupRect { public int Left, Top, Right, Bottom; }
        internal static void RoundDropdown(ComboBox combo)
        {
            if (SystemInformation.HighContrast || !combo.IsHandleCreated || !combo.DroppedDown) return;
            var info = new ComboInfo { Size = Marshal.SizeOf(typeof(ComboInfo)) }; PopupRect rect;
            if (!GetComboBoxInfo(combo.Handle, ref info) || !GetWindowRect(info.List, out rect)) return;
            int radius; using (var g = combo.CreateGraphics()) radius = (int)(16 * g.DpiX / 96);
            var region = CreateRoundRectRgn(0, 0, rect.Right - rect.Left + 1, rect.Bottom - rect.Top + 1, radius, radius);
            if (region != IntPtr.Zero && SetWindowRgn(info.List, region, true) == 0) DeleteObject(region);
        }
        HiddenScrollbars dropdown;
        bool hiding, listClient;
        HiddenScrollbars(IntPtr handle) { AssignHandle(handle); Hide(); }
        HiddenScrollbars(Control control)
        {
            listClient = control is ListView;
            control.HandleCreated += (s, e) => { AssignHandle(control.Handle); Hide(); };
            control.HandleDestroyed += (s, e) => ReleaseHandle();
            if (control.IsHandleCreated) { AssignHandle(control.Handle); Hide(); }
            var combo = control as ComboBox;
            if (combo != null)
            {
                combo.DropDown += (s, e) => {
                    var info = new ComboInfo { Size = Marshal.SizeOf(typeof(ComboInfo)) };
                    if (GetComboBoxInfo(combo.Handle, ref info) && info.List != IntPtr.Zero)
                    {
                        if (dropdown == null || dropdown.Handle != info.List) { if (dropdown != null) dropdown.ReleaseHandle(); dropdown = new HiddenScrollbars(info.List); }
                        dropdown.Hide();
                    }
                };
                control.Disposed += (s, e) => { if (dropdown != null) dropdown.ReleaseHandle(); };
            }
        }
        internal static void Attach(Control control)
        {
            if (!(control is ScrollableControl || control is TextBoxBase || control is ListView || control is ListBox || control is DataGridView || control is ComboBox)) return;
            windows.GetValue(control, key => new HiddenScrollbars(key));
            var grid = control as DataGridView; if (grid != null) grid.ScrollBars = ScrollBars.None;
        }
        void Hide() { if (hiding || Handle == IntPtr.Zero) return; hiding = true; try { ShowScrollBar(Handle, 3, false); } finally { hiding = false; } }
        protected override void WndProc(ref Message m)
        {
            if (listClient && m.Msg == 0x83) { m.Result = IntPtr.Zero; return; }
            if (m.Msg == 0x317 || m.Msg == 0x318) Hide();
            base.WndProc(ref m);
            if (m.Msg == 0x317 || m.Msg == 0x318 || m.Msg == 0x7D || m.Msg == 0x85 || m.Msg == 0x5 || m.Msg == 0x47 || m.Msg == 0xF || m.Msg == 0x115 || m.Msg == 0x114 || m.Msg == 0x20A || m.Msg == 0x20E) Hide();
        }
    }
    static class RoundedField
    {
        internal static void Clip(Control control)
        {
            if (!control.IsHandleCreated || control.Width < 2 || control.Height < 2) return;
            float radius; using (var g = control.CreateGraphics()) radius = 9 * g.DpiX / 96;
            using (var path = Surface.Shape(new RectangleF(0, 0, control.Width, control.Height), radius))
            {
                var old = control.Region; control.Region = new Region(path); if (old != null) old.Dispose();
            }
        }
        internal static void Outline(Graphics g, Control control, bool focus)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var shape = Surface.Shape(new RectangleF(.5f, .5f, control.Width - 1, control.Height - 1), 9 * g.DpiX / 96))
            using (var pen = new Pen(focus ? ThemeColors.Accent : ThemeColors.Border))
            using (var corners = new Region(control.ClientRectangle))
            using (var background = new SolidBrush(Surface.CanvasColor(control.Parent)))
            {
                corners.Exclude(shape); g.FillRegion(background, corners); g.DrawPath(pen, shape);
            }
        }
    }
    sealed class ThemedComboBox : ComboBox
    {
        bool hovering;
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); RoundedField.Clip(this); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); RoundedField.Clip(this); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { hovering = true; base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hovering = false; base.OnMouseLeave(e); Invalidate(); }
        protected override void OnDropDown(EventArgs e)
        {
            float scale; using (var g = CreateGraphics()) scale = g.DpiX / 96f;
            int desired = Width;
            foreach (var item in Items) desired = Math.Max(desired, TextRenderer.MeasureText(GetItemText(item), Font).Width + (int)(56 * scale));
            DropDownWidth = Math.Max(Width, Math.Min(desired, Screen.FromControl(this).WorkingArea.Width - (int)(32 * scale)));
            base.OnDropDown(e); BeginInvoke((Action)(() => { if (!IsDisposed) HiddenScrollbars.RoundDropdown(this); })); Invalidate();
        }
        protected override void OnDropDownClosed(EventArgs e) { base.OnDropDownClosed(e); Invalidate(); }
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if ((m.Msg != 0xF && m.Msg != 0x317 && m.Msg != 0x318) || DropDownStyle == ComboBoxStyle.Simple || !IsHandleCreated) return;
            using (var g = m.Msg == 0xF ? Graphics.FromHwnd(Handle) : Graphics.FromHdc(m.WParam))
            {
                float scale = g.DpiX / 96f;
                int arrowWidth = (int)(32 * scale), x = Width - arrowWidth / 2, y = Height / 2;
                if (DropDownStyle == ComboBoxStyle.DropDownList)
                {
                    using (var background = new SolidBrush(hovering || DroppedDown ? ThemeColors.Button : ThemeColors.Card)) g.FillRectangle(background, ClientRectangle);
                    TextRenderer.DrawText(g, Text, Font, new Rectangle((int)(12 * scale), 1, Math.Max(1, Width - arrowWidth - (int)(16 * scale)), Height - 2), Enabled ? ThemeColors.Ink : ThemeColors.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
                else using (var background = new SolidBrush(ThemeColors.Card)) g.FillRectangle(background, Width - arrowWidth, 1, arrowWidth, Height - 2);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var pen = new Pen(Enabled && (Focused || DroppedDown) ? ThemeColors.Accent : ThemeColors.Muted, Math.Max(1, 1.5f * scale)))
                {
                    pen.StartCap = pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    float direction = DroppedDown ? -1 : 1;
                    g.DrawLines(pen, new[] { new PointF(x - 3 * scale, y - direction * 1.5f * scale), new PointF(x, y + direction * 1.5f * scale), new PointF(x + 3 * scale, y - direction * 1.5f * scale) });
                }
                RoundedField.Outline(g, this, Focused || DroppedDown);
            }
        }
    }
    sealed class ThemedDateTimePicker : DateTimePicker
    {
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); RoundedField.Clip(this); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); RoundedField.Clip(this); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            // Keep native segment selection visible while editing with the keyboard.
            if ((m.Msg != 0xF && m.Msg != 0x317 && m.Msg != 0x318) || ContainsFocus || !IsHandleCreated) return;
            using (var g = m.Msg == 0xF ? Graphics.FromHwnd(Handle) : Graphics.FromHdc(m.WParam))
            using (var background = new SolidBrush(ThemeColors.Card))
            {
                g.FillRectangle(background, ClientRectangle);
                int arrow = SystemInformation.VerticalScrollBarWidth;
                TextRenderer.DrawText(g, Text, Font, new Rectangle(8, 0, Math.Max(1, Width - arrow - 16), Height), Enabled ? ThemeColors.Ink : ThemeColors.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, "\u25be", Font, new Rectangle(Width - arrow - 4, 0, arrow, Height), ThemeColors.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                RoundedField.Outline(g, this, false);
            }
        }
    }
    sealed class ThemedCheckBox : CheckBox
    {
        public override Size GetPreferredSize(Size proposedSize)
        {
            int size; using (var g = Graphics.FromHwnd(IntPtr.Zero)) size = Math.Max(16, (int)(16 * g.DpiX / 96));
            var text = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.SingleLine);
            return new Size(text.Width + size + 12 + Padding.Horizontal, Math.Max(size, text.Height) + 8 + Padding.Vertical);
        }
        public ThemedCheckBox() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); Cursor = Cursors.Hand; }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(BackColor); g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int size = Math.Max(16, (int)(16 * g.DpiX / 96)), top = Math.Max(0, (Height - size) / 2);
            var box = new RectangleF(1, top + 1, size - 2, size - 2);
            using (var shape = Surface.Shape(box, 4))
            using (var fill = new SolidBrush(Checked ? ThemeColors.Accent : ThemeColors.Button))
            using (var border = new Pen(Focused ? ThemeColors.Accent : ThemeColors.Border))
            {
                g.FillPath(fill, shape); g.DrawPath(border, shape);
            }
            if (CheckState != CheckState.Unchecked)
            using (var pen = new Pen(ThemeColors.AccentInk, 2))
            {
                if (CheckState == CheckState.Indeterminate) g.DrawLine(pen, size * .25f, top + size * .5f, size * .75f, top + size * .5f);
                else g.DrawLines(pen, new[] { new PointF(size * .24f, top + size * .5f), new PointF(size * .43f, top + size * .69f), new PointF(size * .77f, top + size * .3f) });
            }
            var text = new Rectangle(size + 7, 0, Math.Max(1, Width - size - 7), Height);
            TextRenderer.DrawText(g, Text, Font, text, Enabled ? ThemeColors.Ink : ThemeColors.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            if (Focused) ControlPaint.DrawFocusRectangle(g, text, ThemeColors.Ink, BackColor);
        }
    }
    sealed class ThemedTextBox : TextBox
    {
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wparam, IntPtr lparam);
        internal string Cue { get; set; }
        internal bool Embedded { get; set; }
        [StructLayout(LayoutKind.Sequential)] struct TextRect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll", EntryPoint = "SendMessageW")] static extern IntPtr SetTextRect(IntPtr handle, int message, IntPtr wparam, ref TextRect rect);
        public ThemedTextBox() { BorderStyle = BorderStyle.None; AutoSize = false; }
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); if (!Embedded) RoundedField.Clip(this); TextInsets(); }
        void TextInsets()
        {
            if (!IsHandleCreated) return;
            int pad; using (var g = CreateGraphics()) pad = Math.Max(4, (int)(8 * g.DpiX / 96));
            SendMessage(Handle, 0xD3, new IntPtr(3), new IntPtr(pad | pad << 16));
            if (Multiline) { var rect = new TextRect { Left = pad, Top = pad, Right = Math.Max(pad + 1, ClientSize.Width - pad), Bottom = Math.Max(pad + Font.Height, ClientSize.Height - pad) }; SetTextRect(Handle, 0xB3, IntPtr.Zero, ref rect); }
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!Embedded) RoundedField.Clip(this); TextInsets();
        }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if ((m.Msg != 0xF && m.Msg != 0x317 && m.Msg != 0x318) || !IsHandleCreated) return;
            using (var g = m.Msg == 0xF ? Graphics.FromHwnd(Handle) : Graphics.FromHdc(m.WParam))
            {
                if (!Embedded) RoundedField.Outline(g, this, Focused);
                if (TextLength == 0 && !String.IsNullOrEmpty(Cue)) TextRenderer.DrawText(g, Cue, Font, Rectangle.Inflate(ClientRectangle, -(int)(8 * g.DpiX / 96), Multiline ? -(int)(8 * g.DpiX / 96) : 0), ThemeColors.Muted, TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | (Multiline ? TextFormatFlags.WordBreak : TextFormatFlags.VerticalCenter));
            }
        }
    }
    sealed class SearchField : Panel
    {
        readonly ThemedTextBox input;
        internal const int FieldHeight = 38;
        internal SearchField(ThemedTextBox input, string cue)
        {
            this.input = input; Height = FieldHeight; AccessibleName = input.AccessibleName + " field";
            DoubleBuffered = true; ResizeRedraw = true;
            input.Dock = DockStyle.None; input.BorderStyle = BorderStyle.None; input.Embedded = true; input.Cue = cue;
            Controls.Add(input);
            input.GotFocus += (s, e) => Invalidate(); input.LostFocus += (s, e) => Invalidate();
            input.FontChanged += (s, e) => PerformLayout();
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e); if (input == null) return;
            int inset; using (var g = CreateGraphics()) inset = Math.Max(2, (int)(2 * g.DpiX / 96));
            int height = input.PreferredHeight;
            input.SetBounds(inset, Math.Max(0, (ClientSize.Height - height) / 2), Math.Max(1, ClientSize.Width - inset * 2), height);
        }
        protected override void OnClick(EventArgs e) { base.OnClick(e); input.Focus(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(ThemeColors.Button); RoundedField.Outline(e.Graphics, this, input.ContainsFocus);
        }
    }
    sealed class ThemedProgressBar : ProgressBar
    {
        public ThemedProgressBar() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(ThemeColors.Button);
            int width = (int)(ClientSize.Width * (Value - Minimum) / (double)Math.Max(1, Maximum - Minimum));
            using (var fill = new SolidBrush(ThemeColors.Accent)) e.Graphics.FillRectangle(fill, 0, 0, width, Height);
        }
    }
    sealed class ThemedTrackBar : TrackBar
    {
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if ((m.Msg != 0xF && m.Msg != 0x317 && m.Msg != 0x318) || !IsHandleCreated) return;
            using (var g = m.Msg == 0xF ? Graphics.FromHwnd(Handle) : Graphics.FromHdc(m.WParam))
            using (var rail = new Pen(ThemeColors.Border, 4))
            using (var active = new Pen(ThemeColors.Accent, 4))
            using (var thumb = new SolidBrush(Enabled ? ThemeColors.Accent : ThemeColors.Muted))
            {
                g.Clear(Parent == null ? ThemeColors.Card : Parent.BackColor);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                // Keep native mouse, arrow-key and accessibility behavior.
                int y = Math.Min(Height / 2, 16), left = 13, right = Math.Max(left, Width - 14);
                int x = left + (int)((right - left) * (Value - Minimum) / (double)Math.Max(1, Maximum - Minimum));
                g.DrawLine(rail, left, y, right, y); g.DrawLine(active, left, y, x, y); g.FillEllipse(thumb, x - 6, y - 6, 12, 12);
                if (Focused) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -2, -2), ThemeColors.Ink, BackColor);
            }
        }
    }
    // Preserve NumericUpDown validation and accessibility, with no spinner buttons.
    sealed class WheelNumber : NumericUpDown
    {
        public WheelNumber() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); BorderStyle = BorderStyle.None; TextAlign = HorizontalAlignment.Center; Cursor = Cursors.Hand; }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            foreach (Control child in Controls) if (!(child is TextBox)) { child.Visible = false; child.Width = 0; }
            foreach (Control child in Controls) if (child is TextBox) { child.BackColor = BackColor; child.SetBounds(0, Math.Max(0, (ClientSize.Height - child.Height) / 2), ClientSize.Width, child.Height); }
        }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(BackColor); }
        protected override void OnPaint(PaintEventArgs e) { e.Graphics.Clear(BackColor); }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (!ContainsFocus) return;
            var handled = e as HandledMouseEventArgs; if (handled != null) handled.Handled = true;
            Value = Math.Max(Minimum, Math.Min(Maximum, Value + Math.Sign(e.Delta) * Increment));
        }
    }
}
