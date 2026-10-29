using System;
using System.IO;
using System.Windows.Forms;

namespace Notewll
{
    static class RecordingShortcutVerification
    {
        public static void Run(string root)
        {
            Directory.CreateDirectory(root); Config.DiagnosticDataRoot(Path.Combine(root, "data")); Application.EnableVisualStyles();
            using (var form = new SettingsForm(new Config { Hotkey = "Next+Down" }, "", "", new NoteStore(Path.Combine(root, "notes"))))
            {
                var fields = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                foreach (string field in new[] { "hotkey", "noteShortcut", "meetingShortcut" })
                {
                    var button = (HubButton)typeof(SettingsForm).GetField(field, fields).GetValue(form);
                    if (!button.Text.StartsWith("Change shortcut: ")) throw new Exception("Shortcut control is missing its action label.");
                }
                Config chosen = null; form.OnSaved = value => { chosen = value; value.Save(); };
                Action<string, int[]> capture = (action, keys) => { form.BeginShortcutCapture(action); foreach (int key in keys) form.CaptureShortcutKey(key, true); form.CaptureShortcutKey(keys[keys.Length - 1], false); };
                capture("Note", new[] { 17, 18, 78 }); capture("Meeting", new[] { 17, 18, 77 }); form.SavePreferences();
                if (chosen == null || chosen.Hotkey != "Next+Down" || chosen.NotesHotkey != "Ctrl+Alt+N" || chosen.MeetingHotkey != "Ctrl+Alt+M") throw new Exception("Recording shortcuts overwrote one another.");
                capture("Dictate", new[] { 121 }); form.SavePreferences();
                foreach (string action in new[] { "Dictate", "Note", "Meeting" }) { form.BeginShortcutCapture(action); form.CaptureShortcutKey(27, true); }
                form.SavePreferences();
                var saved = Config.Load();
                if (saved.Hotkey != "F10" || saved.NotesHotkey != "Ctrl+Alt+N" || saved.MeetingHotkey != "Ctrl+Alt+M" || form.IsCapturingShortcut) throw new Exception("Recording shortcuts did not persist or cancel correctly.");
                foreach (bool duplicateNote in new[] { false, true })
                {
                    var invalid = saved.Copy(); invalid.MeetingHotkey = duplicateNote ? invalid.NotesHotkey : invalid.Hotkey;
                    try { invalid.Validate(); throw new Exception("Duplicate recording shortcut was accepted."); } catch (InvalidDataException) { }
                }
                form.CloseToTray = false;
            }
            Console.Write("recording-shortcuts-ok|three-actions|preserved-existing|capture|cancel|save-reload|duplicates-rejected");
        }
    }
}
