using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Notewll
{
    // Detects an ongoing call by asking Windows which processes hold an active microphone (capture) session,
    // the same signal behind the taskbar microphone indicator. Desktop call apps count as soon as they capture;
    // browsers and WebView2 hosts count only when a visible window title identifies a meeting page.
    static class MeetingDetector
    {
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class EnumeratorClass { }
        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out IDeviceCollection result);
        }
        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IDeviceCollection { [PreserveSig] int GetCount(out uint count); [PreserveSig] int Item(uint index, out IDevice device); }
        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IDevice { [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object result); }
        [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface ISessionManager2
        {
            [PreserveSig] int GetAudioSessionControl(IntPtr group, uint flags, out IntPtr control);
            [PreserveSig] int GetSimpleAudioVolume(IntPtr group, uint flags, out IntPtr volume);
            [PreserveSig] int GetSessionEnumerator(out ISessionEnumerator sessions);
        }
        [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface ISessionEnumerator { [PreserveSig] int GetCount(out int count); [PreserveSig] int GetSession(int index, [MarshalAs(UnmanagedType.IUnknown)] out object session); }
        [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface ISessionControl2
        {
            [PreserveSig] int GetState(out int state);
            [PreserveSig] int GetDisplayName(out IntPtr name);
            [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr context);
            [PreserveSig] int GetIconPath(out IntPtr path);
            [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, IntPtr context);
            [PreserveSig] int GetGroupingParam(out Guid group);
            [PreserveSig] int SetGroupingParam(ref Guid group, IntPtr context);
            [PreserveSig] int RegisterAudioSessionNotification(IntPtr client);
            [PreserveSig] int UnregisterAudioSessionNotification(IntPtr client);
            [PreserveSig] int GetSessionIdentifier(out IntPtr id);
            [PreserveSig] int GetSessionInstanceIdentifier(out IntPtr id);
            [PreserveSig] int GetProcessId(out uint processId);
        }
        const int CaptureFlow = 1, ActiveState = 1;
        const uint DeviceActive = 1, ClsctxAll = 23;

        // Process name (lowercase, no .exe) -> app shown to the user. These only open the microphone for calls.
        static readonly Dictionary<string, string> CallApps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ms-teams", "Teams" }, { "teams", "Teams" }, { "msteams", "Teams" },
            { "zoom", "Zoom" }, { "cpthost", "Zoom" },
            { "webex", "Webex" }, { "webexmta", "Webex" }, { "ciscocollabhost", "Webex" }, { "atmgr", "Webex" }, { "ciscowebexstart", "Webex" },
            { "slack", "Slack" },
        };
        static readonly HashSet<string> Browsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "arc", "msedgewebview2" };

        // Window title fragments that identify a call page in a browser or WebView2 host.
        public static string MeetingFromTitles(IEnumerable<string> titles)
        {
            foreach (var raw in titles)
            {
                string t = raw ?? "";
                if (t.StartsWith("Meet - ", StringComparison.OrdinalIgnoreCase) || t.StartsWith("Meet – ", StringComparison.OrdinalIgnoreCase) || t.IndexOf("meet.google.com", StringComparison.OrdinalIgnoreCase) >= 0) return "Google Meet";
                if (t.IndexOf("Microsoft Teams", StringComparison.OrdinalIgnoreCase) >= 0 && (t.IndexOf("Meeting", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("Call", StringComparison.OrdinalIgnoreCase) >= 0)) return "Teams";
                if (t.IndexOf("Zoom Meeting", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("Zoom Workplace", StringComparison.OrdinalIgnoreCase) >= 0 && t.IndexOf("Meeting", StringComparison.OrdinalIgnoreCase) >= 0) return "Zoom";
                if (t.IndexOf("Webex", StringComparison.OrdinalIgnoreCase) >= 0 && t.IndexOf("Meeting", StringComparison.OrdinalIgnoreCase) >= 0) return "Webex";
                if (t.IndexOf("Huddle", StringComparison.OrdinalIgnoreCase) >= 0 && t.IndexOf("Slack", StringComparison.OrdinalIgnoreCase) >= 0) return "Slack";
            }
            return null;
        }

        // Pure decision used by the detector and tests: given capturing process names and visible window titles, name the meeting app or null.
        public static string Classify(IEnumerable<string> capturingProcesses, Func<IEnumerable<string>> windowTitles, Func<string, string> parentOf = null)
        {
            bool browserCapturing = false;
            foreach (var name in capturingProcesses)
            {
                string app;
                if (CallApps.TryGetValue(name, out app)) return app;
                if (name.Equals("msedgewebview2", StringComparison.OrdinalIgnoreCase) && parentOf != null)
                {
                    string parent = parentOf(name);
                    if (parent != null && CallApps.TryGetValue(parent, out app)) return app;
                }
                if (Browsers.Contains(name)) browserCapturing = true;
            }
            return browserCapturing ? MeetingFromTitles(windowTitles()) : null;
        }

        // Returns the meeting app currently using the microphone, or null. Never throws.
        public static string ActiveMeetingApp() { var signal = Active(); return signal == null ? null : signal.App; }

        public sealed class Signal { public string App, WindowTitle, Name, Code; }

        // The active call with its best window title and what that title says about the meeting, or null.
        public static Signal Active()
        {
            try
            {
                var pids = CapturingProcessIds(); int self = Process.GetCurrentProcess().Id; pids.Remove(self);
                if (pids.Count == 0) return null;
                var names = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (int pid in pids) { try { names[Process.GetProcessById(pid).ProcessName] = pid; } catch { } }
                var windows = VisibleWindows();
                string app = Classify(names.Keys, () => windows.Select(w => w.Value), name => { int pid; return names.TryGetValue(name, out pid) ? ParentName(pid) : null; });
                if (app == null) return null;
                string title = BestTitle(app, windows);
                var signal = new Signal { App = app, WindowTitle = title };
                ParseTitle(app, title, out signal.Name, out signal.Code);
                return signal;
            }
            catch { return null; }
        }

        // Prefer the window that belongs to the call app and looks like a call window.
        static string BestTitle(string app, List<KeyValuePair<string, string>> windows)
        {
            var owners = CallApps.Where(p => p.Value == app).Select(p => p.Key).ToList();
            Func<KeyValuePair<string, string>, int> score = w =>
            {
                string t = w.Value; int s = 0;
                if (owners.Contains(w.Key, StringComparer.OrdinalIgnoreCase)) s += 4;
                if (Browsers.Contains(w.Key) && MeetingFromTitles(new[] { t }) == app) s += 6;
                if (t.IndexOf("Meeting", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("Call", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("Huddle", StringComparison.OrdinalIgnoreCase) >= 0) s += 2;
                if (t.Length < 3) s -= 10;
                return s;
            };
            var best = windows.OrderByDescending(score).FirstOrDefault();
            return best.Value != null && score(best) >= 4 ? best.Value : null;
        }

        static readonly Regex MeetCode = new Regex(@"\b([a-z]{3}-[a-z]{4}-[a-z]{3})\b", RegexOptions.IgnoreCase);
        static readonly string[] BrowserSuffixes = { " - Google Chrome", " - Microsoft​ Edge", " - Microsoft Edge", " — Mozilla Firefox", " - Mozilla Firefox", " - Brave", " - Opera", " - Vivaldi", " - Personal", " - Work" };
        // Turn a call window title into a meeting name and code, e.g. "Weekly sync | Microsoft Teams" -> "Weekly sync".
        public static void ParseTitle(string app, string title, out string name, out string code)
        {
            name = code = null; if (String.IsNullOrWhiteSpace(title)) return;
            string t = title.Trim();
            foreach (string suffix in BrowserSuffixes) if (t.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) t = t.Substring(0, t.Length - suffix.Length).Trim();
            var m = MeetCode.Match(t); if (app == "Google Meet" && m.Success) code = m.Groups[1].Value.ToLowerInvariant();
            if (app == "Google Meet")
            {
                t = Regex.Replace(t, @"^Meet\s*[-–]\s*", "", RegexOptions.IgnoreCase);
                if (code != null) t = t.Replace(m.Groups[1].Value, "").Trim(' ', '-', '–', '|');
            }
            t = Regex.Replace(t, @"\s*\|\s*Microsoft Teams.*$", "", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"\s*[-|]\s*(Webex|Slack|Zoom Workplace|Zoom)$", "", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"^(Huddle|Meeting|Call)\s*[|:-]\s*", "", RegexOptions.IgnoreCase).Trim();
            var generic = new[] { "", "Zoom Meeting", "Zoom", "Zoom Workplace", "Meeting", "Microsoft Teams", "Meet", "Webex", "Slack", "Call", "Huddle", "Google Meet" };
            if (!generic.Contains(t, StringComparer.OrdinalIgnoreCase)) name = t.Length > 200 ? t.Substring(0, 200) : t;
        }

        // Diagnostic: which processes capture right now and what the detector concludes.
        public static string Describe()
        {
            var pids = CapturingProcessIds(); var lines = new StringBuilder();
            foreach (int pid in pids) { string name; try { name = Process.GetProcessById(pid).ProcessName; } catch { name = "?"; } lines.AppendLine("capturing: " + name + " (" + pid + ")" + (name.Equals("msedgewebview2", StringComparison.OrdinalIgnoreCase) ? " parent=" + ParentName(pid) : "")); }
            var signal = Active();
            lines.AppendLine("meeting: " + (signal == null ? "none" : signal.App));
            if (signal != null) lines.AppendLine("window: " + signal.WindowTitle + "\nname: " + signal.Name + "\ncode: " + signal.Code);
            return lines.ToString();
        }

        static HashSet<int> CapturingProcessIds()
        {
            var result = new HashSet<int>();
            var enumerator = (IDeviceEnumerator)new EnumeratorClass(); IDeviceCollection devices = null;
            try
            {
                if (enumerator.EnumAudioEndpoints(CaptureFlow, DeviceActive, out devices) != 0 || devices == null) return result;
                uint count; devices.GetCount(out count);
                for (uint i = 0; i < count; i++)
                {
                    IDevice device = null; object managerObject = null; ISessionEnumerator sessions = null;
                    try
                    {
                        if (devices.Item(i, out device) != 0 || device == null) continue;
                        var iid = typeof(ISessionManager2).GUID;
                        if (device.Activate(ref iid, ClsctxAll, IntPtr.Zero, out managerObject) != 0 || managerObject == null) continue;
                        if (((ISessionManager2)managerObject).GetSessionEnumerator(out sessions) != 0 || sessions == null) continue;
                        int sessionCount; sessions.GetCount(out sessionCount);
                        for (int s = 0; s < sessionCount; s++)
                        {
                            object session = null;
                            try
                            {
                                if (sessions.GetSession(s, out session) != 0 || session == null) continue;
                                var control = (ISessionControl2)session; int state; uint pid;
                                if (control.GetState(out state) == 0 && state == ActiveState && control.GetProcessId(out pid) == 0 && pid != 0) result.Add((int)pid);
                            }
                            finally { CoreAudio.Release(session); }
                        }
                    }
                    finally { CoreAudio.Release(sessions); CoreAudio.Release(managerObject); CoreAudio.Release(device); }
                }
            }
            finally { CoreAudio.Release(devices); CoreAudio.Release(enumerator); }
            return result;
        }

        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc fn, IntPtr param);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        delegate bool EnumProc(IntPtr hwnd, IntPtr param);
        // Visible top-level windows as (owning process name, title).
        static List<KeyValuePair<string, string>> VisibleWindows()
        {
            var windows = new List<KeyValuePair<string, string>>(); var names = new Dictionary<uint, string>();
            EnumWindows((h, p) =>
            {
                if (!IsWindowVisible(h)) return true;
                var t = new StringBuilder(512); if (GetWindowText(h, t, t.Capacity) == 0) return true;
                uint pid; GetWindowThreadProcessId(h, out pid); string name;
                if (!names.TryGetValue(pid, out name)) { try { name = Process.GetProcessById((int)pid).ProcessName; } catch { name = ""; } names[pid] = name; }
                windows.Add(new KeyValuePair<string, string>(name, t.ToString())); return true;
            }, IntPtr.Zero);
            return windows;
        }

        [StructLayout(LayoutKind.Sequential)] struct ProcessBasicInformation { public IntPtr Reserved1, PebBaseAddress, Reserved2a, Reserved2b, UniqueProcessId, InheritedFromUniqueProcessId; }
        [DllImport("ntdll.dll")] static extern int NtQueryInformationProcess(IntPtr process, int infoClass, ref ProcessBasicInformation info, int size, out int returned);
        static string ParentName(int pid)
        {
            try
            {
                using (var process = Process.GetProcessById(pid))
                {
                    var info = new ProcessBasicInformation(); int returned;
                    if (NtQueryInformationProcess(process.Handle, 0, ref info, Marshal.SizeOf(info), out returned) != 0) return null;
                    // Walk up WebView2's own helper chain to the hosting app.
                    int parent = info.InheritedFromUniqueProcessId.ToInt32();
                    for (int depth = 0; depth < 4 && parent > 0; depth++)
                    {
                        using (var p = Process.GetProcessById(parent))
                        {
                            if (!p.ProcessName.Equals("msedgewebview2", StringComparison.OrdinalIgnoreCase)) return p.ProcessName;
                            var next = new ProcessBasicInformation();
                            if (NtQueryInformationProcess(p.Handle, 0, ref next, Marshal.SizeOf(next), out returned) != 0) return null;
                            parent = next.InheritedFromUniqueProcessId.ToInt32();
                        }
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
