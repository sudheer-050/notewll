using System;
using System.Drawing;
using System.Windows.Forms;

namespace Notewll
{
    static class ThemeColors
    {
        public static string Mode { get { return "System"; } set { } }
        public static bool Dark
        {
            get
            {
                try { using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) return key != null && Convert.ToInt32(key.GetValue("AppsUseLightTheme", 1)) == 0; } catch { return false; }
            }
        }
        public static Color AccentInk { get { return Color.White; } }
        public static Color Paper { get { return SystemInformation.HighContrast ? SystemColors.Window : Dark ? Color.FromArgb(26, 24, 33) : Color.FromArgb(251, 250, 254); } }
        public static Color Card { get { return SystemInformation.HighContrast ? SystemColors.Window : Dark ? Color.FromArgb(36, 32, 44) : Color.White; } }
        public static Color Ink { get { return SystemInformation.HighContrast ? SystemColors.WindowText : Dark ? Color.FromArgb(237, 239, 243) : Color.FromArgb(39, 42, 47); } }
        public static Color Muted { get { return SystemInformation.HighContrast ? SystemColors.WindowText : Dark ? Color.FromArgb(174, 182, 192) : Color.FromArgb(115, 116, 118); } }
        public static Color Button { get { return SystemInformation.HighContrast ? SystemColors.Control : Dark ? Color.FromArgb(48, 41, 60) : Color.FromArgb(243, 239, 250); } }
        public static Color Selected { get { return SystemInformation.HighContrast ? SystemColors.Highlight : Dark ? Color.FromArgb(70, 53, 96) : Color.FromArgb(232, 222, 248); } }
        public static Color Sidebar { get { return SystemInformation.HighContrast ? SystemColors.Control : Dark ? Color.FromArgb(31, 26, 42) : Color.FromArgb(245, 242, 252); } }
        public static Color Border { get { return SystemInformation.HighContrast ? SystemColors.WindowFrame : Dark ? Color.FromArgb(66, 53, 83) : Color.FromArgb(226, 218, 239); } }
        public static Color Accent { get { return SystemInformation.HighContrast ? SystemColors.Highlight : Dark ? Color.FromArgb(114, 85, 164) : Color.FromArgb(104, 76, 156); } }
        public static Color Hover { get { return SystemInformation.HighContrast ? SystemColors.Highlight : Dark ? Color.FromArgb(66, 51, 87) : Color.FromArgb(235, 227, 247); } }
        public static void Apply(Control root, string mode)
        {
            Mode = mode;
            var tabPage = root as TabPage; if (tabPage != null) tabPage.UseVisualStyleBackColor = false;
            var buttonBase = root as ButtonBase; if (buttonBase != null) buttonBase.UseVisualStyleBackColor = false;
            var surface = root as Surface;
            if (surface != null) surface.Fill = Card;
            root.BackColor = root.Name == "AppSidebar" ? Sidebar : root is TabPage || root is TabControl ? Paper : surface != null ? Surface.CanvasColor(root.Parent) : root is TextBoxBase || root is ListBox || root is ListView || root is ComboBox || root is DataGridView ? Card : root.Parent == null ? Paper : Surface.CanvasColor(root.Parent);
            root.ForeColor = root is Label && !root.Font.Bold && root.Font.Size < 11 ? Muted : Ink;
            var label = root as Label; if (label != null) label.UseMnemonic = false;
            var choice = root as ComboBox;
            if (choice != null && choice.DrawMode != DrawMode.OwnerDrawFixed)
            {
                choice.FlatStyle = FlatStyle.Flat; choice.DrawMode = DrawMode.OwnerDrawFixed; choice.ItemHeight = Math.Max(22, choice.Font.Height + 6);
                choice.DrawItem += (s, e) => { bool selected = (e.State & DrawItemState.Selected) != 0; using (var brush = new SolidBrush(selected ? Selected : Card)) e.Graphics.FillRectangle(brush, e.Bounds); string value = e.Index >= 0 ? choice.GetItemText(choice.Items[e.Index]) : choice.Text; TextRenderer.DrawText(e.Graphics, value, choice.Font, Rectangle.Inflate(e.Bounds, -4, 0), SystemInformation.HighContrast && selected ? SystemColors.HighlightText : Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix); if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle(); };
                choice.FontChanged += (s, e) => choice.ItemHeight = Math.Max(22, choice.Font.Height + 6);
            }
            var list = root as ListView;
            if (list != null && !list.OwnerDraw)
            {
                list.OwnerDraw = true;
                list.DrawColumnHeader += (s, e) => { using (var brush = new SolidBrush(Button)) e.Graphics.FillRectangle(brush, e.Bounds); TextRenderer.DrawText(e.Graphics, e.Header.Text, list.Font, Rectangle.Inflate(e.Bounds, -6, 0), Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis); };
                list.DrawSubItem += (s, e) => { bool selected = e.Item.Selected; using (var brush = new SolidBrush(selected ? Selected : Card)) e.Graphics.FillRectangle(brush, e.Bounds); TextRenderer.DrawText(e.Graphics, e.SubItem.Text, list.Font, Rectangle.Inflate(e.Bounds, -5, 0), SystemInformation.HighContrast && selected ? SystemColors.HighlightText : Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix); };
            }
            foreach (Control child in root.Controls) Apply(child, mode);
            root.Invalidate(true);
        }
    }
}
