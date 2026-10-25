using System;
namespace Notewll
{
    static class SmartCleanupVerification
    {
        static void Check(bool okay, string message) { if (!okay) throw new Exception(message); }
        public static void Run()
        {
            var raw = new Config { CleanupEnabled = false, SmartCleanupEnabled = true, WritingStyle = "Email" };
            raw.Personalization.Add(new PersonalRule { Kind = "Word", Trigger = "hello", Replacement = "goodbye" });
            const string literal = "  hello um I I has a meeting  ";
            Check(SmartCleanup.Complete(raw, literal, 0) == literal, "Raw text changed");
            var modified = new Config { CleanupEnabled = true, SmartCleanupEnabled = true };
            Check(SmartCleanup.Complete(modified, "um I I has a meeting", 0) == "I has a meeting.", "Modified text changed sentence wording");
            Check(SmartCleanup.Complete(modified, "I was I was hoping to talk", 0) == "I was hoping to talk.", "Repeated start not cleaned");
            Check(SmartCleanup.Complete(modified, "I remembered to call her.", 0) == "I remembered to call her.", "Meaning changed");
            Check(SmartCleanup.Complete(modified, "Do not pay 15.50 to Sudheer before Friday.", 0) == "Do not pay 15.50 to Sudheer before Friday.", "Facts changed");
            Check(SmartCleanup.Complete(new Config { WritingStyle = "Code" }, "um I I has a meeting", 0) == "um I I has a meeting", "Code style changed words");
            Check(!SmartCleanup.Enabled(modified), "Retired setting enabled sentence editing");
            Check(SmartCleanup.Refine(modified, literal) == literal, "Retired refinement changed words");
            Check(!SmartCleanup.Accept("I has a meeting", "I have a meeting."), "Word replacement accepted");
            modified.Validate(); Check(!modified.SmartCleanupEnabled, "Retired preference not normalized");
            Console.Write("text-modes-ok|raw-unchanged|modified-cleanup|protected-facts|legacy-setting-ignored");
        }
    }
}
