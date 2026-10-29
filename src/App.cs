using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Notewll
{
    static class Native
    {
        public const int WH_MOUSE_LL = 14;
        public const int WH_KEYBOARD_LL = 13, WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
        public const int VK_ESCAPE = 0x1B, INPUT_KEYBOARD = 1, KEYEVENTF_KEYUP = 0x0002;
        public delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)] public struct Kbd { public uint vkCode, scanCode, flags, time; public IntPtr extra; }
        [StructLayout(LayoutKind.Sequential)] public struct Input { public uint type; public InputUnion union; }
        [StructLayout(LayoutKind.Explicit)] public struct InputUnion { [FieldOffset(0)] public KeyInput key; [FieldOffset(0)] public MouseInput mouse; }
        [StructLayout(LayoutKind.Sequential)] public struct KeyInput { public ushort vk, scan; public uint flags, time; public IntPtr extra; }
        [StructLayout(LayoutKind.Sequential)] public struct MouseInput { public int dx, dy; public uint data, flags, time; public IntPtr extra; }

        [DllImport("user32.dll")] public static extern IntPtr SetWindowsHookEx(int id, HookProc fn, IntPtr module, uint threadId);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, System.Text.StringBuilder name, int size);
        [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] static extern short GetKeyState(int key);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, IntPtr processId);
        [DllImport("user32.dll")] internal static extern IntPtr GetKeyboardLayout(uint thread);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern short VkKeyScanEx(char character, IntPtr layout);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int ToUnicodeEx(uint key, uint scan, byte[] state, System.Text.StringBuilder text, int capacity, uint flags, IntPtr layout);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern uint SendInput(uint count, Input[] inputs, int size);
        [DllImport("kernel32.dll")] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc fn, IntPtr param);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, System.Text.StringBuilder text, int max);
        delegate bool EnumProc(IntPtr hwnd, IntPtr param);

        public static string WindowTitle(IntPtr window)
        {
            var value = new System.Text.StringBuilder(512); GetWindowText(window, value, value.Capacity); return value.ToString();
        }

        // Test-harness helper: first visible top-level window whose title contains the text.
        public static IntPtr FindWindowByTitle(string part)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((h, p) =>
            {
                var t = new System.Text.StringBuilder(256); GetWindowText(h, t, 256);
                if (IsWindowVisible(h) && t.ToString().IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) { found = h; return false; }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        public static bool Paste(IntPtr target, string method = "Auto")
        {
            if (target == IntPtr.Zero || !IsWindow(target)) return false;
            if (GetForegroundWindow() != target && !SetForegroundWindow(target)) return false;
            Thread.Sleep(80);
            if (GetForegroundWindow() != target) return false;
            if (new[] { 16, 17, 18, 91, 92 }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0)) return false;
            var windowClass = new System.Text.StringBuilder(256); GetClassName(target, windowClass, windowClass.Capacity);
            string mode = PastePolicy.Resolve(method, windowClass.ToString());
            var inputs = mode == "ShiftInsert" ? new[] { Key(0x10, 0), Key(0x2D, 0), Key(0x2D, KEYEVENTF_KEYUP), Key(0x10, KEYEVENTF_KEYUP) } :
                mode == "CtrlShiftV" ? new[] { Key(0x11, 0), Key(0x10, 0), Key(0x56, 0), Key(0x56, KEYEVENTF_KEYUP), Key(0x10, KEYEVENTF_KEYUP), Key(0x11, KEYEVENTF_KEYUP) } :
                new[] { Key(0x11, 0), Key(0x56, 0), Key(0x56, KEYEVENTF_KEYUP), Key(0x11, KEYEVENTF_KEYUP) };
            return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Input))) == inputs.Length;
        }

        public static bool ModifiersDown() { return new[] { 16, 17, 18, 91, 92 }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0); }
        public static bool WriteLive(IntPtr target, LiveEdit edit)
        {
            if (GetForegroundWindow() != target || ModifiersDown()) return false;
            IntPtr layout = GetKeyboardLayout(GetWindowThreadProcessId(target, IntPtr.Zero));
            bool capsLock = (GetKeyState(0x14) & 1) != 0;
            for (int i = 0; i < edit.Remove; i++) if (!TypeGroup(target, new[] { Key(8, 0), Key(8, KEYEVENTF_KEYUP) })) return false;
            foreach (char c in edit.Append)
            {
                if (!TypeGroup(target, CharacterKeys(c, layout, capsLock))) return false;
            }
            return true;
        }
        static bool TypeGroup(IntPtr target, Input[] inputs)
        {
            if (GetForegroundWindow() != target || ModifiersDown()) return false;
            uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Input)));
            if (sent != inputs.Length)
            {
                // A short send may have pressed our Shift without sending its release.
                if (sent > 0 && inputs[0].union.key.vk == 0x10) SendInput(1, new[] { Key(0x10, KEYEVENTF_KEYUP) }, Marshal.SizeOf(typeof(Input)));
                return false;
            }
            Thread.Sleep(2); // Let the target process each key pair before the next character/revision.
            return true;
        }
        internal static Input[] CharacterKeys(char character, IntPtr layout, bool capsLock)
        {
            // Prefer ordinary keys where the target layout produces exactly this character.
            // Some editors/input methods discard VK_PACKET letters while accepting punctuation.
            short mapped = VkKeyScanEx(character, layout);
            if (character >= ' ' && mapped != -1 && ((mapped >> 8) & ~1) == 0)
            {
                ushort key = (ushort)(mapped & 255);
                foreach (bool shift in new[] { (mapped & 256) != 0, (mapped & 256) == 0 })
                {
                    var state = new byte[256]; if (shift) state[0x10] = 0x80; if (capsLock) state[0x14] = 1;
                    var actual = new System.Text.StringBuilder(8);
                    // Flag 4 leaves dead-key state unchanged. Never send a guessed/dead-key mapping.
                    if (ToUnicodeEx(key, 0, state, actual, actual.Capacity, 4, layout) != 1 || actual[0] != character) continue;
                    return shift ? new[] { Key(0x10, 0), Key(key, 0), Key(key, KEYEVENTF_KEYUP), Key(0x10, KEYEVENTF_KEYUP) } : new[] { Key(key, 0), Key(key, KEYEVENTF_KEYUP) };
                }
            }
            return new[] {
                new Input { type = INPUT_KEYBOARD, union = new InputUnion { key = new KeyInput { scan = character, flags = 4 } } },
                new Input { type = INPUT_KEYBOARD, union = new InputUnion { key = new KeyInput { scan = character, flags = 4 | KEYEVENTF_KEYUP } } }
            };
        }
        public static void Tap(ushort vk, bool down)
        {
            var inputs = new[] { Key(vk, down ? 0u : (uint)KEYEVENTF_KEYUP) };
            SendInput(1, inputs, Marshal.SizeOf(typeof(Input)));
        }

        static Input Key(ushort key, uint flags) { return new Input { type = INPUT_KEYBOARD, union = new InputUnion { key = new KeyInput { vk = key, flags = flags } } }; }
    }

    static class Startup
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", ValueName = "LocalDictation";
        public static bool Enabled { get { if (StoreDistribution.Enabled) return StoreDistribution.StartupEnabled; using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue(ValueName) != null; } }
        public static void Set(bool on)
        {
            if (StoreDistribution.Enabled) { StoreDistribution.SetStartup(on); return; }
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (on) k.SetValue(ValueName, "\"" + Application.ExecutablePath + "\" --background");
                else k.DeleteValue(ValueName, false);
            }
        }
    }

    sealed partial class DictationApp : ApplicationContext
    {
        readonly Overlay overlay = new Overlay();
        readonly FloatingDock dock = new FloatingDock();
        readonly NotifyIcon tray;
        readonly HashSet<int> bookmarkKeysDown = new HashSet<int>();
        readonly System.Windows.Forms.Timer noteStatus = new System.Windows.Forms.Timer { Interval = 1000 };
        readonly StateMachine state = new StateMachine();
        readonly DictationRecovery recovery = new DictationRecovery();
        RecoveryForm recoveryWindow;
        volatile bool exiting, recovering;
        bool setupShown;
        string engineReadiness = "Loading the offline speech engine...";
        readonly System.Collections.Generic.List<string> issues = new System.Collections.Generic.List<string>();
        void ReportIssue(string message) { string entry = DateTime.Now.ToString("HH:mm:ss") + "  " + message; issues.Insert(0, entry); if (issues.Count > 20) issues.RemoveAt(20); if (hub != null && !hub.IsDisposed) hub.SetIssues(issues); }
        readonly Native.HookProc hookProc, mouseProc;
        IntPtr mouseHook;
        readonly System.Windows.Forms.Timer calendarTimer = new System.Windows.Forms.Timer { Interval = 30000 };
        readonly HashSet<string> remindedMeetings = new HashSet<string>();
        bool calendarErrorReported;
        readonly System.Windows.Forms.Timer liveTimer = new System.Windows.Forms.Timer { Interval = 250 };
        readonly ManualResetEvent liveIdle = new ManualResetEvent(true);
        int liveBusy;
        LiveRecognition liveRecognition;
        string liveInserted = "";
        bool liveInterrupted, noteIndicator, sessionLiveWriting, sessionAccurate;
        volatile bool prewarming;
        int engineGeneration, lastAccurateBytes;
        string historyPlatform = "Unknown app", historyWindowTitle = "";
        DateTime historyStarted;
        double historyCaptureSeconds;
        long historyProcessingStarted;
        IntPtr hook, target;
        Config config, sessionConfig;
        MicRecorder recorder;
        readonly ShortcutTracker shortcutTracker = new ShortcutTracker();
        HashSet<int> shortcutKeys;
        readonly ShortcutTracker noteTracker = new ShortcutTracker();
        HashSet<int> noteBinding;
        readonly ShortcutTracker meetingTracker = new ShortcutTracker();
        HashSet<int> meetingBinding;
        void SetShortcutBindings()
        {
            shortcutKeys = Shortcut.Parse(config.Hotkey); shortcutTracker.Reset();
            noteBinding = Shortcut.Parse(config.NotesHotkey); noteTracker.Reset();
            meetingBinding = Shortcut.Parse(config.MeetingHotkey); meetingTracker.Reset();
        }
        void ToggleNotes(string mode, string kind = "Note")
        {
            if (state.Current != AppState.Idle) return;
            if (hub != null && hub.IsNoteRecording && hub.Notes.Document.Kind != kind) { ReportIssue("Stop the current recording before starting a different recording action."); return; }
            StopHandsFree();
            EnsureHub(); hub.ToggleNote(mode, kind); UpdateDock();
        }
        void MicrophoneListeningChanged() { PostUi(UpdateDock); }
        void UpdateDock()
        {
            if (hub != null && !hub.IsDisposed) { hub.SetRecoveryAvailable(recovery.HasWork); hub.SetToolbarStatus(dock.VisibilityDescription); }
            bool recording = hub != null && hub.IsNoteRecording; bool busy = hub != null && hub.NoteBusy;
            bool practice = hub != null && hub.IsPracticeListening;
            bool microphone = MicRecorder.AnyListening;
            bool passiveListening = microphone && !practice && handsFree == null && state.Current != AppState.Recording;
            string label = state.Current == AppState.Recording ? sessionAccurate && !AccurateSpeech.Ready ? "Recording now - preparing accurate text" : "Writing live - click Stop to refine" : state.Current == AppState.Processing ? "Refining your words..." : practice ? "Listening in Notewll - click Stop to review" : recording ? (hub.Notes.Document != null && hub.Notes.Document.Kind == "Meeting" ? "Recording meeting" : "Recording note") : passiveListening ? "Microphone listening in Notewll" : busy ? "Saving your note..." : "Ready - drag the grip to move";
            if (recording && hub.Notes.Document != null) label += " " + TimeSpan.FromSeconds(hub.Notes.RecordingSeconds).ToString(@"mm\:ss") + " - click Stop to save";
            if (!recording && !busy && hub != null && !String.IsNullOrWhiteSpace(hub.Notes.Feedback)) label = hub.Notes.Feedback;
            if (!recording && !busy && state.Current == AppState.Idle && config.AccurateDictationEnabled && !AccurateSpeech.Ready) label = engineReadiness;
            if (handsFree != null || handsFreeStarting) label = handsFreeMessage;
            dock.ShowVoiceIndicator = true;
            dock.UpdateState(handsFree != null || state.Current == AppState.Recording || microphone, state.Current == AppState.Processing, recording, busy, recording && hub.Notes.Document != null && hub.Notes.Document.Kind == "Meeting", label, handsFree != null, handsFreeStarting, passiveListening);
            if (dock.HasVoiceIndicator) overlay.Hide();
        }
        void DisplayVoiceIndicator(string text, Color color)
        {
            UpdateDock();
            if (!config.ShowVoiceIndicator || dock.HasVoiceIndicator) overlay.Hide();
            else overlay.Display(text, color);
        }
        void HideVoiceIndicator() { overlay.Hide(); UpdateDock(); }
        int generation;
        SettingsForm hub;
        EventWaitHandle showHubEvent, exitEvent;
        RegisteredWaitHandle exitWait;
        RegisteredWaitHandle showHubWait;
        // Last dictation, memory only, for the raw-vs-cleaned preview. Never written to disk.
        string lastRaw = "", lastCleaned = "";

        public DictationApp(bool showHub = true)
        {
            overlay.InputLevel = () => handsFree != null ? handsFree.Level : recorder != null ? recorder.SoundLevel : MicRecorder.AnyListening ? MicRecorder.ListeningLevel : hub != null ? hub.NoteSoundLevel : 0;
            dock.InputLevel = overlay.InputLevel;
            try { config = Config.Load(); }
            catch (InvalidDataException ex) { config = new Config(); MessageBox.Show(ex.Message + " Defaults will be used.", "Notewll"); }
            overlay.ReducedMotion = config.ReducedMotion;
            var menu = DialogUi.PopupMenu(false);
            menu.Items.Add("Open Notewll", null, (s, e) => ShowHub("Home"));
            menu.Items.Add("Edit copied text by voice...", null, (s, e) => { if (state.Current != AppState.Idle || LocalCommandDialog.OpenCount > 0) return; EnsureHub(); LocalCommandDialog.Show(hub, config.Copy(), Clipboard.ContainsText() ? Clipboard.GetText() : "", null, hub.CanTest); });
            menu.Items.Add("Show floating toolbar", null, (s, e) => { config.ShowToolbar = true; dock.SetVisibility(true); try { config.Save(); } catch (Exception ex) { ReportIssue("Toolbar shown, but its preference could not be saved: " + ex.Message); } });
            AddHandsFreeMenu(menu);
            calendarTimer.Tick += (s, e) => {
                if (!config.MeetingReminders || exiting) return;
                try { foreach (var meeting in MeetingCalendar.Load()) { var start = MeetingCalendar.Start(meeting); if (start >= DateTime.UtcNow.AddMinutes(-1) && start <= DateTime.UtcNow.AddMinutes(10) && remindedMeetings.Add(meeting.Id + meeting.StartUtc)) tray.ShowBalloonTip(8000, "Upcoming meeting", meeting.Title + " at " + start.ToLocalTime().ToString("t") + ". Open Preferences > Notes > Calendar to record.", ToolTipIcon.Info); } }
                catch (Exception ex) { if (!calendarErrorReported) { calendarErrorReported = true; ReportIssue("Calendar reminder unavailable: " + ex.Message); } }
            }; calendarTimer.Start();
            menu.Items.Add("Recover last dictation", null, (s, e) => ShowRecovery());
            menu.Items.Add("Issues & recovery", null, (s, e) => { ShowHub("Home"); if (hub != null) hub.ShowIssues(); });
            menu.Items.Add("Hide toolbar for 1 hour", null, (s, e) => dock.Snooze());
            menu.Items.Add("Keyboard action menu", null, (s, e) => ShowActionMenu());
            menu.Items.Add("Preferences", null, (s, e) => ShowHub("Preferences"));
            menu.Items.Add("Stop and save note", null, (s, e) => { if (hub != null && hub.IsNoteRecording) hub.ToggleNote(hub.NoteMode); });
            menu.Items.Add("Exit", null, (s, e) => ExitThread());
            tray = new NotifyIcon { Icon = AppLogo.Icon, Text = "Notewll " + StoreDistribution.DisplayVersion + " - hold " + config.Hotkey, Visible = true, ContextMenuStrip = menu };
            dock.RestorePosition(config.ToolbarX, config.ToolbarY);
            dock.Configure(config.ReducedMotion); dock.ConfigureBall(config); dock.CancelWrite = Cancel;
            dock.ToggleWrite = () => { if (handsFree != null || handsFreeStarting) { StopHandsFree(); return; } if (hub != null && hub.IsPracticeListening) hub.StopLocalPractice(); else if (state.Current == AppState.Recording) StopRecording(); else StartRecording(); UpdateDock(); };
            dock.ToggleHandsFree = ToggleHandsFree;
            dock.ToggleNote = () => { EnsureHub(); ToggleNotes("Microphone", "Note"); }; dock.ToggleMeeting = () => ToggleNotes(config.MeetingSource == "Computer" ? "Computer" : "Both", "Meeting");
            dock.PositionSaved = position => { config.ToolbarX = position.X; config.ToolbarY = position.Y; try { config.Save(); } catch { } };
            dock.SetVisibility(config.ShowToolbar);
            MicRecorder.ListeningChanged += MicrophoneListeningChanged;
            noteStatus.Tick += (s, e) =>
            {
                CheckHandsFreeDevice(); LocalSnapshots.Schedule(config, hub != null && hub.NoteBusy);
                UpdateDock();
                bool recordingNote = hub != null && hub.IsNoteRecording;
                if (config.ShowVoiceIndicator && recordingNote && !noteIndicator && state.Current == AppState.Idle) { DisplayVoiceIndicator("Recording from " + hub.NoteMode + " notes", Color.FromArgb(45, 120, 75)); noteIndicator = true; }
                if (!recordingNote && noteIndicator) { if (state.Current == AppState.Idle) overlay.Hide(); noteIndicator = false; }
                tray.Text = handsFree != null ? "Notewll - hands-free microphone ON" : hub != null && hub.IsNoteRecording ? "Notewll - recording " + hub.NoteMode + " notes" : "Notewll - hold " + (config.Hotkey.Length > 30 ? config.Hotkey.Substring(0, 30) : config.Hotkey); }; noteStatus.Start();
            tray.DoubleClick += (s, e) => ShowHub("Home");
            Microsoft.Win32.SystemEvents.SessionSwitch += SessionChangedForHandsFree;
            SetShortcutBindings();
            liveTimer.Tick += (s, e) => LiveTick();
            mouseProc = (code, message, data) =>
            {
                var mousePoint = code >= 0 ? Marshal.PtrToStructure<Point>(data) : Point.Empty;
                if (code >= 0 && (message == (IntPtr)0x201 || message == (IntPtr)0x204 || message == (IntPtr)0x207 || message == (IntPtr)0x20A)) Interlocked.Increment(ref handsFreeInputEpoch);
                if (code >= 0 && !dock.Bounds.Contains(mousePoint) && (message == (IntPtr)0x201 || message == (IntPtr)0x204 || message == (IntPtr)0x207 || message == (IntPtr)0x20A) && (state.Current == AppState.Recording || state.Current == AppState.Processing)) liveInterrupted = true;
                return Native.CallNextHookEx(mouseHook, code, message, data);
            };
            mouseHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, mouseProc, Native.GetModuleHandle(null), 0);
            hookProc = KeyboardHook;
            hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, hookProc, Native.GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero) throw new InvalidOperationException("Could not register the global keyboard hook.");
            // Create the message target without showing a startup/status box.
            var overlayHandle = overlay.Handle;
            exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "LocalDictation.Exit");
            exitWait = ThreadPool.RegisterWaitForSingleObject(exitEvent, (s, timedOut) => { if (!overlay.IsDisposed) PostUi(() => ExitThread()); }, null, Timeout.Infinite, false);
            showHubEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "LocalDictation.ShowHub");
            showHubWait = ThreadPool.RegisterWaitForSingleObject(showHubEvent, (s, timedOut) =>
            {
                if (!overlay.IsDisposed) PostUi(() => ShowHub("Home"));
            }, null, Timeout.Infinite, false);
            if (showHub) PostUi(() => { ShowHub("Home"); Application.Idle += WarmWhenUiIdle; });
            else Prewarm();
            if (config.CheckUpdatesOnStart) CheckStartupUpdate();
        }
        void WarmWhenUiIdle(object sender, EventArgs e) { Application.Idle -= WarmWhenUiIdle; if (!exiting && !prewarming && !AccurateSpeech.Ready) Prewarm(); }
        async void CheckStartupUpdate()
        {
            try { string version = await ReleaseUpdates.AvailableVersion(config.Copy()); if (version != null && !exiting) PostUi(() => { ReportIssue("Version " + version + " is available. Open Preferences > About > Check for updates to verify and install it."); tray.ShowBalloonTip(5000, "Update available", "Open Preferences > About to download and verify version " + version + ".", ToolTipIcon.Info); }); }
            catch (Exception ex) { PostUi(() => ReportIssue("Startup update check failed; installed speech still works offline. " + ex.Message)); }
        }

        // Make sure the bundled speech engine is up before the first dictation; only speaks up if it cannot be reached.
        void PostUi(Action action)
        {
            if (exiting || overlay.IsDisposed || !overlay.IsHandleCreated) return;
            try { overlay.BeginInvoke((Action)(() => { if (!exiting && !overlay.IsDisposed) action(); })); } catch (InvalidOperationException) { }
        }
        void Prewarm()
        {
            var cfg = config.Copy(); int revision = ++engineGeneration; prewarming = true;
            engineReadiness = cfg.SpeechLanguage != "en" ? "Preparing local multilingual speech..." : cfg.AccurateDictationEnabled ? "Preparing accurate local speech..." : "Preparing basic offline speech...";
            if (hub != null && !hub.IsDisposed) hub.SetReadiness(engineReadiness);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                string problem = null;
                try
                {
                    if (cfg.SpeechLanguage != "en") SpeechQuality.Recognize(cfg, new byte[0], false);
                    else if (cfg.AccurateDictationEnabled) AccurateSpeech.Warmup(cfg);
                    else { AccurateSpeech.Shutdown(); problem = SpeechEngine.EnsureRunning(cfg, 30); }
                }
                catch (Exception ex) { problem = ex.Message; }
                string detail = problem;
                PostUi(() =>
                {
                    if (revision != engineGeneration) return;
                    prewarming = false;
                    engineReadiness = detail ?? (cfg.SpeechLanguage != "en" ? "Ready - local multilingual speech." : cfg.AccurateDictationEnabled ? "Ready - accurate local speech with speech detection." : "Ready - basic offline speech.");
                    if (hub != null && !hub.IsDisposed) hub.SetReadiness(engineReadiness);
                    if (detail != null) ReportIssue(detail);
                    UpdateDock();
                });
            });
        }

        void StartRecording()
        {
            if (state.Current != AppState.Idle || recovering) return;
            if (hub != null && hub.IsTesting) return;
            if (config.SpeechLanguage != "en" && !SpeechLanguages.Ready(config)) { Fail("Install multilingual speech in Preferences > Storage > Optional features, or select English."); return; }
            if (handsFree != null || handsFreeStarting) StopHandsFree("Paused for shortcut dictation.");
            if (config.AccurateDictationEnabled && !prewarming && !AccurateSpeech.Ready) Prewarm();
            target = Native.GetForegroundWindow();
            historyStarted = DateTime.UtcNow; historyPlatform = ActivityHistory.ForegroundApp(target); historyWindowTitle = Native.WindowTitle(target);
            sessionConfig = WritingProfiles.Resolve(config, historyPlatform);
            sessionLiveWriting = config.LiveWriting && !(config.CleanupEnabled && config.RichSnippetPaste);
            sessionAccurate = config.SpeechLanguage == "en" && config.AccurateDictationEnabled; lastAccurateBytes = 0;
            try
            {
                recorder = new MicRecorder(config.Microphone);
                recorder.Start();
            }
            catch (Exception ex) { if (recorder != null) recorder.Dispose(); recorder = null; Fail("Microphone problem: " + ex.Message); return; }
            state.Move(AppState.Recording); generation++; if (liveRecognition != null) liveRecognition.Dispose(); liveRecognition = new LiveRecognition(sessionAccurate || config.SpeechLanguage != "en" ? sessionConfig : null); liveInserted = ""; liveInterrupted = mouseHook == IntPtr.Zero; liveTimer.Start();
            DisplayVoiceIndicator("● Recording from " + recorder.DeviceName + (recorder.FellBack ? " (saved mic not found — using default)" : "") + " — release " + config.Hotkey, Color.FromArgb(190, 45, 45));
        }

        void LiveTick()
        {
            if (state.Current != AppState.Recording || recorder == null || sessionAccurate && !AccurateSpeech.Ready || Interlocked.CompareExchange(ref liveBusy, 1, 0) != 0) return;
            byte[] pcm = recorder.Snapshot(); int peak = recorder.Peak; int gen = generation; var cfg = sessionConfig; var recognition = liveRecognition;
            if (pcm.Length < 8000 || peak < 150) { Interlocked.Exchange(ref liveBusy, 0); return; }
            if (sessionAccurate && pcm.Length - lastAccurateBytes < 16000) { Interlocked.Exchange(ref liveBusy, 0); return; }
            lastAccurateBytes = pcm.Length;
            liveIdle.Reset();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    string text = recognition.Snapshot(pcm, peak, false);
                    string formatted = WritingFormat.Compose(cfg, text.Trim());
                    if (!overlay.IsDisposed) PostUi(() => { if (gen == generation && state.Current == AppState.Recording && sessionLiveWriting) ApplyLiveText(formatted); });
                }
                catch { } // The complete recording still goes through normal recovery on release.
                finally { Interlocked.Exchange(ref liveBusy, 0); liveIdle.Set(); }
            });
        }
        bool ApplyLiveText(string text)
        {
            if (hub != null && !hub.IsDisposed && target == hub.Handle) { hub.UpdateLiveTranscript(text); return true; }
            if (!config.AutoPaste || liveInterrupted) return false;
            if (Native.GetForegroundWindow() != target) { liveInterrupted = true; return false; }
            if (Native.ModifiersDown()) return false;
            var edit = LiveEdit.Between(liveInserted, text);
            if (!Native.WriteLive(target, edit)) { liveInterrupted = true; return false; }
            liveInserted = text; return true;
        }
        void StopRecording()
        {
            if (state.Current != AppState.Recording) return;
            liveTimer.Stop();
            byte[] pcm = recorder.Stop();
            historyCaptureSeconds = pcm.Length / 32000.0; historyProcessingStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            int peak = recorder.Peak;
            recorder.Dispose(); recorder = null;
            recovery.Audio = (byte[])pcm.Clone(); recovery.Text = liveInserted; recovery.Message = "Captured speech can be retried.";
            if (pcm.Length < 16000 * 2 / 3 || !sessionAccurate && peak < 150) { Cancel(); return; }
            state.Move(AppState.Processing);
            DisplayVoiceIndicator("Transcribing locally…", Color.FromArgb(50, 90, 170));
            int gen = ++generation;
            var cfg = sessionConfig; var recognition = liveRecognition;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    if (!liveIdle.WaitOne(12000)) throw new SttException("Local speech did not finish. Open Recover last dictation to keep or retry the captured audio.");
                    string raw = recognition != null ? recognition.Snapshot(pcm, peak, true) : SpeechEngine.Transcribe(cfg, MicRecorder.ToWav(MicRecorder.Normalize(pcm, peak)));
                    if (recognition != null) recognition.Dispose();
                    string finalText = WritingFormat.Compose(cfg, raw);
                    PostUi(() => { if (!exiting && gen == generation) Finish(raw, finalText); });
                }
                catch (Exception ex) { PostUi(() => { if (gen == generation) Fail(ex.Message); }); }
            });
        }

        void Finish(string raw, string refined)
        {
            if (state.Current != AppState.Processing) return;
            string text = refined;
            recovery.Text = text;
            lastRaw = raw; lastCleaned = text;
            if (String.IsNullOrWhiteSpace(text)) { Cancel(); return; }
            try { if (hub == null || hub.IsDisposed || target != hub.Handle) ActivityHistory.Record(new NoteStore(), raw, text, historyPlatform, historyStarted, historyWindowTitle, historyCaptureSeconds, (System.Diagnostics.Stopwatch.GetTimestamp() - historyProcessingStarted) / (double)System.Diagnostics.Stopwatch.Frequency); }
            catch (Exception ex) { ReportIssue("Dictation history could not be saved: " + ex.Message); }
            if (hub != null && !hub.IsDisposed)
            {
                hub.UpdateTranscript(raw, text);
                if (target == hub.Handle) { state.Move(AppState.Idle); HideVoiceIndicator(); return; }
            }
            if (config.AutoPaste)
            {
                var formattedSnippet = RichSnippets.Formatted(sessionConfig ?? config, raw);
                if (formattedSnippet != null)
                {
                    try { if (liveInterrupted || !RichSnippets.Paste(sessionConfig, target, formattedSnippet)) throw new InvalidOperationException("Formatted insertion was interrupted. Recover the text before trying again."); state.Move(AppState.Idle); HideVoiceIndicator(); recovery.Discard(); return; }
                    catch (Exception ex) { Fail(ex.Message); return; }
                }
                if (ApplyLiveText(text)) { state.Move(AppState.Idle); HideVoiceIndicator(); recovery.Discard(); return; }
                Fail("Text is ready but typing was interrupted. Open Recover last dictation to review and copy it."); return;
            }
            try { Clipboard.SetDataObject(text, true, 5, 100); }
            catch (Exception ex) { Fail("Could not copy transcript: " + ex.Message); return; }
            state.Move(AppState.Idle); recovery.Message = "Text copied. You can review it from the tray.";
            UpdateDock();
            overlay.Display("Copied to clipboard", Color.FromArgb(45, 120, 75)); HideSoon();
        }
        void ShowRecovery()
        {
            if (!recovery.HasWork) { tray.ShowBalloonTip(3000, "Notewll", "There is no unfinished dictation to recover.", ToolTipIcon.Info); return; }
            if (recoveryWindow != null && !recoveryWindow.IsDisposed) { recoveryWindow.Activate(); return; }
            recoveryWindow = new RecoveryForm(recovery, done =>
            {
                if (state.Current == AppState.Recording || state.Current == AppState.Processing) { recovery.Message = "Finish the active dictation first."; done(); return; }
                var original = recovery.Audio; var audio = original == null ? null : (byte[])original.Clone(); if (audio == null || recovering) { done(); return; } int revision = recovery.Revision; recovering = true;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { string recognized = DictationSpeech.Transcribe(config, audio); string recovered = SmartCleanup.Complete(config, recognized, 4000); if (revision == recovery.Revision) { recovery.Text = recovered; recovery.Message = "Recovered. Copy only what you need."; } }
                    catch (Exception ex) { if (revision == recovery.Revision) recovery.Message = ex.Message; }
                    finally { recovering = false; Array.Clear(audio, 0, audio.Length); done(); }
                });
            }); recoveryWindow.Show();
        }
        void ShowActionMenu()
        {
            var menu = DialogUi.PopupMenu(); menu.Items.Add("Dictate / stop", null, (s, e) => { if (state.Current == AppState.Recording) StopRecording(); else StartRecording(); });
            menu.Items.Add("Cancel dictation", null, (s, e) => Cancel()); menu.Items.Add("Voice note", null, (s, e) => ToggleNotes("Microphone", "Note"));
            menu.Items.Add("Online meeting", null, (s, e) => ToggleNotes(config.MeetingSource == "Computer" ? "Computer" : "Both", "Meeting")); menu.Items.Add("Open app", null, (s, e) => ShowHub("Home")); menu.Show(Cursor.Position);
        }

        void Cancel()
        {
            liveTimer.Stop(); generation++; if (liveRecognition != null) liveRecognition.Dispose();
            if (liveInserted.Length > 0 && !liveInterrupted) ApplyLiveText("");
            if (state.Current == AppState.Recording)
            {
                if (recorder != null) recorder.Dispose(); recorder = null; state.Move(AppState.Idle);
            }
            else if (state.Current == AppState.Processing) { generation++; state.Move(AppState.Idle); }
            else return;
            recovery.Discard(); HideVoiceIndicator();
        }

        void Fail(string message)
        {
            liveTimer.Stop();
            ReportIssue(message);
            if (state.Current != AppState.Error) state.Move(AppState.Error);
            recovery.Message = message + " Open Recover last dictation from the tray.";
            if (hub != null && !hub.IsDisposed) hub.SetReadiness(recovery.Message);
            UpdateDock();
            overlay.Display(message, Color.FromArgb(180, 90, 20));
            tray.ShowBalloonTip(5000, "Notewll", message, ToolTipIcon.Warning);
            var timer = new System.Windows.Forms.Timer { Interval = 6000 };
            timer.Tick += (s, e) => { timer.Stop(); if (state.Current == AppState.Error) { state.Move(AppState.Idle); HideVoiceIndicator(); } };
            timer.Start();
        }

        void HideSoon()
        {
            var timer = new System.Windows.Forms.Timer { Interval = 1200 };
            timer.Tick += (s, e) => { timer.Stop(); if (state.Current == AppState.Idle) overlay.Hide(); };
            timer.Start();
        }

        IntPtr KeyboardHook(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0)
            {
                int vk = (int)Marshal.PtrToStructure<Native.Kbd>(lParam).vkCode;
                var data = Marshal.PtrToStructure<Native.Kbd>(lParam);
                if ((data.flags & 0x10) != 0) return Native.CallNextHookEx(hook, code, wParam, lParam);
                bool down = wParam == (IntPtr)Native.WM_KEYDOWN || wParam == (IntPtr)Native.WM_SYSKEYDOWN;
                bool up = wParam == (IntPtr)Native.WM_KEYUP || wParam == (IntPtr)Native.WM_SYSKEYUP;
                if (down || up) Interlocked.Increment(ref handsFreeInputEpoch);
                if (vk == Native.VK_ESCAPE && down && (handsFree != null || handsFreeStarting)) { PostUi(() => StopHandsFree()); return (IntPtr)1; }
                if (up && bookmarkKeysDown.Remove(vk)) return (IntPtr)1;
                if (down && vk >= 0x31 && vk <= 0x33 && (Native.GetAsyncKeyState(0x11) & 0x8000) != 0 && (Native.GetAsyncKeyState(0x12) & 0x8000) != 0 && hub != null && hub.IsNoteRecording)
                { if (bookmarkKeysDown.Add(vk)) { string kind = new[] { "Important", "Question", "Follow up" }[vk - 0x31]; PostUi(() => { if (hub != null && hub.Notes != null) hub.Notes.AddBookmark(kind); }); } return (IntPtr)1; }
                if (hub != null && hub.IsCapturingShortcut)
                {
                    if (down || up) hub.CaptureShortcutKey(vk, down);
                    shortcutTracker.Reset(); noteTracker.Reset(); meetingTracker.Reset(); return (IntPtr)1;
                }
                if ((state.Current == AppState.Recording || state.Current == AppState.Processing) && down && !shortcutKeys.Contains(Shortcut.Normalize(vk)) && vk != Native.VK_ESCAPE) liveInterrupted = true;
                int transition;
                bool consume = shortcutTracker.Feed(shortcutKeys, vk, down, out transition);
                if (transition == 1) PostUi(StartRecording);
                if (transition == -1) PostUi(StopRecording);
                int noteTransition; consume |= noteTracker.Feed(noteBinding, vk, down, out noteTransition);
                if (noteTransition == 1) PostUi(() => ToggleNotes("Microphone", "Note"));
                int meetingTransition; consume |= meetingTracker.Feed(meetingBinding, vk, down, out meetingTransition);
                if (meetingTransition == 1) PostUi(() => ToggleNotes(config.MeetingSource == "Computer" ? "Computer" : "Both", "Meeting"));
                if (consume) return (IntPtr)1;
                if (vk == Native.VK_ESCAPE && down && (state.Current == AppState.Recording || state.Current == AppState.Processing)) { PostUi(Cancel); return (IntPtr)1; }
            }
            return Native.CallNextHookEx(hook, code, wParam, lParam);
        }

        void ShowSettings()
        {
            ShowHub("Preferences");
        }

        void EnsureHub()
        {
            if (hub == null || hub.IsDisposed)
            {
                hub = new SettingsForm(config, lastRaw, lastCleaned) { CloseToTray = true };
                hub.SetIssues(issues); hub.DismissIssues = () => { issues.Clear(); hub.SetIssues(issues); };
                hub.Notes.IssueReported = ReportIssue;
                hub.CanTest = () => { if (handsFree != null || handsFreeStarting) StopHandsFree("Paused for a recording or microphone test."); return state.Current == AppState.Idle && VoiceEnrollmentDialog.OpenCount == 0; };
                hub.ToggleHandsFree = ToggleHandsFree; hub.PauseHandsFree = () => StopHandsFree();
                hub.SetHandsFreeStatus(handsFree != null || handsFreeStarting, handsFreeMessage);
                hub.OnSaved = next =>
                {
                    if (state.Current != AppState.Idle || hub.IsTesting) throw new InvalidOperationException("Finish dictating before saving preferences.");
                    if (handsFree != null || handsFreeStarting) StopHandsFree("Preferences changed. Start hands-free again when ready.");
                    if (next.StartWithWindows || next.StartWithWindows != Startup.Enabled) Startup.Set(next.StartWithWindows);
                    next.Save();
                    bool speechChanged = next.AccurateDictationEnabled != config.AccurateDictationEnabled || next.QualityPython != config.QualityPython || next.SpeechLanguage != config.SpeechLanguage;
                    config = next; overlay.ReducedMotion = config.ReducedMotion; dock.Configure(config.ReducedMotion); dock.ConfigureBall(config); dock.SetVisibility(config.ShowToolbar); SetShortcutBindings();
                    if (speechChanged) Prewarm();
                    tray.Text = "Notewll - hold " + config.Hotkey;
                };
            }
        }
        void ShowHub(string page)
        {
            if (state.Current != AppState.Idle) return;
            EnsureHub();
            hub.OpenRecovery = ShowRecovery;
            hub.SetReadiness(engineReadiness); hub.ShowPage(page); hub.Show(); if (hub.WindowState == FormWindowState.Minimized) hub.WindowState = FormWindowState.Normal; hub.Activate();
            if (!config.SetupGuideSeen && !setupShown) { setupShown = true; hub.ShowFirstRunGuide(); }
        }

        protected override void ExitThreadCore()
        {
            Application.Idle -= WarmWhenUiIdle;
            MicRecorder.ListeningChanged -= MicrophoneListeningChanged;
            Microsoft.Win32.SystemEvents.SessionSwitch -= SessionChangedForHandsFree;
            calendarTimer.Stop(); calendarTimer.Dispose();
            exiting = true; StopHandsFree(); ThreadPool.QueueUserWorkItem(_ => MyVoiceMatcher.Shutdown()); generation++; liveTimer.Stop();
            if (recorder != null) { var tail = recorder.Stop(); recovery.Audio = tail; }
            if (recoveryWindow != null) recoveryWindow.Dispose();
            dock.Close(); dock.Dispose();
            liveTimer.Stop(); liveTimer.Dispose();
            if (mouseHook != IntPtr.Zero) Native.UnhookWindowsHookEx(mouseHook);
            noteStatus.Stop(); noteStatus.Dispose();
            if (exitWait != null) exitWait.Unregister(null); if (exitEvent != null) exitEvent.Dispose();
            if (showHubWait != null) showHubWait.Unregister(null);
            if (showHubEvent != null) showHubEvent.Dispose();
            if (hub != null) { hub.CloseToTray = false; hub.Dispose(); }
            if (recorder != null) recorder.Dispose();
            if (liveRecognition != null) liveRecognition.Dispose(); SpeechEngine.Shutdown(); ThreadPool.QueueUserWorkItem(_ => SpeechQuality.Shutdown()); ThreadPool.QueueUserWorkItem(_ => AccurateSpeech.Shutdown());
            if (hook != IntPtr.Zero) Native.UnhookWindowsHookEx(hook);
            tray.Visible = false; tray.Dispose(); overlay.Close(); base.ExitThreadCore();
        }
    }

}
