using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Notewll
{
    static class ActivityHistory
    {
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        public static string ForegroundApp(IntPtr window)
        {
            try { uint id; GetWindowThreadProcessId(window, out id); using (var process = Process.GetProcessById((int)id)) return process.ProcessName; }
            catch { return "Unknown app"; }
        }
        public static void Record(NoteStore store, string raw, string text, string platform, DateTime started, string windowTitle = null, double? captureSeconds = null, double? processingSeconds = null)
        {
            var note = new NoteDocument { Id = Guid.NewGuid().ToString("N"), CreatedUtc = started.ToString("o"), Kind = "Dictation", Mode = "Microphone", Platform = platform, TargetWindowTitle = windowTitle, Title = text.Length > 90 ? text.Substring(0, 90) + "…" : text, WrittenNotes = text, Status = "Complete", DurationSeconds = Math.Max(0, (DateTime.UtcNow - started).TotalSeconds) };
            note.CaptureSeconds = captureSeconds; note.ProcessingSeconds = processingSeconds;
            if (captureSeconds.HasValue) note.DurationSeconds = captureSeconds.Value;
            note.Segments.Add(new NoteSegment { Source = "Microphone", Complete = true, RawText = raw, Text = text }); store.Save(note);
        }
        public static string Kind(NoteDocument note) { return note.Kind == "Dictation" ? "Dictation" : note.Kind == "Meeting" ? "Meeting" : note.Kind == "Conversation" ? "Conversation" : "Note"; }
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
        Surface homeActions, activityCard, habitCard;
        int ActivityTop { get { return 200; } }
        int HomeEditorTop { get { return ActivityTop + 332 + 16 + 248 + 16; } }
        readonly NumericUpDown typingSpeed = new WheelNumber { Minimum = 10, Maximum = 250, Value = 40, AccessibleName = "Your typing speed in words per minute" };
        readonly VoiceKpis voiceKpis = new VoiceKpis();
        Label speedLabel, analyticsNote, homeWelcome, homeWelcomeDetail;
        List<NoteDocument> activityEntries = new List<NoteDocument>();
        void BuildActivityHome(Panel page)
        {
            var stats = Card(page, ActivityTop, 332); activityCard = stats;
            Label(stats, "YOUR VOICE, IN NUMBERS", 22, 12, 350, 25, 10, true, Ink);
            speedLabel = Label(stats, "Your typing speed (WPM)", 22, 48, 190, 26, 9, false, Muted);
            speedLabel.Font = new Font("Segoe UI", 10, FontStyle.Italic);
            typingSpeed.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            typingSpeed.AccessibleDescription = "Click the number, then scroll to change WPM. You can also type a value or use arrow keys.";
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
        void PositionHomeSections()
        {
            if (activityCard == null) return;
            activityCard.Tag = new Rectangle(40, ActivityTop, 760, 332);
            if (habitCard != null) habitCard.Tag = new Rectangle(40, ActivityTop + 348, 760, 248);
            if (homePractice != null) homePractice.Tag = new Rectangle(40, HomeEditorTop, 760, practiceExpanded ? 688 : 76);
            LayoutPage(pages[0]);
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
            var create = homeActions.Controls.OfType<HubButton>().Single();
            int createWidth = (int)(132 * uiScale), createHeight = (int)(38 * uiScale);
            create.SetBounds(homeActions.ClientSize.Width - margin - createWidth, (homeActions.ClientSize.Height - createHeight) / 2, createWidth, createHeight);
            var heading = homeActions.Controls.OfType<Label>().Single();
            heading.SetBounds(margin, (homeActions.ClientSize.Height - (int)(28 * uiScale)) / 2, Math.Max(1, create.Left - margin * 2), (int)(28 * uiScale));

        }
        void RefreshActivity()
        {
            if (notes == null) return;
            activityEntries = buildingHub ? notes.HistorySnapshot : notes.Store.List();
            PositionHomeSections(); RefreshKpis();
        }
        void RefreshKpis()
        {
            var dictations = activityEntries.Where(n => n.Kind == "Dictation").ToArray();
            voiceKpis.Words = dictations.Sum(n => ActivityHistory.Words(n));
            voiceKpis.Sentences = dictations.Sum(n => ActivityHistory.SentenceCount(ActivityHistory.Text(n)));
            voiceKpis.TypingWpm = (int)typingSpeed.Value;
            voiceKpis.Sessions = dictations.Length;
            var measured = dictations.Where(n => n.CaptureSeconds > 0).ToArray(); double captured = measured.Sum(n => n.CaptureSeconds.Value);
            voiceKpis.MeasuredWpm = captured > 0 ? measured.Sum(n => ActivityHistory.Words(n)) * 60 / captured : 0;
            if (measured.Length > 0) analyticsNote.Text = "Savings assume 150 WPM; measured speech " + voiceKpis.MeasuredWpm.ToString("0") + " WPM; processing " + measured.Average(n => n.ProcessingSeconds ?? 0).ToString("0.0") + " sec/session. Editing excluded.";
            voiceKpis.ActiveDays = activityEntries.Select(n => ActivityHistory.LocalDate(n).Date).Distinct().Count();
            voiceKpis.Days = Enumerable.Range(0, 7).Select(i => activityEntries.Count(n => ActivityHistory.LocalDate(n).Date == DateTime.Today.AddDays(i - 6))).ToArray();
            voiceKpis.DailySavedMinutes = Enumerable.Range(0, 7).Select(i => VoiceKpis.SavedMinutes(dictations.Where(n => ActivityHistory.LocalDate(n).Date == DateTime.Today.AddDays(i - 6)).Sum(n => ActivityHistory.Words(n)), voiceKpis.TypingWpm)).ToArray();
            voiceKpis.AccessibleDescription = String.Format("{0} words dictated. Estimated time saved: {1}. Typing {2} WPM versus speaking 150 WPM. {3} active days.", voiceKpis.Words, VoiceKpis.Duration(VoiceKpis.SavedMinutes(voiceKpis.Words, voiceKpis.TypingWpm)), voiceKpis.TypingWpm, voiceKpis.ActiveDays);
            voiceKpis.AccessibleDescription += " " + voiceKpis.Sentences + " sentences dictated.";
            voiceKpis.AccessibleDescription += String.Format(" Estimated typing time {0}; speaking time {1}. Active on {2} of the last 7 days.", VoiceKpis.Duration((double)voiceKpis.Words / voiceKpis.TypingWpm), VoiceKpis.Duration((double)voiceKpis.Words / VoiceKpis.SpeakingWpm), voiceKpis.Days.Count(day => day > 0));
            voiceKpis.AccessibleDescription += " Estimated time saved in the last 7 days: " + VoiceKpis.Duration(voiceKpis.DailySavedMinutes.Sum()) + ".";
            int dictatedWords = voiceKpis.Words;
            if (homeWelcome != null)
            {
                homeWelcome.Text = VoiceKpis.WelcomeHeadline(voiceKpis.Words, voiceKpis.TypingWpm);
                homeWelcomeDetail.Text = voiceKpis.Words == 0 ? "Dictate into your apps and see how much time your voice gives back." : VoiceKpis.SavedMinutes(voiceKpis.Words, voiceKpis.TypingWpm) > 0 ? "Welcome back. Estimated from saved dictations at " + voiceKpis.TypingWpm + " WPM typing and 150 WPM speaking." : "You've dictated " + dictatedWords.ToString("N0") + " words. Your selected typing speed gives no estimated time saving.";
            }
            RefreshSpeakingProgress(); voiceKpis.Invalidate();
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
