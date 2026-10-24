using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LocalDictation
{
    sealed partial class DictationApp
    {
        HandsFreeSession handsFree;
        bool handsFreeStarting;
        int handsFreeRevision, handsFreeInputEpoch;
        string handsFreeMessage = "Hands-free paused. Start it in Preferences > Hands-free.";
        ToolStripMenuItem handsFreeMenu;
        void AddHandsFreeMenu(ContextMenuStrip menu)
        {
            handsFreeMenu = new ToolStripMenuItem("Start hands-free - my voice only");
            handsFreeMenu.Click += (s, e) => ToggleHandsFree(); menu.Items.Add(handsFreeMenu);
        }
        void RefreshHandsFree()
        {
            if (handsFreeMenu != null) handsFreeMenu.Text = handsFree != null || handsFreeStarting ? "Pause hands-free" : "Start hands-free - my voice only";
            if (hub != null && !hub.IsDisposed) hub.SetHandsFreeStatus(handsFree != null || handsFreeStarting, handsFreeMessage);
            UpdateDock();
        }
        HandsFreeTarget HandsFreeFocus() { var target = HandsFreeTarget.Capture(Volatile.Read(ref handsFreeInputEpoch)); return target != null && HandsFreeAllowed(config.HandsFreeApps, target.AppName) ? target : null; }
        internal static bool HandsFreeAllowed(string allowed, string app) { return (allowed ?? "").Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Any(name => String.Equals(System.IO.Path.GetFileNameWithoutExtension(name.Trim()), app, StringComparison.OrdinalIgnoreCase)); }
        string handsFreeDevice;
        void SessionChangedForHandsFree(object sender, Microsoft.Win32.SessionSwitchEventArgs e) { if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLock || e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLogoff) { Interlocked.Increment(ref handsFreeInputEpoch); PostUi(() => StopHandsFree("Screen locked. Hands-free paused; start it again when ready.")); } }
        void CheckHandsFreeDevice()
        {
            if (handsFree == null) return;
            try { if (CoreAudio.DefaultId(false) != handsFreeDevice || config.Microphone != Config.DefaultMic && !Mics.List().Contains(config.Microphone)) StopHandsFree("Microphone disconnected or changed. Hands-free paused."); }
            catch { StopHandsFree("Microphone unavailable. Hands-free paused."); }
        }
        async void ToggleHandsFree()
        {
            if (handsFree != null || handsFreeStarting) { StopHandsFree(); return; }
            if (state.Current != AppState.Idle || recovering || hub != null && hub.IsTesting) { ReportIssue("Finish the current recording or test before starting hands-free."); return; }
            int revision = ++handsFreeRevision;
            handsFreeStarting = true; handsFreeMessage = "Preparing local voice matching. The mic is not open yet."; RefreshHandsFree();
            try {
                if (String.IsNullOrWhiteSpace(config.HandsFreeApps)) throw new InvalidOperationException("Choose allowed apps in Hands-free preferences first.");
                handsFreeDevice = CoreAudio.DefaultId(false);
                var profile = MyVoiceProfile.Load(); var snapshot = config.Copy();
                await Task.Run(() => { MyVoiceMatcher.Ready(snapshot); string problem = SpeechEngine.EnsureRunning(snapshot, 30); if (problem != null) throw new InvalidOperationException(problem); });
                if (exiting || revision != handsFreeRevision) return;
                HandsFreeSession active = null;
                active = new HandsFreeSession(snapshot, profile, HandsFreeFocus, (phrase, text) => PostUi(() => {
                    if (!ReferenceEquals(handsFree, active) || revision != handsFreeRevision || state.Current != AppState.Idle) return;
                    if (!phrase.Target.Same(HandsFreeFocus()) || Native.ModifiersDown()) { handsFreeMessage = "Focus changed - phrase discarded. Listening for your next phrase."; RefreshHandsFree(); return; }
                    // One append only after verification. No UI Automation, focus stealing, clipboard or paste fallback.
                    if (!Native.WriteLive(phrase.Target.Window, new LiveEdit { Remove = 0, Append = text.Trim() + " " })) { StopHandsFree("Typing was blocked. Hands-free paused; no automatic paste was attempted."); return; }
                    lastRaw = lastCleaned = text;
                    if (hub != null && !hub.IsDisposed) hub.UpdateTranscript(text, text);
                    handsFreeMessage = "Listening - matched phrase typed. Click the ball or press Escape to pause."; RefreshHandsFree();
                }), (message, fatal) => PostUi(() => {
                    if (!ReferenceEquals(handsFree, active) || revision != handsFreeRevision) return;
                    if (fatal) StopHandsFree(message); else { handsFreeMessage = message; RefreshHandsFree(); }
                }));
                handsFree = active; active.Start(); handsFreeStarting = false;
                dock.SetVisibility(true);
                handsFreeMessage = "Listening - my voice only. Speak a full phrase, then pause. Escape stops the mic."; RefreshHandsFree();
            } catch (Exception ex) {
                if (revision != handsFreeRevision || exiting) return;
                StopHandsFree("Hands-free paused: " + ex.Message); ReportIssue(handsFreeMessage);
                EnsureHub(); hub.ShowPage("Preferences"); hub.SelectSettingsCategory("Hands-free"); hub.Show(); hub.Activate();
            }
        }
        void StopHandsFree(string message = "Hands-free paused. The microphone is off.")
        {
            ++handsFreeRevision; Interlocked.Increment(ref handsFreeInputEpoch); handsFreeStarting = false;
            var active = handsFree; handsFree = null;
            if (active != null) active.Dispose();
            handsFreeMessage = message;
            if (!exiting) { dock.SetVisibility(config.ShowToolbar); RefreshHandsFree(); }
        }
    }
}
