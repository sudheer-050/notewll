using System;
using System.Collections.Generic;
namespace Notewll
{
    static class ProductVerification
    {
        public static void Run(string root = null)
        {
            var c = new Config { Personalization = new List<PersonalRule> {
                new PersonalRule { Kind = "Word", Trigger = "alpha", Replacement = "beta" },
                new PersonalRule { Kind = "Snippet", Trigger = "beta", Replacement = "saved text" },
                new PersonalRule { Kind = "Snippet", Trigger = "alpha beta", Replacement = "longest" }
            }};
            if (PersonalRule.Apply(c, "alpha, beta; alpha beta") != "beta, saved text; longest") throw new Exception("Replacements cascade or longest match loses.");
            if (PersonalRule.Apply(c, "alpha_id alphabet") != "alpha_id alphabet") throw new Exception("Replacements corrupt identifiers.");
            if (PersonalRule.Apply(c, "ALPHA", true) != "beta") throw new Exception("Word matching ignores case or applies snippets.");
            c.CleanupEnabled = false;
            if (WritingFormat.Compose(c, "alpha") != "alpha") throw new Exception("Raw text was modified.");
            var imported = new PersonalRule { Kind = "Word", Trigger = " gamma ", Replacement = "Gamma" };
            var merged = RuleLibrary.Merge(c.Personalization, new[] { imported }, "Word");
            if (merged.Count != 4 || merged[3].Origin != "Imported" || imported.Trigger != " gamma " || c.Personalization.Count != 3) throw new Exception("Import mutates source entries.");
            bool rejected = false;
            try { RuleLibrary.Merge(c.Personalization, new[] { new PersonalRule { Kind = "Word", Trigger = "ALPHA", Replacement = "overwrite" } }, "Word"); } catch (ArgumentException) { rejected = true; }
            if (!rejected || c.Personalization[0].Replacement != "beta") throw new Exception("Duplicate import overwrites an existing entry.");
            c.CleanupEnabled = true; c.Personalization.Clear();
            if (WritingFormat.Compose(c, "numbered list one apples two pears three lemons four dates") != "1. Apples" + Environment.NewLine + "2. Pears" + Environment.NewLine + "3. Lemons" + Environment.NewLine + "4. Dates") throw new Exception("Explicit lists lose items.");
            if (WritingFormat.Compose(c, "bullet list apples next item pears") != "\u2022 Apples" + Environment.NewLine + "\u2022 Pears") throw new Exception("Bullet lists fail.");
            c.AppStylesEnabled = true;
            if (WritingProfiles.Resolve(c, "OUTLOOK").WritingStyle != "Email" || WritingProfiles.Resolve(c, "unknown").WritingStyle != c.WritingStyle || c.WritingStyle == "Email") throw new Exception("App styles mutate defaults or ignore the target.");
            c.CleanupLevel = "Light";
            if (!WritingFormat.Compose(c, "I just wanted to I just want to leave").Contains("wanted")) throw new Exception("Light cleanup removes a restart.");
            c.SpeechLanguage = "te";
            if (WritingFormat.Compose(c, "నేను 15 వద్ద కాదు") != "నేను 15 వద్ద కాదు") throw new Exception("English cleanup modifies another language.");
            c.SpeechLanguage = "en";
            if (LocalCommands.Apply("Send 15 to Alex on Tuesday.", "replace Tuesday with Wednesday") != "Send 15 to Alex on Wednesday.") throw new Exception("Explicit replacement changes unrelated facts.");
            if (LocalCommands.Apply("Keep 15.50. Remove this.", "remove last sentence") != "Keep 15.50.") throw new Exception("Sentence editing mistakes decimal punctuation.");
            bool commandRejected = false; try { LocalCommands.Apply("Keep this.", "make up a summary"); } catch (ArgumentException) { commandRejected = true; }
            if (!commandRejected) throw new Exception("Unsupported editing command silently rewrites text.");
            var suggestion = CorrectionLearning.Suggest("Send this to sue dear today.", "Send this to Sudheer today.");
            if (suggestion == null || suggestion.Trigger != "sue dear" || suggestion.Replacement != "Sudheer" || CorrectionLearning.Suggest("Do not pay Alex", "Do pay Alex") != null) throw new Exception("Word learning is missing or suggests a negation change.");
            string calendar = "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:one\r\nDTSTART:20261029T180000Z\r\nSUMMARY:Design \\nreview\r\nEND:VEVENT\r\nEND:VCALENDAR";
            var meetings = MeetingCalendar.Parse(calendar);
            if (meetings.Count != 1 || MeetingCalendar.Start(meetings[0]) != new DateTime(2026, 10, 29, 18, 0, 0, DateTimeKind.Utc) || meetings[0].Title != "Design \nreview") throw new Exception("Calendar time or escaped text changed.");
            bool recurrenceRejected = false; try { MeetingCalendar.Parse(calendar.Replace("SUMMARY:", "RRULE:FREQ=DAILY\r\nSUMMARY:")); } catch (System.IO.InvalidDataException) { recurrenceRejected = true; }
            if (!recurrenceRejected) throw new Exception("Unsupported calendar recurrence was silently treated as a single meeting.");
            using (var editor = new System.Windows.Forms.RichTextBox())
            {
                editor.Text = "Thanks, Alex"; editor.SelectAll(); editor.SelectionFont = new System.Drawing.Font(editor.Font, System.Drawing.FontStyle.Bold);
                var rich = new PersonalRule { Kind = "Snippet", Trigger = "my signature", Replacement = editor.Text, ReplacementRtf = editor.Rtf };
                PersonalRule.Validate(new[] { rich }); c.Personalization.Add(rich);
                if (WritingFormat.Compose(c, "my signature.") != rich.Replacement || rich.Copy().ReplacementRtf != rich.ReplacementRtf) throw new Exception("Exact snippets lose literal text or formatting.");
                c.RichSnippetPaste = true; c.CleanupEnabled = false;
                if (RichSnippets.Formatted(c, "my signature") != null) throw new Exception("Raw mode activates formatted snippets.");
                c.CleanupEnabled = true;
                rich.Replacement = "different"; bool invalidRtf = false; try { PersonalRule.Validate(new[] { rich }); } catch (ArgumentException) { invalidRtf = true; }
                if (!invalidRtf) throw new Exception("Inconsistent rich snippet was accepted.");
            }
            if (root != null)
            {
                var store = new NoteStore(System.IO.Path.Combine(root, "atomic-notes")); var note = store.Create("Atomic test", "Microphone", ""); note.Status = "Saved"; note.WrittenNotes = "previous"; store.Save(note);
                string path = System.IO.Path.Combine(store.Folder(note), "note.json"), previous = System.IO.File.ReadAllText(path);
                using (var locked = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
                {
                    note.WrittenNotes = "new text"; bool failed = false; try { store.Save(note); } catch (System.IO.IOException) { failed = true; }
                    if (!failed || System.IO.File.ReadAllText(path) != previous || store.List(false)[0].WrittenNotes != "previous") throw new Exception("Failed note save replaced the committed snapshot.");
                }
                store.Save(note); System.IO.File.WriteAllText(path, "{");
                if (store.List(false)[0].WrittenNotes != "new text") throw new Exception("Corrupt note did not recover its last committed snapshot.");
            }
            Console.Write("product-ok|simultaneous-rules|longest-match|identifiers|raw-preserved");
        }
    }
}
