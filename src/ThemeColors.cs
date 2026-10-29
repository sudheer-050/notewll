using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;

namespace Notewll
{
    static class ThemeColors
    {
        public static readonly string[] Names = { "System", "Light", "Dark", "Bookshop", "Ocean", "Forest", "Rose", "Midnight" };
        // Paper, card, ink, muted ink, button, selection, sidebar, border, accent, hover.
        static readonly Dictionary<string, Color[]> palettes = new Dictionary<string, Color[]> {
            { "Light", Colors(0xF7F8FC,0xFFFFFF,0x202638,0x5D6576,0xEFF1F8,0xE8E5FC,0xF0F2F8,0xDCE1EC,0x5C48C9,0xE5E8F2) },
            { "Dark", Colors(0x11141D,0x1A1F2C,0xEDF0F8,0xB1BACD,0x252C3D,0x35305B,0x151924,0x343D53,0xB6A5FF,0x30384C) },
            { "Bookshop", Colors(0xF7F1E7,0xFFFCF6,0x302820,0x6C5B4D,0xEEE3D3,0xDCC8AC,0xEFE5D6,0xD6C8B6,0x775238,0xE3D2BA) },
            { "Ocean", Colors(0xF2F8FC,0xFFFFFF,0x173449,0x506978,0xE3F0F7,0xCBE4F1,0xE8F2F8,0xC6DDE9,0x146B8F,0xD6EAF4) },
            { "Forest", Colors(0xF3F7F2,0xFFFFFF,0x233D2C,0x536B59,0xE6EFE3,0xCFE1C8,0xEAF1E6,0xCDDCC7,0x356747,0xDCE9D5) },
            { "Rose", Colors(0xFCF5F7,0xFFFCFD,0x472C39,0x7B5C6A,0xF5E5ED,0xEDD0DE,0xF8EBF1,0xE5CDDA,0x974664,0xF0DAE5) },
            { "Midnight", Colors(0x111B2C,0x19283D,0xEDF3FC,0xADBCD2,0x223650,0x314D70,0x152137,0x344961,0x8FBAF1,0x294365) }
        };
        static Color[] Colors(params int[] values) { return values.Select(value => Color.FromArgb(value >> 16 & 255, value >> 8 & 255, value & 255)).ToArray(); }
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> watched = new System.Runtime.CompilerServices.ConditionalWeakTable<Control, object>();
        static string mode = "System";
        static bool systemDark = ReadSystemDark();
        internal static bool Valid(string value) { return Array.IndexOf(Names, value) >= 0; }
        public static string Mode
        {
            get { return mode; }
            set { mode = Valid(value) ? value : "System"; if (mode == "System") systemDark = ReadSystemDark(); }
        }
        static bool ReadSystemDark() { try { using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) return key != null && Convert.ToInt32(key.GetValue("AppsUseLightTheme", 1)) == 0; } catch { return false; } }
        static Color[] Palette { get { return palettes[Mode == "System" ? (systemDark ? "Dark" : "Light") : Mode]; } }
        internal static Color[] PreviewColors(string name)
        {
            if (SystemInformation.HighContrast) return new[] { SystemColors.Window, SystemColors.Window, SystemColors.WindowText, SystemColors.WindowText, SystemColors.Control, SystemColors.Highlight, SystemColors.Control, SystemColors.WindowFrame, SystemColors.Highlight, SystemColors.Control };
            if (!Valid(name)) name = "System";
            return (Color[])palettes[name == "System" ? (ReadSystemDark() ? "Dark" : "Light") : name].Clone();
        }
        public static bool Dark { get { return Mode == "Dark" || Mode == "Midnight" || Mode == "System" && systemDark; } }
        public static Color AccentInk { get { return SystemInformation.HighContrast ? SystemColors.HighlightText : Dark ? Palette[0] : Color.White; } }
        public static Color Paper { get { return SystemInformation.HighContrast ? SystemColors.Window : Palette[0]; } }
        public static Color Card { get { return SystemInformation.HighContrast ? SystemColors.Window : Palette[1]; } }
        public static Color Ink { get { return SystemInformation.HighContrast ? SystemColors.WindowText : Palette[2]; } }
        public static Color Muted { get { return SystemInformation.HighContrast ? SystemColors.WindowText : Palette[3]; } }
        public static Color Button { get { return SystemInformation.HighContrast ? SystemColors.Control : Palette[4]; } }
        public static Color Selected { get { return SystemInformation.HighContrast ? SystemColors.Highlight : Palette[5]; } }
        public static Color Sidebar { get { return SystemInformation.HighContrast ? SystemColors.Control : Palette[6]; } }
        public static Color Border { get { return SystemInformation.HighContrast ? SystemColors.WindowFrame : Palette[7]; } }
        public static Color Accent { get { return SystemInformation.HighContrast ? SystemColors.Highlight : Palette[8]; } }
        public static Color Hover { get { return SystemInformation.HighContrast ? SystemColors.Highlight : Palette[9]; } }
        public static void Apply(Control root, string mode)
        {
            Mode = mode;
            ApplyControls(root);
        }
        internal static void ApplyControls(Control root)
        {
            HiddenScrollbars.Attach(root);
            var textField = root as TextBoxBase; if (textField != null) textField.BorderStyle = BorderStyle.None;
            var listField = root as ListView; if (listField != null) listField.BorderStyle = BorderStyle.None;
            var listBox = root as ListBox; if (listBox != null) listBox.BorderStyle = BorderStyle.None;
            var grid = root as DataGridView;
            if (grid != null) { grid.BorderStyle = BorderStyle.None; grid.EnableHeadersVisualStyles = false; grid.BackgroundColor = Card; grid.GridColor = Border; grid.DefaultCellStyle.BackColor = Card; grid.DefaultCellStyle.ForeColor = Ink; grid.DefaultCellStyle.SelectionBackColor = Selected; grid.DefaultCellStyle.SelectionForeColor = Ink; grid.ColumnHeadersDefaultCellStyle.BackColor = Button; grid.ColumnHeadersDefaultCellStyle.ForeColor = Ink; grid.RowHeadersDefaultCellStyle.BackColor = Button; grid.RowHeadersDefaultCellStyle.ForeColor = Ink; }
            var number = root as NumericUpDown; if (number != null) number.BorderStyle = BorderStyle.None;
            var panel = root as Panel; if (panel != null) panel.BorderStyle = BorderStyle.None;
            var check = root as CheckBox;
            if (check != null) { check.FlatStyle = FlatStyle.Flat; check.FlatAppearance.BorderColor = Border; check.FlatAppearance.CheckedBackColor = Selected; check.FlatAppearance.MouseOverBackColor = Hover; check.FlatAppearance.MouseDownBackColor = Selected; }
            var radio = root as RadioButton; if (radio != null) radio.FlatStyle = FlatStyle.Flat;
            var tabPage = root as TabPage; if (tabPage != null) tabPage.UseVisualStyleBackColor = false;
            var buttonBase = root as ButtonBase; if (buttonBase != null) buttonBase.UseVisualStyleBackColor = false;
            var surface = root as Surface;
            if (surface != null) surface.Fill = Card;
            root.BackColor = root.Name == "AppSidebar" ? Sidebar : root is TabPage || root is TabControl ? Paper : surface != null ? Surface.CanvasColor(root.Parent) : root is TextBoxBase || root is ListBox || root is ListView || root is ComboBox || root is NumericUpDown || root is DataGridView ? Card : root.Parent == null ? Paper : Surface.CanvasColor(root.Parent);
            if (textField != null && !textField.ReadOnly && !textField.Multiline && !(root.Parent is UpDownBase)) root.BackColor = Button;
            root.ForeColor = root is Label && !root.Font.Bold && root.Font.Size < 11 ? Muted : Ink;
            var label = root as Label; if (label != null) label.UseMnemonic = false;
            var choice = root as ComboBox;
            if (choice != null && choice.DrawMode != DrawMode.OwnerDrawFixed)
            {
                choice.FlatStyle = FlatStyle.Flat; choice.DrawMode = DrawMode.OwnerDrawFixed;
                Action sizeRows = () => { using (var g = choice.CreateGraphics()) choice.ItemHeight = Math.Max((int)(26 * g.DpiX / 96), choice.Font.Height + (int)(8 * g.DpiX / 96)); };
                sizeRows(); choice.FontChanged += (s, e) => sizeRows();
                choice.DrawItem += (s, e) => {
                    bool selected = (e.State & DrawItemState.Selected) != 0;
                    float scale = e.Graphics.DpiX / 96f;
                    using (var brush = new SolidBrush(Card)) e.Graphics.FillRectangle(brush, e.Bounds);
                    if (selected)
                    {
                        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        using (var path = Surface.Shape(new RectangleF(e.Bounds.X + 3 * scale, e.Bounds.Y + scale, Math.Max(1, e.Bounds.Width - 6 * scale), Math.Max(1, e.Bounds.Height - 2 * scale)), 5 * scale))
                        using (var brush = new SolidBrush(Selected)) e.Graphics.FillPath(brush, path);
                    }
                    string value = e.Index >= 0 ? choice.GetItemText(choice.Items[e.Index]) : choice.Text;
                    Color ink = !choice.Enabled ? Muted : SystemInformation.HighContrast && selected ? SystemColors.HighlightText : Ink;
                    var text = new Rectangle(e.Bounds.X + (int)(12 * scale), e.Bounds.Y, Math.Max(1, e.Bounds.Width - (int)(42 * scale)), e.Bounds.Height);
                    TextRenderer.DrawText(e.Graphics, value, choice.Font, text, ink, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    if (e.Index >= 0 && e.Index == choice.SelectedIndex && (e.State & DrawItemState.ComboBoxEdit) == 0)
                        TextRenderer.DrawText(e.Graphics, "\u2713", choice.Font, new Rectangle(e.Bounds.Right - (int)(28 * scale), e.Bounds.Y, (int)(22 * scale), e.Bounds.Height), ink, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
                };
            }
            if (listBox != null && listBox.DrawMode != DrawMode.OwnerDrawFixed)
            {
                listBox.DrawMode = DrawMode.OwnerDrawFixed; listBox.ItemHeight = Math.Max(28, listBox.Font.Height + 10);
                listBox.DrawItem += (s, e) => {
                    bool selected = (e.State & DrawItemState.Selected) != 0;
                    using (var fill = new SolidBrush(selected ? Selected : Card)) e.Graphics.FillRectangle(fill, e.Bounds);
                    if (e.Index >= 0) TextRenderer.DrawText(e.Graphics, listBox.GetItemText(listBox.Items[e.Index]), listBox.Font, Rectangle.Inflate(e.Bounds, -6, 0), SystemInformation.HighContrast && selected ? SystemColors.HighlightText : Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
                };
            }
            var list = root as ListView;
            if (list != null && !list.OwnerDraw)
            {
                list.OwnerDraw = true;
                var columnWeights = list.Columns.Cast<ColumnHeader>().Select(column => Math.Max(1, column.Width)).ToArray();
                Action fitColumns = () => {
                    if (list.View != View.Details || list.Columns.Count == 0 || list.Columns.Count != columnWeights.Length || list.ClientSize.Width < 100) return;
                    int available = Math.Max(1, list.ClientSize.Width), total = columnWeights.Sum(), used = 0;
                    for (int i = 0; i < list.Columns.Count - 1; i++) { list.Columns[i].Width = Math.Max(24, columnWeights[i] * available / total); used += list.Columns[i].Width; }
                    list.Columns[list.Columns.Count - 1].Width = Math.Max(24, available - used);
                };
                list.SizeChanged += (s, e) => fitColumns(); fitColumns();
                list.DrawColumnHeader += (s, e) => { using (var brush = new SolidBrush(Button)) e.Graphics.FillRectangle(brush, e.Bounds); TextRenderer.DrawText(e.Graphics, e.Header.Text, list.Font, Rectangle.Inflate(e.Bounds, -6, 0), Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | (e.Header.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left)); };
                list.DrawSubItem += (s, e) => { bool selected = e.Item.Selected; using (var brush = new SolidBrush(selected ? Selected : Card)) e.Graphics.FillRectangle(brush, e.Bounds); TextRenderer.DrawText(e.Graphics, e.SubItem.Text, list.Font, Rectangle.Inflate(e.Bounds, -5, 0), SystemInformation.HighContrast && selected ? SystemColors.HighlightText : Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | (list.Columns[e.ColumnIndex].TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left)); };
                if (list.Name == "PersonalizationTable")
                {
                    list.DrawColumnHeader += (s, e) => { if (e.ColumnIndex == 0) using (var pen = new Pen(Muted, 2)) e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom); };
                    list.DrawSubItem += (s, e) => { if (e.ColumnIndex == 0) using (var pen = new Pen(Muted, 2)) e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom); };
                }
            }
            foreach (Control child in root.Controls) ApplyControls(child);
            object watch; if (!watched.TryGetValue(root, out watch)) { watched.Add(root, new object()); root.ControlAdded += (s, e) => ApplyControls(e.Control); }
            root.Invalidate(true);
        }
    }
}
