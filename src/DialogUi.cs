using System;
using System.Drawing;
using System.Windows.Forms;
namespace Notewll
{
    static class DialogUi
    {
        public static ContextMenuStrip PopupMenu()
        {
            var menu = new ContextMenuStrip();
            var ui = System.Threading.SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            // Closed fires before WinForms finishes hiding and reparenting the popup.
            // Dispose on the next UI message, after the close stack has returned.
            menu.Closed += (s, e) => ui.Post(state => menu.Dispose(), null);
            return menu;
        }
        public static Form Create(string title, Size size) { return new Form { Text = title, Icon = AppLogo.Icon, Font = new Font("Segoe UI", 10), AutoScaleDimensions = new SizeF(96, 96), AutoScaleMode = AutoScaleMode.Dpi, ClientSize = size, MinimumSize = new Size(420, 320), StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false, Padding = new Padding(16) }; }
        public static Label Text(string text) { return new Label { Text = text, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 10), UseMnemonic = false }; }
        public static HubButton Button(string text, EventHandler click, bool primary = false) { var button = new HubButton { Text = text, AutoSize = true, MinimumSize = new Size(120, 36), Padding = new Padding(12, 6, 12, 6), Primary = primary }; button.Click += click; return button; }
        public static string Lines(string text) { return (text ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine); }
    }
}
