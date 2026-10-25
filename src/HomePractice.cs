using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace Notewll
{
    sealed partial class SettingsForm
    {
        Surface homePractice;
        HubButton practiceToggle;
        Label practiceHeading, practiceHint;
        Control[] practiceBody;
        bool practiceExpanded;
        void BuildHomePractice()
        {
            homePractice = Card(pages[0], 880, 76); homePractice.AccessibleName = "Try dictation on Home";
            practiceHeading = Label(homePractice, "Try dictation", 22, 12, 460, 28, 12, true, Ink);
            practiceHint = Label(homePractice, "A quick check of your microphone and recognized text.", 22, 44, 700, 22, 9, false, Muted);
            practiceToggle = Button(homePractice, "Try it", 578, 14, 156, (s, e) => SetHomePractice(!practiceExpanded, !practiceExpanded)); practiceToggle.AccessibleName = "Expand or collapse dictation practice";
            Label(homePractice, "Speak a sentence, stop, then review both versions below.", 22, 96, 710, 28, 10, false, Muted);
            transcribe.SetBounds(22, 136, 180, 38); homePractice.Controls.Add(transcribe);
            Button(homePractice, "Cancel test", 218, 136, 120, (s, e) => { StopTest(); sttStatus.Text = "Test canceled. No audio was saved."; });
            Button(homePractice, "Test audio file...", 354, 136, 170, (s, e) => TestAudioFile());
            rawHeading = Label(homePractice, "RAW TEXT", 24, 200, 325, 24, 8, true, Muted);
            cleanedHeading = Label(homePractice, "MODIFIED TEXT", 388, 200, 325, 24, 8, true, Muted);
            raw.SetBounds(24, 240, 325, 170); cleaned.SetBounds(388, 240, 325, 170); raw.AccessibleName = "Raw text"; cleaned.AccessibleName = "Modified text"; homePractice.Controls.AddRange(new Control[] { raw, cleaned });
            copy.SetBounds(22, 434, 112, 38); homePractice.Controls.Add(copy);
            Button(homePractice, "Apply cleanup", 520, 434, 210, (s, e) => ApplyPracticeCorrection()); Button(homePractice, "Clear", 150, 434, 90, (s, e) => UpdateTranscript("", "")); Button(homePractice, "Remember a correction", 260, 434, 240, (s, e) => RememberCorrection());
            sttStatus.SetBounds(22, 486, 710, 52); sttStatus.Text = "Ready for a quick test. Practice is not added to your history."; homePractice.Controls.Add(sttStatus);
            foreach (Control control in homePractice.Controls) control.Tag = control.Bounds;
            practiceBody = homePractice.Controls.Cast<Control>().Where(control => control != practiceHeading && control != practiceHint && control != practiceToggle).ToArray();
            foreach (var control in practiceBody) control.Visible = false;
        }
        void SetHomePractice(bool expanded, bool scrollTo = false)
        {
            if (homePractice == null) return;
            if (!expanded && (testRecorder != null || transcribing)) { StopTest(); sttStatus.Text = "Test stopped. Expand to try again."; }
            practiceExpanded = expanded; practiceToggle.Text = expanded ? "Hide test" : "Try it";
            practiceToggle.AccessibleDescription = expanded ? "Expanded" : "Collapsed";
            foreach (var control in practiceBody) control.Visible = expanded;
            homePractice.Tag = new Rectangle(40, 880, 760, expanded ? 560 : 76);
            LayoutPage(pages[0]);
            if (expanded && scrollTo) pages[0].ScrollControlIntoView(homePractice);
            homePractice.Invalidate(true);
        }
        void LayoutHomePractice()
        {
            if (homePractice == null || practiceBody == null) return;
            foreach (Control control in homePractice.Controls)
            {
                if (!(control.Tag is Rectangle)) continue; var bounds = (Rectangle)control.Tag;
                control.SetBounds((int)(bounds.X * uiScale), (int)(bounds.Y * uiScale), (int)(bounds.Width * uiScale), (int)(bounds.Height * uiScale));
            }
            int pad = (int)(22 * uiScale);
            practiceToggle.Left = homePractice.ClientSize.Width - practiceToggle.Width - pad;
            practiceHeading.Width = Math.Max(1, practiceToggle.Left - pad * 2);
            practiceHint.Width = sttStatus.Width = homePractice.ClientSize.Width - pad * 2;
            LayoutTranscript(homePractice);
        }
    }
}
