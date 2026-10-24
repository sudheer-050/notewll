using System;
using System.Diagnostics;
using System.Threading;
namespace LocalDictation
{
    static class SmartCleanupVerification
    {
        static void Check(bool okay, string message) { if (!okay) throw new Exception(message); }
        public static void Run()
        {
            string[,] edits = {
                { "Can you list what are what updates you have done?", "Can you list what updates you have done?" },
                { "Hey, how are you? I just wanted to talk to you about what are what what about the king?", "Hey, how are you? I just wanted to talk to you about the king?" },
                { "I has a meeting tomorrow", "I have a meeting tomorrow." },
                { "She have a report", "She has a report." },
                { "He go to work", "He goes to work." },
                { "They needs a report", "They need a report." },
                { "I was I was hoping you could help", "I was hoping you could help." },
                { "We need to send the report send the report tomorrow", "We need to send the report tomorrow." },
                { "I need meeting tomorrow", "I need a meeting tomorrow." },
                { "I want to ask I want to ask about the deadline", "I want to ask about the deadline." }
            };
            for (int i = 0; i < edits.GetLength(0); i++) Check(SmartCleanup.Accept(edits[i,0], edits[i,1]), "Valid repair rejected: " + edits[i,0]);
            string[,] unsafeEdits = {
                { "Do not send it", "Do send it." }, { "Send it at 3", "Send it by 3." },
                { "Pay 15.50 to Alex", "Pay 1550 to Alex." }, { "Send it to Sudheer", "Send it to Alex." },
                { "We need to send the report and invoice", "We need to send the report." },
                { "I don't agree", "I do agree." }, { "I had a meeting", "I have a meeting." },
                { "Only send one copy", "Send one copy." }, { "Wait until Friday", "Wait Friday." },
                { "He said that that was fine", "He said that was fine." }, { "She had had enough", "She had enough." },
                { "I can send it", "I will send it." }, { "I paid 15 and 15", "I paid 15." },
                { "I have a meeting tomorrow", "I have a meeting tomorrow with the team to discuss the roadmap." },
                { "Call Alex, then Sam", "Call Sam, then Alex." }
            };
            for (int i = 0; i < unsafeEdits.GetLength(0); i++) Check(!SmartCleanup.Accept(unsafeEdits[i,0], unsafeEdits[i,1]), "Unsafe rewrite accepted: " + unsafeEdits[i,0]);
            Check(WritingFormat.Compose(new Config(), "I was I was hoping to talk") == "I was hoping to talk.", "Repeated prefix was not cleaned locally");
            var disabled = new Config { SmartCleanupEnabled = false }; Check(SmartCleanup.Complete(disabled, "I has a meeting", 1200) == "I has a meeting.", "Disabled model changed words");
            Check(SmartCleanup.Complete(new Config { WritingStyle = "Code" }, "I was I was hoping", 1200) == "I was I was hoping", "Code mode rewrote words");
            Check(SmartCleanup.Complete(new Config { CleanupEnabled = false }, "I was I was hoping", 1200) == "I was I was hoping", "Verbatim mode rewrote words");
            using (var entered = new ManualResetEvent(false)) using (var release = new ManualResetEvent(false))
            {
                var grammar = new LiveGrammar(raw => { entered.Set(); release.WaitOne(); return "I have a meeting."; }, false);
                grammar.Offer("I has a meeting"); Check(entered.WaitOne(2000), "Refinement did not start");
                var timer = Stopwatch.StartNew();
                try { Check(grammar.ResolveFinal("I has a meeting tomorrow", 100) == "I has a meeting tomorrow", "Stale result changed latest words"); Check(timer.ElapsedMilliseconds < 500, "Cleanup blocked final release"); }
                finally { release.Set(); }
                Check(grammar.Resolve("I has a meeting", 2000) == "I have a meeting.", "Ready result lost");
            }
            Console.Write("smart-cleanup-ok|general-restarts|grammar|protected-facts|bounded-release|no-stale-edit|verbatim");
        }
    }
}
