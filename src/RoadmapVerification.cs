using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
namespace Notewll
{
    static class RoadmapVerification
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        public static void Run(string root)
        {
            Directory.CreateDirectory(root); Config.DiagnosticDataRoot(Path.Combine(root, "data"));
            var store = new NoteStore(Path.Combine(root, "notes")); var note = store.Create("Release meeting", "Both", "Alex"); note.Status = "Saved"; note.Project = "Apollo release"; note.Tags = "budget, launch"; note.WrittenNotes = "Private annotation: keep secret";
            note.Segments.Add(new NoteSegment { Source = "Computer", StartFrame = 16000, Frames = 16000, Complete = true, RawText = "Alex will send the report by Friday.", Text = "Edited display" });
            note.Segments.Add(new NoteSegment { Source = "Computer", StartFrame = 48000, Frames = 16000, Complete = true, RawText = "The report covers launch readiness and our budget." });
            note.Segments.Add(new NoteSegment { Source = "Microphone", StartFrame = 80000, Frames = 16000, Complete = true, RawText = "We agreed the delivery must arrive next week." });
            note.Bookmarks.Add(new NoteBookmark { Kind = "Important", Seconds = 1.5 });
            var suggestions = ConversationTools.Suggest(note); Check(suggestions.Count == 2 && suggestions[0].Owner == "Alex" && !suggestions[0].Confirmed, "Task suggestions auto-confirmed or lost owner");
            note.Tasks.AddRange(suggestions); note.Tasks[0].Confirmed = true; note.Tasks[0].Owner = "Sam"; store.Save(note);
            var saved = store.List().Single(); Check(saved.Tasks[0].Owner == "Sam" && saved.Tasks[0].Confirmed && saved.Bookmarks[0].Seconds == 1.5, "Annotations did not persist");
            Check(saved.Segments[0].RawText == "Alex will send the report by Friday.", "Raw transcript changed");
            Check(ConversationTools.Matches(saved, "project:\"Apollo release\" tag:budget person:Alex", ""), "Filters lost metadata"); Check(!ConversationTools.Matches(saved, "tag:missing", ""), "Filter ignored");
            Check(ConversationTools.Highlights(saved).All(h => h.Text == saved.Segments[h.Segment].RawText), "Highlights invented speech");
            Check(ConversationTools.Suggest(saved).Count == 0, "Repeated suggestions");
            Check(!DictationApp.HandsFreeAllowed("", "notepad") && DictationApp.HandsFreeAllowed("chrome.exe, Notepad", "notepad") && !DictationApp.HandsFreeAllowed("notepad", "evilnotepad"), "App allowlist failed");
            string snapshot = Path.Combine(root, "backup.nwb"); LocalSnapshots.Save(store, snapshot);
            var target = new NoteStore(Path.Combine(root, "restored"));
            int count = LocalSnapshots.Read(snapshot, plain => NotesData.Restore(target, plain, true)); Check(count == 1 && target.List().Count == 0, "Restore preview mutated notes");
            count = LocalSnapshots.Read(snapshot, plain => NotesData.Restore(target, plain)); Check(count == 1 && target.List().Single().Tasks[0].Owner == "Sam", "Restore lost annotations");
            Check(LocalSnapshots.Read(snapshot, plain => NotesData.Restore(target, plain)) == 0, "Restore overwrote existing IDs");
            byte[] bad = File.ReadAllBytes(snapshot); bad[bad.Length - 33] ^= 1; string damaged = Path.Combine(root, "damaged.nwb"); File.WriteAllBytes(damaged, bad); bool rejected = false; try { LocalSnapshots.Read(damaged, plain => NotesData.Restore(target, plain)); } catch (IOException) { rejected = true; } Check(rejected, "Tampered snapshot was accepted");
            var scoped = NoteExport.Scope(saved, 0, false); string preview = NoteExport.PreviewText(scoped); Check(!preview.Contains("keep secret") && !preview.Contains("Edited display"), "Private notes leaked or source words lost");
            NoteExport.SavePreview(preview.Replace("Alex", "[REDACTED]"), Path.Combine(root, "redacted.pdf")); Check(saved.Segments[0].RawText.Contains("Alex"), "Export mutated original");
            Application.EnableVisualStyles(); using (var form = new Form { ClientSize = new Size(1050, 760) }) using (var pane = new NotesPane(new Config(), store))
            {
                pane.Dock = DockStyle.Fill; form.Controls.Add(pane); form.Show(); pane.SelectNote(note.Id); pane.ShowRoadmapPage(); Application.DoEvents(); using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, form.ClientRectangle); bitmap.Save(Path.Combine(root, "find-and-act.png")); } form.Close();
            }
            Console.Write("roadmap-ok|tasks|bookmarks|filters|highlights|allowlist|encrypted-roundtrip|preview|tamper|private-export");
        }
    }
}
