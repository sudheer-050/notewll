using System;

namespace Notewll
{
    sealed class LiveEdit
    {
        public int Remove;
        public string Append;
        public static LiveEdit Between(string previous, string next)
        {
            previous = previous ?? ""; next = next ?? ""; int shared = 0;
            while (shared < previous.Length && shared < next.Length && previous[shared] == next[shared]) shared++;
            if (shared > 0 && shared < previous.Length && Char.IsLowSurrogate(previous[shared])) shared--;
            return new LiveEdit { Remove = previous.Length - shared, Append = next.Substring(shared) };
        }
    }
    static class PastePolicy
    {
        public static string Resolve(string mode, string windowClass)
        {
            if (mode != "Auto") return mode;
            if ((windowClass ?? "").IndexOf("CASCADIA", StringComparison.OrdinalIgnoreCase) >= 0) return "CtrlShiftV";
            if (windowClass == "ConsoleWindowClass" || windowClass == "mintty" || windowClass == "PuTTY") return "ShiftInsert";
            return "CtrlV";
        }
    }
}
