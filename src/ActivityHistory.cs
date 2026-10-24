using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace LocalDictation
{
    static class ActivityHistory
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr window, int message, IntPtr value, string text);
        internal static void SearchCue(TextBox box) { SendMessage(box.Handle, 0x1501, IntPtr.Zero, "Search history, project:name, tag:topic, person:name..."); }
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        public static string ForegroundApp(IntPtr window)
        {
            try { uint id; GetWindowThreadProcessId(window, out id); using (var process = Process.GetProcessById((int)id)) return process.ProcessName; }
            catch { return "Unknown app"; }
        }
        public static void Record(NoteStore store, string raw, string text, string platform, DateTime started, string windowTitle = null)
        {
            var note = new NoteDocument { Id = Guid.NewGuid().ToString("N"), CreatedUtc = started.ToString("o"), Kind = "Dictation", Mode = "Microphone", Platform = platform, TargetWindowTitle = windowTitle, Title = text.Length > 90 ? text.Substring(0, 90) + "…" : text, WrittenNotes = text, Status = "Complete", DurationSeconds = Math.Max(0, (DateTime.UtcNow - started).TotalSeconds) };
            note.Segments.Add(new NoteSegment { Source = "Microphone", Complete = true, RawText = raw, Text = text }); store.Save(note);
        }
        public static string Kind(NoteDocument note) { return note.Kind == "Dictation" ? "Dictation" : note.Kind == "Meeting" ? "Meeting" : "Note"; }
        public static string Text(NoteDocument note) { return note.Kind == "Dictation" || note.Kind == "Note" && note.EditableTextInitialized ? note.WrittenNotes ?? "" : (note.WrittenNotes + " " + String.Join(" ", note.Segments.Where(s => s.Complete && String.IsNullOrEmpty(s.Error)).Select(s => s.RawText ?? s.Text))).Trim(); }
        public static int Words(NoteDocument note) { return Regex.Matches(Text(note), @"[\p{L}\p{N}]+(?:['’-][\p{L}\p{N}]+)*").Count; }
        // Approximate sentence count: punctuation boundaries, ignoring decimal points; a final unpunctuated phrase counts once.
        internal static int SentenceCount(string text) { return Regex.Split(text ?? "", @"(?<!\d)[.!?]+|[.!?]+(?!\d)|[\r\n]+").Count(part => Regex.IsMatch(part, @"[\p{L}\p{N}]")); }
        public static DateTime LocalDate(NoteDocument note) { return DateTime.Parse(note.CreatedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind).ToLocalTime(); }
        internal static List<NoteDocument> Match(IEnumerable<NoteDocument> entries, string[] kinds, DateTime since, DateTime until, int limit)
        { return entries.Where(n => kinds.Contains(Kind(n)) && LocalDate(n) >= since && LocalDate(n) <= until).OrderByDescending(n => n.CreatedUtc).Take(limit).ToList(); }
    }
    sealed partial class SettingsForm
    {
        Surface homeActions;
        readonly NumericUpDown typingSpeed = new NumericUpDown { Minimum = 10, Maximum = 250, Value = 40, AccessibleName = "Your typing speed in words per minute" };
        readonly VoiceKpis voiceKpis = new VoiceKpis();
        Label speedLabel, analyticsNote, homeWelcome, homeWelcomeDetail;
        List<NoteDocument> activityEntries = new List<NoteDocument>();
        void BuildActivityHome(Panel page)
        {
            var stats = Card(page, 268, 332);
            Label(stats, "YOUR VOICE, IN NUMBERS", 22, 16, 350, 25, 10, true, Ink);
            speedLabel = Label(stats, "Your typing speed (WPM)", 22, 48, 190, 26, 9, false, Muted);
            speedLabel.Font = new Font("Segoe UI", 10, FontStyle.Italic);
            typingSpeed.Font = new Font("Segoe UI", 10, FontStyle.Italic);
            typingSpeed.SetBounds(220, 44, 82, 28); stats.Controls.Add(typingSpeed);
            typingSpeed.Value = Math.Max(10, Math.Min(250, source.TypingWordsPerMinute));
            voiceKpis.SetBounds(22, 88, 710, 220); stats.Controls.Add(voiceKpis);
            analyticsNote = Label(stats, "Estimated from dictations only \u00b7 speaking at 150 WPM \u00b7 excludes editing and processing time", 22, 310, 710, 20, 8, false, Muted);
            typingSpeed.ValueChanged += (sender, args) => {
                try { var savedConfig = Config.Load(); savedConfig.TypingWordsPerMinute = (int)typingSpeed.Value; savedConfig.Save(); source.TypingWordsPerMinute = (int)typingSpeed.Value; }
                catch (Exception ex) { MessageBox.Show(this, "The preview has updated, but your typing speed could not be saved: " + ex.Message, "Typing speed"); }
                RefreshKpis();
            };
            stats.Resize += (sender, args) => LayoutActivity();
        }
        void LayoutActivity()
        {
            if (voiceKpis.Parent == null || analyticsNote == null) return;
            int width = voiceKpis.Parent.ClientSize.Width, margin = (int)(22 * uiScale);
            typingSpeed.SetBounds((int)(220 * uiScale), (int)(44 * uiScale), (int)(82 * uiScale), (int)(28 * uiScale));
            speedLabel.SetBounds(margin, (int)(48 * uiScale), (int)(186 * uiScale), (int)(26 * uiScale));
            voiceKpis.SetBounds(margin, (int)(88 * uiScale), width - margin * 2, (int)(220 * uiScale));
            analyticsNote.Width = width - margin * 2;
            if (homeActions == null) return;
            var buttons = homeActions.Controls.OfType<HubButton>().ToArray(); int gap = (int)(12 * uiScale);
            int buttonWidth = (homeActions.ClientSize.Width - margin * 2 - gap * (buttons.Length - 1)) / buttons.Length;
            for (int i = 0; i < buttons.Length; i++) buttons[i].SetBounds(margin + i * (buttonWidth + gap), (int)(74 * uiScale), buttonWidth, (int)(38 * uiScale));
            homeShortcut.SetBounds(margin, (int)(40 * uiScale), homeActions.ClientSize.Width - margin * 2, (int)(26 * uiScale));
        }
        void RefreshActivity()
        {
            if (notes == null) return;
            activityEntries = notes.Store.List(); RefreshKpis();
        }
        void RefreshKpis()
        {
            voiceKpis.Words = activityEntries.Where(n => n.Kind == "Dictation").Sum(n => ActivityHistory.Words(n));
            voiceKpis.Sentences = activityEntries.Where(n => n.Kind == "Dictation").Sum(n => ActivityHistory.SentenceCount(ActivityHistory.Text(n)));
            voiceKpis.TypingWpm = (int)typingSpeed.Value;
            voiceKpis.Sessions = activityEntries.Count(n => n.Kind == "Dictation");
            voiceKpis.ActiveDays = activityEntries.Select(n => ActivityHistory.LocalDate(n).Date).Distinct().Count();
            voiceKpis.Days = Enumerable.Range(0, 7).Select(i => activityEntries.Count(n => ActivityHistory.LocalDate(n).Date == DateTime.Today.AddDays(i - 6))).ToArray();
            voiceKpis.AccessibleDescription = String.Format("{0} words dictated. Estimated time saved: {1}. Typing {2} WPM versus speaking 150 WPM. {3} active days.", voiceKpis.Words, VoiceKpis.Duration(VoiceKpis.SavedMinutes(voiceKpis.Words, voiceKpis.TypingWpm)), voiceKpis.TypingWpm, voiceKpis.ActiveDays);
            voiceKpis.AccessibleDescription += " " + voiceKpis.Sentences + " sentences dictated.";
            int dictatedWords = voiceKpis.Words;
            if (homeWelcome != null)
            {
                homeWelcome.Text = VoiceKpis.WelcomeHeadline(voiceKpis.Words, voiceKpis.TypingWpm);
                homeWelcomeDetail.Text = voiceKpis.Words == 0 ? "Dictate into your apps and see how much time your voice gives back." : VoiceKpis.SavedMinutes(voiceKpis.Words, voiceKpis.TypingWpm) > 0 ? "Welcome back. Estimated from saved dictations at " + voiceKpis.TypingWpm + " WPM typing and 150 WPM speaking." : "You've dictated " + dictatedWords.ToString("N0") + " words. Your selected typing speed gives no estimated time saving.";
            }
            voiceKpis.Invalidate();
        }
        void DeleteActivity(IEnumerable<NoteDocument> entries, bool confirmed = false)
        {
            var selected = entries.ToList(); if (selected.Count == 0) return;
            if (notes.Busy) { MessageBox.Show(this, "Finish the recording or transcription before deleting history."); return; }
            if (!confirmed && MessageBox.Show(this, "Move " + selected.Count + " selected item(s), including their text and recordings, to Recently deleted? Restore them in Preferences → Storage. Analytics will update.", "Delete history", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try { notes.PrepareHistoryDelete(); foreach (var note in selected) notes.Store.Delete(note); }
            catch (Exception ex) { MessageBox.Show(this, "Some items could not be deleted: " + ex.Message); }
            finally { notes.ReloadLibrary(); RefreshActivity(); }
        }
        void ClearActivity()
        {
            if (notes.Busy) { MessageBox.Show(this, "Finish the recording or transcription before clearing history."); return; }
            RefreshActivity();
            var selection = HistoryCleanupDialog.Choose(this, activityEntries);
            if (selection != null) DeleteActivity(selection, true);
        }
    }
}
