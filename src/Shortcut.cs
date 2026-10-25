using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Notewll
{
    // One shared representation for the recorder, saved configuration and global hook.
    static class Shortcut
    {
        public static int Normalize(int key)
        {
            if (key == 162 || key == 163) return 17;
            if (key == 160 || key == 161) return 16;
            if (key == 164 || key == 165) return 18;
            if (key == 92) return 91;
            return key;
        }
        public static string Name(int key)
        {
            switch (Normalize(key)) { case 17: return "Ctrl"; case 16: return "Shift"; case 18: return "Alt"; case 91: return "Win"; }
            return ((Keys)key).ToString();
        }
        public static string Format(IEnumerable<int> keys)
        {
            return String.Join("+", keys.Select(Normalize).Distinct().OrderBy(k => k == 17 ? -4 : k == 18 ? -3 : k == 16 ? -2 : k == 91 ? -1 : k).Select(Name));
        }
        public static string Warning(string shortcut, string other)
        {
            var keys = Parse(shortcut); var secondary = Parse(other);
            if (keys.IsProperSubsetOf(secondary) || secondary.IsProperSubsetOf(keys)) return "One shortcut is a prefix of the other. It can start recording before the full combination is pressed.";
            if (keys.SetEquals(new[] { 17, 18, 46 }) || keys.SetEquals(new[] { 18, 9 }) || keys.SetEquals(new[] { 18, 115 }) || keys.Contains(91) && keys.Any(k => k == 76 || k == 68 || k == 71)) return "Windows commonly reserves this combination and may intercept it.";
            if (keys.All(k => k == 16 || k == 17 || k == 18 || k == 91) || keys.Count == 1 && keys.Single() >= 32 && keys.Single() <= 90) return "This key is also used for ordinary typing or shortcuts. A function key usually avoids interference.";
            return null;
        }
        public static HashSet<int> Parse(string text)
        {
            var keys = new HashSet<int>();
            foreach (string token in (text ?? "").Split('+'))
            {
                string t = token.Trim(); Keys value;
                int key = t.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ? 17 : t.Equals("Alt", StringComparison.OrdinalIgnoreCase) ? 18 : t.Equals("Shift", StringComparison.OrdinalIgnoreCase) ? 16 : t.Equals("Win", StringComparison.OrdinalIgnoreCase) ? 91 :
                    Enum.TryParse<Keys>(t, true, out value) ? (int)value : 0;
                key = Normalize(key);
                if (key < 8 || key > 254 || key == 27 || !Enum.IsDefined(typeof(Keys), key) || !keys.Add(key))
                    throw new InvalidDataException("Choose a key or combination; Escape is reserved for canceling.");
            }
            return keys;
        }
    }
    sealed class ShortcutTracker
    {
        readonly HashSet<int> held = new HashSet<int>();
        readonly HashSet<int> swallowed = new HashSet<int>();
        public bool Active { get; private set; }
        public void Reset() { held.Clear(); swallowed.Clear(); Active = false; }
        // Returns start/stop transitions separately from whether Windows should receive this event.
        public bool Feed(HashSet<int> binding, int key, bool down, out int transition)
        {
            key = Shortcut.Normalize(key); transition = 0;
            if (down) held.Add(key); else held.Remove(key);
            bool consume = swallowed.Contains(key);
            if (!Active && down && held.SetEquals(binding))
            {
                Active = true; transition = 1; swallowed.Add(key); consume = true;
            }
            else if (Active && !down && binding.Contains(key)) { Active = false; transition = -1; }
            if (!down) swallowed.Remove(key);
            return consume;
        }
    }
}
