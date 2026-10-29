using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Drawing;
namespace Notewll
{
    sealed class PlannedMeeting
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string StartUtc { get; set; }
        public string Url { get; set; }
        public bool Cancelled { get; set; }
    }
    static class MeetingCalendar
    {
        static string FilePath { get { return Path.Combine(Config.DirectoryPath, "calendar.json"); } }
        public static DateTime Start(PlannedMeeting m) { return DateTime.Parse(m.StartUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime(); }
        static string Unescape(string value) { return Regex.Replace(value, @"\\([nN,;\\])", m => m.Groups[1].Value.Equals("n", StringComparison.OrdinalIgnoreCase) ? "\n" : m.Groups[1].Value); }
        public static List<PlannedMeeting> Parse(string content)
        {
            if (content.Length > 2 * 1024 * 1024 || !content.Contains("BEGIN:VCALENDAR")) throw new InvalidDataException("Choose an iCalendar file up to 2 MB.");
            var result = new List<PlannedMeeting>(); Dictionary<string, string[]> item = null; int nested = 0;
            foreach (var raw in Regex.Replace(content, @"\r?\n[ \t]", "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (raw.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase)) { if (item != null) throw new InvalidDataException("Nested calendar event."); item = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase); continue; }
                if (item == null) continue;
                if (raw.StartsWith("BEGIN:", StringComparison.OrdinalIgnoreCase)) { nested++; continue; }
                if (raw.StartsWith("END:", StringComparison.OrdinalIgnoreCase) && nested > 0) { nested--; continue; }
                if (raw.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
                {
                    if (!item.ContainsKey("UID") || !item.ContainsKey("DTSTART")) throw new InvalidDataException("Each event needs a UID and start time.");
                    if (item.ContainsKey("RRULE") || item.ContainsKey("RDATE") || item.ContainsKey("RECURRENCE-ID")) throw new InvalidDataException("Import individual meeting occurrences; recurring series are not supported by local calendar import.");
                    var date = item["DTSTART"]; DateTime local;
                    bool utc = date[1].EndsWith("Z", StringComparison.Ordinal);
                    if (!DateTime.TryParseExact(date[1], utc ? "yyyyMMdd'T'HHmmss'Z'" : "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out local)) throw new InvalidDataException("Import timed meetings; all-day events are not meeting reminders.");
                    DateTime start;
                    if (utc) start = DateTime.SpecifyKind(local, DateTimeKind.Utc);
                    else
                    {
                        var tz = Regex.Match(date[0], "TZID=\"?([^;\"]+)", RegexOptions.IgnoreCase); var zone = TimeZoneInfo.Local;
                        if (tz.Success)
                        {
                            string id = tz.Groups[1].Value;
                            var aliases = new Dictionary<string, string> { { "America/New_York", "Eastern Standard Time" }, { "America/Chicago", "Central Standard Time" }, { "America/Denver", "Mountain Standard Time" }, { "America/Los_Angeles", "Pacific Standard Time" }, { "Europe/London", "GMT Standard Time" }, { "Asia/Kolkata", "India Standard Time" }, { "Etc/UTC", "UTC" } };
                            zone = TimeZoneInfo.FindSystemTimeZoneById(aliases.ContainsKey(id) ? aliases[id] : id);
                        }
                        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local)) throw new InvalidDataException("Export this meeting in UTC; its local time is ambiguous or invalid.");
                        start = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone);
                    }
                    string url = item.ContainsKey("URL") ? item["URL"][1] : ""; Uri link;
                    if (url.Length > 0 && (!Uri.TryCreate(url, UriKind.Absolute, out link) || (link.Scheme != "https" && link.Scheme != "http"))) throw new InvalidDataException("Meeting links must use http or https.");
                    var meeting = new PlannedMeeting { Id = item["UID"][1], Title = Unescape(item.ContainsKey("SUMMARY") ? item["SUMMARY"][1] : "Meeting"), StartUtc = start.ToString("o"), Url = url, Cancelled = item.ContainsKey("STATUS") && item["STATUS"][1].Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) };
                    if (meeting.Id.Length > 500 || meeting.Title.Length > 500 || url.Length > 2000) throw new InvalidDataException("Calendar fields are too long.");
                    result.Add(meeting); if (result.Count > 200) throw new InvalidDataException("Import at most 200 individual meetings."); item = null; continue;
                }
                if (nested > 0) continue;
                int colon = raw.IndexOf(':'); if (colon < 1) throw new InvalidDataException("Invalid calendar property.");
                string header = raw.Substring(0, colon), key = header.Split(';')[0];
                if (item.ContainsKey(key) && new[] { "UID", "DTSTART", "SUMMARY", "URL" }.Contains(key.ToUpperInvariant())) throw new InvalidDataException("Duplicate calendar field.");
                item[key] = new[] { header, raw.Substring(colon + 1) };
            }
            if (item != null || result.Count == 0 || result.Select(m => m.Id).Distinct().Count() != result.Count) throw new InvalidDataException("The calendar is incomplete, empty or contains duplicate events.");
            return result;
        }
        public static List<PlannedMeeting> Load()
        {
            if (!File.Exists(FilePath)) return new List<PlannedMeeting>();
            if (new FileInfo(FilePath).Length > 2 * 1024 * 1024) throw new InvalidDataException("Calendar file is too large.");
            var items = new JavaScriptSerializer().Deserialize<List<PlannedMeeting>>(File.ReadAllText(FilePath));
            if (items == null || items.Count > 200) throw new InvalidDataException("Invalid saved calendar.");
            foreach (var item in items) { if (item == null || String.IsNullOrEmpty(item.Id) || item.Title == null) throw new InvalidDataException("Invalid saved meeting."); Start(item); }
            return items;
        }
        public static void Import(string path)
        {
            if (new FileInfo(path).Length > 2 * 1024 * 1024) throw new InvalidDataException("Calendar import limit is 2 MB.");
            var incoming = Parse(File.ReadAllText(path)); var ids = new HashSet<string>(incoming.Select(m => m.Id));
            var result = Load().Where(m => !ids.Contains(m.Id) && Start(m) >= DateTime.UtcNow.AddDays(-1)).Concat(incoming.Where(m => !m.Cancelled)).ToList();
            if (result.Count > 200) throw new InvalidDataException("Keep at most 200 meetings.");
            Directory.CreateDirectory(Config.DirectoryPath); string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(result)); if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null); else File.Move(temporary, FilePath); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
    sealed partial class SettingsForm
    {
        readonly CheckBox meetingReminders = new ThemedCheckBox { Text = "Remind me about imported meetings while Notewll runs", AutoSize = true };
        void BuildCalendarControls(Panel page)
        {
            meetingReminders.Checked = source.MeetingReminders; meetingReminders.CheckedChanged += (s, e) => MarkDirty(); Setting(page, meetingReminders, 490);
            Button(page, "Calendar & upcoming meetings", 20, 538, 330, (s, e) => ShowMeetingCalendar());
        }
        void ShowMeetingCalendar()
        {
            using (var dialog = DialogUi.Create("Upcoming meetings", new Size(720, 480)))
            {
                var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, AccessibleName = "Imported meetings" }; list.Columns.Add("Local start time", 200); list.Columns.Add("Meeting", 400);
                var info = DialogUi.Text("Import individual timed meetings from an .ics file. This is a local copy; reimport updates. Calendar accounts are not connected. Recording starts only when you choose it."); info.Dock = DockStyle.Top; info.AutoSize = false; info.Height = 80;
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
                Action refresh = () => { list.Items.Clear(); foreach (var meeting in MeetingCalendar.Load().Where(m => MeetingCalendar.Start(m) >= DateTime.UtcNow.AddHours(-1)).OrderBy(MeetingCalendar.Start)) { var row = new ListViewItem(MeetingCalendar.Start(meeting).ToLocalTime().ToString("g")) { Tag = meeting }; row.SubItems.Add(meeting.Title); list.Items.Add(row); } };
                buttons.Controls.Add(DialogUi.Button("Import calendar...", (s, e) => { using (var file = new OpenFileDialog { Filter = "iCalendar|*.ics" }) if (file.ShowDialog(dialog) == DialogResult.OK) try { MeetingCalendar.Import(file.FileName); refresh(); } catch (Exception ex) { MessageBox.Show(dialog, ex.Message, "Calendar not imported"); } }));
                buttons.Controls.Add(DialogUi.Button("Record selected", (s, e) => { if (list.SelectedItems.Count == 0) return; if (notes.Busy || IsTesting) { MessageBox.Show(dialog, "Finish your current recording first."); return; } var meeting = (PlannedMeeting)list.SelectedItems[0].Tag; dialog.Close(); ShowPage("Notes"); notes.RecordPlannedMeeting(meeting.Title); }, true));
                buttons.Controls.Add(DialogUi.Button("Close", (s, e) => dialog.Close())); dialog.Controls.Add(list); dialog.Controls.Add(info); dialog.Controls.Add(buttons); ThemeColors.Apply(dialog, source.Theme);
                try { refresh(); } catch (Exception ex) { info.Text = ex.Message; } dialog.ShowDialog(this);
            }
        }
    }
}
