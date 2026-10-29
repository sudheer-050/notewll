using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
namespace Notewll
{
    static class UiAudit
    {
        static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static readonly List<object> findings = new List<object>();
        static readonly List<string> captures = new List<string>();
        static object Field(object target, string name) { return target.GetType().GetField(name, Flags).GetValue(target); }
        static IEnumerable<Control> All(Control root) { foreach (Control child in root.Controls) { yield return child; foreach (var descendant in All(child)) yield return descendant; } }
        static string Name(Control c) { return !String.IsNullOrWhiteSpace(c.AccessibleName) ? c.AccessibleName : !String.IsNullOrWhiteSpace(c.Text) ? c.Text.Replace("\r", " ").Replace("\n", " ") : c.GetType().Name; }
        static void Inspect(Form form, string state)
        {
            foreach (var c in All(form).Where(c => c.Visible && c.Width > 0 && c.Height > 0))
            {
                var label = c as Label;
                if (label != null && !label.AutoSize && !label.AutoEllipsis && !String.IsNullOrWhiteSpace(label.Text))
                {
                    Size needed;
                    using (var g = label.CreateGraphics())
                    {
                        if (label.UseCompatibleTextRendering) { var size = g.MeasureString(label.Text, label.Font, Math.Max(1, label.Width - label.Padding.Horizontal)); needed = Size.Ceiling(size); }
                        else needed = TextRenderer.MeasureText(g, label.Text, label.Font, new Size(Math.Max(1, label.Width - label.Padding.Horizontal), Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                    }
                    if (needed.Height > label.Height - label.Padding.Vertical + 4) findings.Add(new { state, issue = "text-height", control = Name(c), actual = c.Height, needed = needed.Height + label.Padding.Vertical });
                }
                if (label != null && c.Parent != null)
                {
                    foreach (var sibling in c.Parent.Controls.Cast<Control>().Where(sibling => sibling.Visible && (sibling is ButtonBase || sibling is TextBoxBase || sibling is ComboBox)))
                    {
                        var intersection = Rectangle.Intersect(c.Bounds, sibling.Bounds);
                        if (intersection.Width > 3 && intersection.Height > 3) findings.Add(new { state, issue = "label-over-control", control = Name(c), covered = Name(sibling) });
                    }
                }
                var parent = c.Parent as ScrollableControl;
                if (parent != null && parent.AutoScroll) { if (parent.DisplayRectangle.Width > parent.ClientSize.Width + 5) findings.Add(new { state, issue = "horizontal-overflow", control = Name(parent), actual = parent.ClientSize.Width, needed = parent.DisplayRectangle.Width }); continue; }
                if ((c.Dock == DockStyle.None || c.Parent is TableLayoutPanel || c.Parent is Form) && c.Parent != null && (c.Right > c.Parent.ClientSize.Width + 3 || c.Bottom > c.Parent.ClientSize.Height + 3) && !(c.Parent is UpDownBase))
                    findings.Add(new { state, issue = "outside-parent", control = Name(c), bounds = c.Bounds.ToString(), parent = c.Parent.GetType().Name + " " + c.Parent.ClientSize });
            }
        }
        static void Capture(Form form, string root, string state)
        {
            Application.DoEvents(); form.Refresh(); Application.DoEvents(); Inspect(form, state);
            using (var bitmap = new Bitmap(form.Width, form.Height)) using (var graphics = Graphics.FromImage(bitmap))
            {
                var dc = graphics.GetHdc(); try { if (!Native.PrintWindow(form.Handle, dc, 0)) throw new Exception("Window capture failed: " + state); } finally { graphics.ReleaseHdc(dc); }
                bitmap.Save(Path.Combine(root, state + ".png"));
            }
            captures.Add(state); File.WriteAllText(Path.Combine(root, "findings.json"), new JavaScriptSerializer().Serialize(findings), Encoding.UTF8);
        }
        [StructLayout(LayoutKind.Sequential)] struct ComboInfo { public int Size; public Rectangle Item, Button; public int State; public IntPtr Combo, Edit, List; }
        [StructLayout(LayoutKind.Sequential)] struct WindowRect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] static extern bool GetComboBoxInfo(IntPtr handle, ref ComboInfo info);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr handle, out WindowRect rect);
        static void CapturePopup(IntPtr handle, string root, string state)
        {
            Application.DoEvents(); WindowRect rect;
            if (!GetWindowRect(handle, out rect) || rect.Right <= rect.Left || rect.Bottom <= rect.Top) throw new Exception("Dropdown did not open: " + state);
            using (var bitmap = new Bitmap(rect.Right - rect.Left, rect.Bottom - rect.Top)) using (var graphics = Graphics.FromImage(bitmap))
            {
                var dc = graphics.GetHdc(); try { if (!Native.PrintWindow(handle, dc, 0)) throw new Exception("Dropdown capture failed: " + state); } finally { graphics.ReleaseHdc(dc); }
                bitmap.Save(Path.Combine(root, state + ".png"));
            }
            captures.Add(state);
        }
        static void CaptureChoice(ComboBox choice, string root, string state)
        {
            choice.Focus(); choice.DroppedDown = true; Application.DoEvents();
            try
            {
                var info = new ComboInfo { Size = Marshal.SizeOf(typeof(ComboInfo)) };
                if (!GetComboBoxInfo(choice.Handle, ref info) || info.List == IntPtr.Zero) throw new Exception("Dropdown list is unavailable: " + state);
                if (choice.ItemHeight < choice.Font.Height + 6) throw new Exception("Dropdown rows are cramped: " + state);
                CapturePopup(info.List, root, state);
            }
            finally { choice.DroppedDown = false; Application.DoEvents(); }
        }
        static void ScrollViews(Form form, string root, string state)
        {
            Capture(form, root, state);
            var panels = All(form).OfType<ScrollableControl>().Where(c => c.Visible && c.AutoScroll && c.DisplayRectangle.Height > c.ClientSize.Height + 5).ToArray();
            int i = 0;
            foreach (var panel in panels)
            {
                int range = Math.Max(0, panel.DisplayRectangle.Height - panel.ClientSize.Height);
                if (panel is BufferedScrollPanel)
                {
                    typeof(BufferedScrollPanel).GetMethod("OnMouseDown", Flags).Invoke(panel, new object[] { new MouseEventArgs(MouseButtons.Left, 1, panel.ClientSize.Width - 2, panel.ClientSize.Height - 2, 0) });
                    typeof(BufferedScrollPanel).GetMethod("OnMouseUp", Flags).Invoke(panel, new object[] { new MouseEventArgs(MouseButtons.Left, 1, panel.ClientSize.Width - 2, panel.ClientSize.Height - 2, 0) });
                    if (-panel.AutoScrollPosition.Y < range - 2) throw new Exception("The scroll rail cannot reach the bottom: " + state);
                }
                else panel.AutoScrollPosition = new Point(0, range);
                Application.DoEvents(); Capture(form, root, state + "-bottom" + (++i)); panel.AutoScrollPosition = Point.Empty;
            }
        }
        static void Dialog(SettingsForm owner, string root, string state, Action open)
        {
            bool found = false; Exception failure = null;
            using (var timer = new Timer { Interval = 120 })
            {
                timer.Tick += (s, e) => {
                    var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f != owner && f.Visible); if (dialog == null) return;
                    timer.Stop(); found = true;
                    try
                    {
                        if (dialog.CancelButton == null) throw new Exception("Dialog has no keyboard cancel action: " + state);
                        ScrollViews(dialog, root, state);
                        var customDates = All(dialog).OfType<HubButton>().FirstOrDefault(button => button.Text == "Custom");
                        if (customDates != null) { customDates.PerformClick(); ScrollViews(dialog, root, state + "-custom-dates"); }
                        var tabs = All(dialog).OfType<AppTabs>().FirstOrDefault();
                        if (tabs != null) foreach (TabPage page in tabs.TabPages) { tabs.SelectedTab = page; ScrollViews(dialog, root, state + "-" + page.Text.Replace(" ", "-")); }
                        if (dialog.FormBorderStyle == FormBorderStyle.Sizable) { dialog.Size = new Size(Math.Max(dialog.MinimumSize.Width, (int)(dialog.Width * .9)), Math.Max(dialog.MinimumSize.Height, (int)(dialog.Height * .9))); ScrollViews(dialog, root, state + "-resized"); }
                    }
                    catch (Exception ex) { failure = ex; }
                    finally { dialog.Close(); }
                };
                timer.Start(); open(); timer.Stop();
            }
            if (!found) throw new Exception("Dialog was not opened: " + state); if (failure != null) throw failure;
        }
        public static void Run(string root)
        {
            Directory.CreateDirectory(root); Config.DiagnosticDataRoot(Path.Combine(root, "data")); Application.EnableVisualStyles();
            string installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "LocalDictation");
            if (File.Exists(Path.Combine(installed, "native", "libvosk.dll")) && File.Exists(Path.Combine(installed, "model-small-en-us", "am", "final.mdl"))) SpeechEngine.DiagnosticRoot(installed);
            var store = new NoteStore(Path.Combine(root, "notes"));
            var note = store.Create("Project ideas and next steps", "Computer", "Alex"); note.Kind = "Meeting"; note.WrittenNotes = "Keep the original recording. Review the draft before sending."; note.Status = "Complete";
            for (int i = 0; i < 18; i++) note.Segments.Add(new NoteSegment { Complete = true, Source = "Computer", StartFrame = i * 16000, RawText = "um let's review the design and keep every original detail " + i, Text = "Let's review the design and keep every original detail " + i + "." });
            store.Save(note);
            var config = new Config();
            config.Personalization.Add(new PersonalRule { Kind = "Word", Trigger = "notewell", Replacement = "Notewll" });
            config.Personalization.Add(new PersonalRule { Kind = "Snippet", Trigger = "my sign off", Replacement = "Thanks for your time.\r\nKind regards, Alex" });
            using (var hub = new SettingsForm(config, "um let's review the design", "Let's review the design.", store))
            {
                hub.Show(); Application.DoEvents(); var normal = hub.Size;
                foreach (string theme in new[] { "Light", "Dark" })
                {
                    config.Theme = theme; ThemeColors.Apply(hub, theme);
                    foreach (bool compact in new[] { true, false })
                    {
                        hub.Size = compact ? hub.MinimumSize : normal;
                        string prefix = theme + (compact ? "-compact-" : "-normal-");
                        foreach (string page in new[] { "Home", "Notes", "Dictionary", "Snippets", "Style", "Help" }) { hub.ShowPage(page); ScrollViews(hub, root, prefix + page); }
                        hub.ShowPage("Notes"); hub.Notes.SearchFor("no matching saved entries"); Capture(hub, root, prefix + "Notes-empty-search"); hub.Notes.SearchFor("");
                        hub.ShowPage("Playground"); ScrollViews(hub, root, prefix + "Practice");
                        hub.ShowPage("Preferences"); var settings = (AppTabs)Field(hub, "settingsTabs");
                        foreach (TabPage page in settings.TabPages) { hub.SelectSettingsCategory(page.Text); ScrollViews(hub, root, prefix + "Settings-" + page.Text); }
                        hub.ShowPage("Notes"); hub.Notes.SelectNote(note.Id); var tabs = (AppTabs)Field(hub.Notes, "tabs");
                        foreach (TabPage tab in tabs.TabPages) { tabs.SelectedTab = tab; ScrollViews(hub, root, prefix + "Meeting-" + tab.Text.Replace(" ", "-")); }
                        hub.Notes.NewWrittenNote(); ScrollViews(hub, root, prefix + "New-note");
                    }
                    hub.Size = normal; hub.ShowPage("Home");
                    using (var menu = hub.BuildHomeCreateMenu())
                    {
                        var create = ((Surface)Field(hub, "homeActions")).Controls.OfType<HubButton>().Single();
                        menu.Show(create, new Point(create.Width, create.Height), ToolStripDropDownDirection.BelowLeft); Application.DoEvents(); menu.Items[1].Select();
                        if (menu.Items.Cast<ToolStripItem>().Any(item => item.Height < menu.Font.Height + 12)) throw new Exception("Create menu rows are cramped.");
                        CapturePopup(menu.Handle, root, theme + "-dropdown-Create"); menu.Close();
                    }
                    hub.ShowPage("Style"); CaptureChoice((ComboBox)Field(hub, "correctionMode"), root, theme + "-dropdown-Cleanup");
                    hub.ShowPage("Preferences"); hub.SelectSettingsCategory("Recording"); CaptureChoice((ComboBox)Field(hub, "speechLanguage"), root, theme + "-dropdown-Language");
                    hub.ShowPage("Notes");
                    using (var menu = DialogUi.PopupMenu()) { menu.Items.Add("Open note"); menu.Items.Add("Copy text"); menu.Show(hub, new Point(hub.ClientSize.Width / 2, hub.ClientSize.Height / 2)); Application.DoEvents(); if (menu.BackColor != ThemeColors.Card || menu.ForeColor != ThemeColors.Ink) throw new Exception("Context menu has mismatched theme colors."); menu.Close(); }
                    string dialogPrefix = theme + "-dialog-"; hub.Size = normal;
                    Dialog(hub, root, dialogPrefix + "Storage", () => NotesStorageDialog.Show(hub, store, config));
                    Dialog(hub, root, dialogPrefix + "Dictionary", () => PersonalRule.Edit(hub, config.Copy(), null));
                    Dialog(hub, root, dialogPrefix + "Snippet", () => PersonalRule.Prompt(hub, new PersonalRule { Kind = "Snippet", Trigger = "my sign off", Replacement = "Thanks for your time.\r\nKind regards, Alex" }, theme, config));
                    Dialog(hub, root, dialogPrefix + "Typed-word", () => PersonalRule.PromptText(hub, new PersonalRule { Kind = "Word", Trigger = "notewell", Replacement = "Notewll" }, theme, config));
                    Dialog(hub, root, dialogPrefix + "Voice-edit", () => LocalCommandDialog.Show(hub, config, "Send the report to Alex on Tuesday.", null));
                    Dialog(hub, root, dialogPrefix + "Calendar", () => typeof(SettingsForm).GetMethod("ShowMeetingCalendar", Flags).Invoke(hub, null));
                    hub.ShowPage("Style"); Dialog(hub, root, dialogPrefix + "App-styles", () => All(hub).OfType<Button>().Single(b => b.Visible && b.Text == "App rules...").PerformClick());
                    Dialog(hub, root, dialogPrefix + "Voice-word", () => { using (var dialog = new VoiceWordDialog(config, null)) dialog.ShowDialog(hub); });
                    Dialog(hub, root, dialogPrefix + "Voice-enrollment", () => { using (var dialog = new VoiceEnrollmentDialog(config, true)) dialog.ShowDialog(hub); });
                    Dialog(hub, root, dialogPrefix + "Recovery", () => { using (var dialog = new RecoveryForm(new DictationRecovery { Text = "Please review this recovered dictation.", Message = "Text is ready to copy." }, done => done())) dialog.ShowDialog(hub); });
                    Dialog(hub, root, dialogPrefix + "Color", () => { using (var dialog = new BallColorPicker(config, false)) dialog.ShowDialog(hub); });
                    Dialog(hub, root, dialogPrefix + "Cleanup", () => HistoryCleanupDialog.Choose(hub, store.List()));
                    Dialog(hub, root, dialogPrefix + "Info", () => NotesEntryInfo.Show(hub, store, note));
                    hub.ShowPage("Notes"); hub.Notes.SelectNote(note.Id);
                    foreach (var pair in new[] { new[] { "Export", "ShowExportDialog" }, new[] { "Devices", "ShowDevices" }, new[] { "Speakers", "EditSpeakerLabels" }, new[] { "Details", "EditDetails" }, new[] { "Bookmarks", "ReviewBookmarks" }, new[] { "Tasks", "ReviewTasks" } })
                        Dialog(hub, root, dialogPrefix + pair[0], () => typeof(NotesPane).GetMethod(pair[1], Flags).Invoke(hub.Notes, null));
                }
                hub.CloseToTray = false; hub.Close();
            }
            File.WriteAllText(Path.Combine(root, "findings.json"), new JavaScriptSerializer().Serialize(findings), Encoding.UTF8);
            var html = new StringBuilder("<!doctype html><meta charset='utf-8'><title>Notewll UI audit</title><style>body{font:15px system-ui;background:#17171c;color:#eee;margin:24px}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(360px,1fr));gap:20px}article{background:#24242c;padding:12px;border-radius:12px}img{width:100%;height:auto}a{color:#bba0e7}</style><h1>Notewll UI audit</h1><main>");
            foreach (string capture in captures) html.Append("<article><h3>" + capture + "</h3><a href='" + capture + ".png'><img loading='lazy' src='" + capture + ".png' alt='" + capture + "'></a></article>");
            File.WriteAllText(Path.Combine(root, "index.html"), html + "</main>", Encoding.UTF8);
            if (findings.Count != 0) throw new Exception("UI audit found " + findings.Count + " layout issues. See findings.json.");
            Console.Write("ui-audit|captures=" + captures.Count + "|findings=" + findings.Count);
        }
    }
}
