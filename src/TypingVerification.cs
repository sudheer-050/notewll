using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Notewll
{
    static class TypingVerification
    {
        internal const string Sentence = "Why are you writing question marks?";
        public static void Keymap()
        {
            IntPtr layout = Native.GetKeyboardLayout(0); int ordinary = 0;
            string sample = Sentence + " ABC xyz 0123456789 .,!?:; 'quoted' \"double\" @#$%^&*()_+-=[]{}\\/|~`\t\r\n Jos\u00e9 \u674e \ud83d\ude42";
            foreach (bool caps in new[] { false, true })
            {
                var result = new StringBuilder(); var state = new byte[256]; if (caps) state[0x14] = 1;
                foreach (char c in sample)
                {
                    foreach (var input in Native.CharacterKeys(c, layout, caps))
                    {
                        var key = input.union.key; bool up = (key.flags & Native.KEYEVENTF_KEYUP) != 0;
                        if ((key.flags & 4) != 0) { if (!up) result.Append((char)key.scan); continue; }
                        if (key.vk == 0x10) { state[0x10] = up ? (byte)0 : (byte)0x80; continue; }
                        if (!up) { var actual = new StringBuilder(8); int count = Native.ToUnicodeEx(key.vk, key.scan, state, actual, actual.Capacity, 4, layout); if (count != 1) throw new Exception("Typing used an ambiguous keyboard mapping."); result.Append(actual[0]); ordinary++; }
                    }
                    if (state[0x10] != 0) throw new Exception("Typing left Shift pressed.");
                }
                if (result.ToString() != sample) throw new Exception("Keyboard mapping changed the sentence, punctuation or Unicode text.");
            }
            Console.Write("keymap-ok|caps-on-and-off|unicode-preserved|ordinary-characters=" + ordinary);
        }
        public static void Editors()
        {
            Application.EnableVisualStyles();
            foreach (bool rich in new[] { false, true })
            using (var form = new Form { Text = "Notewll typing check", Width = 720, Height = 240 })
            using (TextBoxBase field = rich ? (TextBoxBase)new RichTextBox() : new ThemedTextBox { Multiline = true })
            {
                field.Dock = DockStyle.Fill; field.Text = "Keep this. "; form.Controls.Add(field);
                form.Show(); form.Activate(); field.Focus(); field.SelectionStart = field.TextLength; Application.DoEvents();
                if (!Native.WriteLive(form.Handle, LiveEdit.Between("", Sentence))) throw new Exception("Desktop keyboard injection is blocked or focus changed.");
                Expect(field, "Keep this. " + Sentence);
                string previous = Sentence;
                foreach (string next in new[] { "Why.", "Why are.", "Why are you.", "Why are you writing.", "Why are you writing question.", Sentence })
                {
                    if (!Native.WriteLive(form.Handle, LiveEdit.Between(previous, next))) throw new Exception("Desktop keyboard injection is blocked or focus changed.");
                    Expect(field, "Keep this. " + next); previous = next;
                }
                form.Close();
            }
            Console.Write("typing-ok|plain-and-rich-editors|exact-sentence|live-revisions|surrounding-preserved");
        }
        static void Expect(TextBoxBase field, string expected)
        {
            var timer = Stopwatch.StartNew();
            do { Application.DoEvents(); if (field.Text == expected) return; Thread.Sleep(10); } while (timer.ElapsedMilliseconds < 2000);
            throw new Exception("Typing mismatch. Expected: " + expected + " | Received: " + field.Text);
        }
    }
}
