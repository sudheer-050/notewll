using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
namespace Notewll
{
    static class DialogUi
    {
        sealed class MenuColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return ThemeColors.Card; } }
            public override Color ImageMarginGradientBegin { get { return ThemeColors.Card; } }
            public override Color ImageMarginGradientMiddle { get { return ThemeColors.Card; } }
            public override Color ImageMarginGradientEnd { get { return ThemeColors.Card; } }
            public override Color MenuItemSelected { get { return ThemeColors.Selected; } }
            public override Color MenuItemBorder { get { return ThemeColors.Border; } }
            public override Color MenuBorder { get { return ThemeColors.Border; } }
            public override Color SeparatorDark { get { return ThemeColors.Border; } }
            public override Color SeparatorLight { get { return ThemeColors.Card; } }
        }
        sealed class MenuRenderer : ToolStripProfessionalRenderer
        {
            public MenuRenderer() : base(new MenuColors()) { RoundedEdges = false; }
            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (!e.Item.Selected || !e.Item.Enabled) return;
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = Surface.Shape(new RectangleF(2, 1, e.Item.Width - 4, e.Item.Height - 2), 6 * e.Graphics.DpiX / 96))
                using (var fill = new SolidBrush(ThemeColors.Selected)) e.Graphics.FillPath(fill, path);
            }
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = Surface.Shape(new RectangleF(.5f, .5f, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1), 9 * e.Graphics.DpiX / 96))
                using (var pen = new Pen(ThemeColors.Border)) e.Graphics.DrawPath(pen, path);
            }
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                Color ink = !e.Item.Enabled ? ThemeColors.Muted : SystemInformation.HighContrast && e.Item.Selected ? SystemColors.HighlightText : ThemeColors.Ink;
                int pad = (int)(16 * e.Graphics.DpiX / 96);
                var bounds = new Rectangle(pad, 0, Math.Max(1, e.Item.Width - pad * 2), e.Item.Height);
                TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, bounds, ink, e.TextFormat | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                using (var pen = new Pen(ThemeColors.Border)) e.Graphics.DrawLine(pen, 12, e.Item.Height / 2, e.Item.Width - 12, e.Item.Height / 2);
            }
        }
        static readonly Font menuFont = new Font("Segoe UI", 10);
        static void ClipMenu(object sender, EventArgs e) { if (!SystemInformation.HighContrast) RoundedField.Clip((Control)sender); }
        static void StylePopup(ToolStripDropDown menu)
        {
            float scale; using (var g = menu.CreateGraphics()) scale = g.DpiX / 96f;
            menu.Renderer = SystemInformation.HighContrast ? (ToolStripRenderer)new ToolStripSystemRenderer() : new MenuRenderer();
            menu.Font = menuFont; menu.BackColor = ThemeColors.Card; menu.ForeColor = ThemeColors.Ink;
            menu.Padding = new Padding((int)(6 * scale)); menu.MinimumSize = new Size((int)(200 * scale), 0);
            var dropdown = menu as ToolStripDropDownMenu; if (dropdown != null) dropdown.ShowImageMargin = false;
            menu.SizeChanged -= ClipMenu; menu.SizeChanged += ClipMenu;
            int width = (int)(200 * scale);
            foreach (ToolStripItem item in menu.Items) width = Math.Max(width, TextRenderer.MeasureText(item.Text, menu.Font).Width + (int)(64 * scale));
            foreach (ToolStripItem item in menu.Items)
            {
                item.BackColor = ThemeColors.Card; item.ForeColor = item.Enabled ? ThemeColors.Ink : ThemeColors.Muted;
                item.AutoSize = false; item.Padding = Padding.Empty;
                item.Size = new Size(width - menu.Padding.Horizontal, item is ToolStripSeparator ? (int)(10 * scale) : Math.Max((int)(36 * scale), menu.Font.Height + (int)(16 * scale)));
                var parent = item as ToolStripMenuItem; if (parent != null && parent.HasDropDownItems) StylePopup(parent.DropDown);
            }
        }
        public static ContextMenuStrip PopupMenu(bool disposeOnClose = true)
        {
            var menu = new ContextMenuStrip { ShowImageMargin = false };
            menu.Opening += (s, e) => StylePopup(menu);
            if (!disposeOnClose) return menu;
            var ui = System.Threading.SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            // Closed fires before WinForms finishes hiding and reparenting the popup.
            // Dispose on the next UI message, after the close stack has returned.
            menu.Closed += (s, e) => ui.Post(state => menu.Dispose(), null);
            return menu;
        }
        static IEnumerable<Control> Descendants(Control root) { foreach (Control child in root.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
        public static Form Create(string title, Size size) { var form = new Form { Text = title, Icon = AppLogo.Icon, Font = new Font("Segoe UI", 10), AutoScaleDimensions = new SizeF(96, 96), AutoScaleMode = AutoScaleMode.None, ClientSize = size, MinimumSize = new Size(420, 320), StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false, Padding = new Padding(16) }; form.Load += (s, e) => { using (var graphics = form.CreateGraphics()) { float scale = graphics.DpiX / 96f; if (scale != 1) form.Scale(new SizeF(scale, scale)); } }; form.Shown += (s, e) => { ThemeColors.Apply(form, ThemeColors.Mode); form.MinimumSize = form.Size; if (form.CancelButton == null) form.CancelButton = Descendants(form).OfType<Button>().FirstOrDefault(button => button.Text == "Cancel" || button.Text == "Close"); }; return form; }
        public static Label Text(string text) { return new Label { Text = text, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 10), UseMnemonic = false }; }
        public static HubButton Button(string text, EventHandler click, bool primary = false) { var button = new HubButton { Text = text, AutoSize = true, MinimumSize = new Size(120, 36), Padding = new Padding(12, 6, 12, 6), Primary = primary }; button.Click += click; return button; }
        public static string Lines(string text) { return (text ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine); }
    }
}
