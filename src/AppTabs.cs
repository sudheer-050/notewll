using System;
using System.Drawing;
using System.Windows.Forms;
namespace Notewll
{
    // Native page content with borderless, keyboard-accessible navigation.
    sealed class AppTabs : UserControl
    {
        sealed class PageHost : TabControl
        {
            protected override void WndProc(ref Message message)
            {
                // TCM_ADJUSTRECT: pages cover the native property-sheet headers.
                if (message.Msg == 0x1328 && !DesignMode) { message.Result = IntPtr.Zero; return; }
                base.WndProc(ref message);
            }
        }
        readonly PageHost content = new PageHost { Dock = DockStyle.Fill, TabStop = false };
        readonly FlowLayoutPanel navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = Padding.Empty, Padding = new Padding(0, 0, 0, 12) };
        public TabControl.TabPageCollection TabPages { get { return content.TabPages; } }
        public int SelectedIndex { get { return content.SelectedIndex; } set { content.SelectedIndex = value; } }
        public TabPage SelectedTab { get { return content.SelectedTab; } set { content.SelectedTab = value; } }
        public AppTabs()
        {
            AutoScaleMode = AutoScaleMode.None;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.Margin = Padding.Empty;
            layout.Controls.Add(navigation, 0, 0); layout.Controls.Add(content, 0, 1); Controls.Add(layout);
            content.ControlAdded += (s, e) => { e.Control.TextChanged += PageTextChanged; RebuildNavigation(); };
            content.ControlRemoved += (s, e) => { e.Control.TextChanged -= PageTextChanged; RebuildNavigation(); };
            content.SelectedIndexChanged += (s, e) => RefreshSelection();
        }
        void PageTextChanged(object sender, EventArgs e) { RebuildNavigation(); }
        void RebuildNavigation()
        {
            navigation.SuspendLayout();
            while (navigation.Controls.Count > 0) { var old = navigation.Controls[0]; navigation.Controls.Remove(old); old.Dispose(); }
            foreach (TabPage page in TabPages)
            {
                var button = new HubButton { Text = page.Text, Font = Font, AccessibleName = page.Text, AccessibleRole = AccessibleRole.PageTab, Margin = new Padding(0, 0, 8, 4), Tag = page };
                button.Click += (s, e) => SelectedTab = page; navigation.Controls.Add(button);
            }
            SizeButtons(); RefreshSelection(); navigation.ResumeLayout(true);
        }
        void SizeButtons()
        {
            if (navigation == null) return;
            float scale; using (var graphics = CreateGraphics()) scale = graphics.DpiX / 96f;
            foreach (HubButton button in navigation.Controls)
            {
                button.Font = Font;
                button.Size = new Size(TextRenderer.MeasureText(button.Text, Font).Width + (int)(32 * scale), Math.Max(Font.Height + (int)(16 * scale), (int)(38 * scale)));
            }
        }
        void RefreshSelection()
        {
            foreach (HubButton button in navigation.Controls) { button.Selected = button.Tag == SelectedTab; button.AccessibleDescription = button.Selected ? "Selected view" : "Switch view"; button.Invalidate(); }
        }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); SizeButtons(); }
        protected override void OnLayout(LayoutEventArgs e) { SizeButtons(); base.OnLayout(e); }
        protected override bool ProcessCmdKey(ref Message message, Keys keys)
        {
            bool next = keys == (Keys.Control | Keys.Tab) || (navigation.ContainsFocus && keys == Keys.Right);
            bool previous = keys == (Keys.Control | Keys.Shift | Keys.Tab) || (navigation.ContainsFocus && keys == Keys.Left);
            if ((next || previous) && TabPages.Count > 0)
            {
                SelectedIndex = (SelectedIndex + (previous ? TabPages.Count - 1 : 1)) % TabPages.Count;
                if (navigation.ContainsFocus) navigation.Controls[SelectedIndex].Focus();
                return true;
            }
            return base.ProcessCmdKey(ref message, keys);
        }
    }
}
