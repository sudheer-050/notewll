using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Windows.Forms;

namespace Notewll
{
    static class ThemeVerification
    {
        public static void Run(string root, bool preview)
        {
            Directory.CreateDirectory(root); Config.DiagnosticDataRoot(Path.Combine(root, "data")); Application.EnableVisualStyles();
            string installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "LocalDictation");
            if (preview && File.Exists(Path.Combine(installed, "native", "libvosk.dll")) && File.Exists(Path.Combine(installed, "model-small-en-us", "am", "final.mdl"))) SpeechEngine.DiagnosticRoot(installed);
            var config = new Config(); config.Personalization.Add(new PersonalRule { Kind = "Word", Trigger = "Notewll", Replacement = "Notewll" }); config.Personalization.Add(new PersonalRule { Kind = "Snippet", Trigger = "my sign off", Replacement = "Thanks for your time. Talk soon!" }); config.Save(); string lastSaved = config.Theme;
            var store = new NoteStore(Path.Combine(root, "notes"));
            var note = store.Create("A bright idea", "Microphone", ""); note.WrittenNotes = "Build something useful, and leave room for curiosity."; store.Save(note);
            note.Segments.Add(new NoteSegment { Complete = true, RawText = "um keep the original words", Text = "Keep the original words.", Source = "Microphone" }); store.Save(note);
            var version = typeof(NotesPane).GetMethod("ModifiedVersion", BindingFlags.NonPublic | BindingFlags.Static);
            if ((string)version.Invoke(null, new object[] { note, "" }) != "Keep the original words.") throw new Exception("A newly recorded note has no modified text.");
            note.EditableTextInitialized = true;
            if ((string)version.Invoke(null, new object[] { note, "" }) != "") throw new Exception("An intentional empty edit was replaced.");
            var distinct = new HashSet<int>();
            using (var form = new SettingsForm(config, "", "", store))
            {
                var choice = (ComboBox)typeof(SettingsForm).GetField("themeChoice", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
                var grid = (TableLayoutPanel)typeof(SettingsForm).GetField("themePreviews", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
                var cards = grid.Controls.OfType<ThemeCard>().ToArray();
                if (cards.Length != ThemeColors.Names.Length) throw new Exception("A theme preview card is missing.");
                form.OnSaved = value => value.Save();
                form.Show(); if (preview) form.Size = form.MinimumSize; Application.DoEvents();
                foreach (string name in ThemeColors.Names)
                {
                    form.ShowPage("Preferences"); form.SelectSettingsCategory("App"); Application.DoEvents();
                    cards.Single(card => card.ThemeName == name).PerformClick();
                    var wpm = (NumericUpDown)typeof(SettingsForm).GetField("typingSpeed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
                    if (!(wpm is WheelNumber) || wpm.BackColor != ThemeColors.Card || wpm.ForeColor != ThemeColors.Ink || wpm.BorderStyle != BorderStyle.None || wpm.Controls.Cast<Control>().Any(child => !(child is TextBox) && child.Visible)) throw new Exception("WPM control has native chrome or mismatched theme colors.");
                    if (ThemeColors.Mode != name || Config.Load().Theme != lastSaved) throw new Exception("Theme preview wrote preferences or was not applied.");
                    if (cards.Count(card => card.Selected) != 1 || !cards.Single(card => card.ThemeName == name).Selected) throw new Exception("Theme selection checkmark is incorrect.");
                    foreach (var card in cards) using (var image = new Bitmap(card.Width, card.Height)) card.DrawToBitmap(image, new Rectangle(Point.Empty, card.Size));
                    if (ThemeColors.Mode != name) throw new Exception("Drawing a theme card changed the selected application theme.");
                    if (!SystemInformation.HighContrast)
                    {
                        CheckContrast(name, ThemeColors.Ink, ThemeColors.Paper); CheckContrast(name, ThemeColors.Ink, ThemeColors.Card);
                        CheckContrast(name, ThemeColors.Muted, ThemeColors.Paper); CheckContrast(name, ThemeColors.Muted, ThemeColors.Card);
                        CheckContrast(name, ThemeColors.Ink, ThemeColors.Selected); CheckContrast(name, ThemeColors.AccentInk, ThemeColors.Accent);
                        CheckContrast(name, ThemeColors.Accent, ThemeColors.Paper);
                        if (name != "System" && !distinct.Add(ThemeColors.Paper.ToArgb())) throw new Exception("Themes use the same background.");
                    }
                    form.ShowPage("Home"); Application.DoEvents(); if (!wpm.Visible) throw new Exception("Home analytics must always be visible."); Application.DoEvents(); wpm.Focus(); decimal beforeWheel = wpm.Value;
                    typeof(WheelNumber).GetMethod("OnMouseWheel", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(wpm, new object[] { new HandledMouseEventArgs(MouseButtons.None, 0, 4, 4, 120) });
                    if (wpm.Value != beforeWheel + 1) throw new Exception("WPM wheel did not advance one step.");
                    wpm.Value = beforeWheel;
                    using (var numberImage = new Bitmap(wpm.Width, wpm.Height))
                    {
                        wpm.DrawToBitmap(numberImage, wpm.ClientRectangle);
                        if (numberImage.GetPixel(wpm.Width - 3, wpm.Height / 2).ToArgb() != ThemeColors.Card.ToArgb()) throw new Exception("The removed spinner leaves a painted box.");
                    }
                    form.SavePreferences(); lastSaved = name;
                    if (Config.Load().Theme != name) throw new Exception("Selected theme was lost on save/reload.");
                    if (preview)
                    {
                        form.ShowPage("Home"); Capture(form, Path.Combine(root, name + "-home.png"));
                        form.ShowPage("Notes"); form.Notes.SelectNote(note.Id); Capture(form, Path.Combine(root, name + "-notes.png"));
                        var pane = form.Notes;
                        typeof(NotesPane).GetField("reviewingConversation", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(pane, true);
                        typeof(NotesPane).GetMethod("LayoutNoteEditors", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(pane, null);
                        var textTabs = (AppTabs)typeof(NotesPane).GetField("tabs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pane);
                        var modified = (TextBox)typeof(NotesPane).GetField("modifiedText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pane);
                        var originalText = (RichTextBox)typeof(NotesPane).GetField("transcript", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pane);
                        if (!originalText.ReadOnly || !originalText.Text.Contains("um keep the original words")) throw new Exception("Original text is missing.");
                        textTabs.SelectedIndex = 1; Capture(form, Path.Combine(root, name + "-original.png"));
                        textTabs.SelectedTab = (TabPage)modified.Parent; Application.DoEvents();
                        if (!modified.ReadOnly || modified.Text != note.WrittenNotes || note.Segments[0].RawText != "um keep the original words") throw new Exception("Text versions lost original or modified content.");
                        Capture(form, Path.Combine(root, name + "-modified.png"));
                        foreach (string library in new[] { "Dictionary", "Snippets" }) { form.ShowPage(library); Capture(form, Path.Combine(root, name + "-" + library + ".png")); }
                        form.ShowPage("Preferences"); form.SelectSettingsCategory("Recording"); Capture(form, Path.Combine(root, name + "-recording.png"));
                        form.ShowPage("Preferences"); form.SelectSettingsCategory("Writing"); Capture(form, Path.Combine(root, name + "-writing.png"));
                        form.ShowPage("Preferences"); form.SelectSettingsCategory("App"); Capture(form, Path.Combine(root, name + "-settings.png"));
                        using (var picker = new Bitmap(grid.Width, grid.Height)) { grid.DrawToBitmap(picker, new Rectangle(Point.Empty, grid.Size)); picker.Save(Path.Combine(root, name + "-picker.png")); }
                        using (var enrollment = new VoiceEnrollmentDialog(new Config { Theme = name }, true)) { enrollment.Show(); Application.DoEvents(); Capture(enrollment, Path.Combine(root, name + "-voice.png")); enrollment.Close(); }
                        ThemeColors.Apply(form, name);
                    }
                }
                form.CloseToTray = false; form.Close();
            }
            ThemeColors.Mode = "System";
            using (var reopened = new SettingsForm(Config.Load(), "", "", store))
            {
                var choice = (ComboBox)typeof(SettingsForm).GetField("themeChoice", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(reopened);
                if (Convert.ToString(choice.SelectedItem) != lastSaved || ThemeColors.Mode != lastSaved) throw new Exception("Reopening the app lost the selected theme.");
            }
            var unknown = new Config { Theme = "old-unknown-palette" }; unknown.Validate(); if (unknown.Theme != "System") throw new Exception("Unknown theme did not fall back to System.");
            ThemeColors.Mode = "System";
            if (preview)
            {
                string html = "<!doctype html><meta charset='utf-8'><title>Notewll themes</title><style>body{font:16px system-ui;background:#eee;padding:24px}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(360px,1fr));gap:24px}article{background:white;padding:16px;border-radius:12px}img{width:100%;height:auto}</style><h1>Notewll themes</h1><p>Application renders. Ball appearance is set separately.</p><main>";
                foreach (string name in ThemeColors.Names) html += "<article><h2>" + name + "</h2><a href='" + name + "-home.png'><img src='" + name + "-home.png' alt='" + name + " theme'></a><p><a href='" + name + "-settings.png'>Settings</a> · <a href='" + name + "-notes.png'>Notes</a> · <a href='" + name + "-voice.png'>Voice setup</a></p></article>";
                File.WriteAllText(Path.Combine(root, "index.html"), html + "</main>");
            }
            Console.Write("themes-ok|choices=8|preview-without-saving|save-reload|readable-colors|legacy-fallback");
        }
        static double Luminance(Color color) { Func<byte, double> linear = value => value / 255.0 <= .04045 ? value / 255.0 / 12.92 : Math.Pow((value / 255.0 + .055) / 1.055, 2.4); return .2126 * linear(color.R) + .7152 * linear(color.G) + .0722 * linear(color.B); }
        static void CheckContrast(string name, Color foreground, Color background) { double a = Luminance(foreground), b = Luminance(background); if ((Math.Max(a, b) + .05) / (Math.Min(a, b) + .05) < 4.5) throw new Exception(name + " has insufficient text contrast: " + foreground + " on " + background); }
        static void Capture(Form form, string path) { Application.DoEvents(); form.Refresh(); using (var image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(path); } }
    }
}
