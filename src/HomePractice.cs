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
            homePractice = Card(pages[0], HomeEditorTop, 76); homePractice.AccessibleName = "Dictation editor on Home";
            practiceHeading = Label(homePractice, "Try Notewll", 22, 12, 460, 28, 12, true, Ink);
            practiceHint = Label(homePractice, "Speak into the editor, then review or copy your words.", 22, 44, 700, 22, 9, false, Muted);
            practiceToggle = Button(homePractice, "Open editor", 578, 14, 156, (s, e) => SetHomePractice(!practiceExpanded, !practiceExpanded)); practiceToggle.AccessibleName = "Expand or collapse dictation editor";
            Label(homePractice, "Your words appear below as you speak. Stop to finish the transcription.", 22, 96, 710, 28, 10, false, Muted);
            transcribe.SetBounds(22, 136, 180, 38); homePractice.Controls.Add(transcribe);
            Button(homePractice, "Cancel", 218, 136, 120, (s, e) => { bool active = testRecorder != null; StopTest(); if (active) UpdateTranscript(practiceRawPrefix, practiceCleanPrefix); sttStatus.Text = "Recording canceled. Your previous text is still here."; });
            Button(homePractice, "Open audio file...", 354, 136, 170, (s, e) => TestAudioFile());
            rawHeading = Label(homePractice, "RAW TEXT", 24, 200, 325, 24, 8, true, Muted);
            cleanedHeading = Label(homePractice, "MODIFIED TEXT", 388, 200, 325, 24, 8, true, Muted);
            raw.SetBounds(24, 240, 325, 250); cleaned.SetBounds(388, 240, 325, 250); raw.AccessibleName = "Raw text"; cleaned.AccessibleName = "Modified text"; homePractice.Controls.AddRange(new Control[] { raw, cleaned });
            copy.SetBounds(22, 514, 112, 38); homePractice.Controls.Add(copy);
            Button(homePractice, "Apply cleanup", 520, 514, 210, (s, e) => ApplyPracticeCorrection()); Button(homePractice, "Clear", 150, 514, 90, (s, e) => UpdateTranscript("", "")); Button(homePractice, "Remember a correction", 260, 514, 240, (s, e) => RememberCorrection());
            sttStatus.SetBounds(22, 614, 710, 52); sttStatus.Text = "Ready. Dictation in this editor stays here until you copy or clear it."; homePractice.Controls.Add(sttStatus);
            Button(homePractice, "Edit by voice", 22, 558, 210, (s, e) => {
                string before = cleaned.Text; int start = cleaned.SelectionStart, length = cleaned.SelectionLength;
                string selected = length > 0 ? cleaned.SelectedText : before;
                LocalCommandDialog.Show(this, PracticeConfig(), selected, value => {
                    if (cleaned.Text != before) throw new InvalidOperationException("The text changed while editing. Copy your proposed edit and try again.");
                    cleaned.Text = length > 0 ? before.Substring(0, start) + value + before.Substring(start + length) : value;
                    OfferCorrection(before, cleaned.Text);
                }, CanTest);
            });
            foreach (Control control in homePractice.Controls) control.Tag = control.Bounds;
            practiceBody = homePractice.Controls.Cast<Control>().Where(control => control != practiceHeading && control != practiceHint && control != practiceToggle).ToArray();
            foreach (var control in practiceBody) control.Visible = false;
        }
        void SetHomePractice(bool expanded, bool scrollTo = false)
        {
            if (homePractice == null) return;
            if (!expanded && (testRecorder != null || transcribing)) { bool active = testRecorder != null; StopTest(); if (active) UpdateTranscript(practiceRawPrefix, practiceCleanPrefix); sttStatus.Text = "Recording stopped. Open the editor to continue."; }
            practiceExpanded = expanded; practiceToggle.Text = expanded ? "Hide editor" : "Open editor";
            practiceToggle.AccessibleDescription = expanded ? "Expanded" : "Collapsed";
            foreach (var control in practiceBody) control.Visible = expanded;
            homePractice.Tag = new Rectangle(40, HomeEditorTop, 760, expanded ? 688 : 76);
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
            practiceHint.Width = Math.Max(1, practiceToggle.Left - pad * 2); sttStatus.Width = homePractice.ClientSize.Width - pad * 2;
            foreach (var label in homePractice.Controls.OfType<Label>().Where(label => label != practiceHint && (label.Anchor & AnchorStyles.Right) != 0)) label.Width = Math.Max(1, homePractice.ClientSize.Width - label.Left - pad);
            var actionNames = new[] { "Copy text", "Clear", "Remember a correction", "Apply cleanup" };
            var weights = new[] { 112, 90, 240, 170 }; int x = pad, gap = (int)(10 * uiScale), available = homePractice.ClientSize.Width - pad * 2 - gap * 3;
            for (int i = 0; i < actionNames.Length; i++) { var button = homePractice.Controls.OfType<HubButton>().FirstOrDefault(b => b == copy && i == 0 || b.Text == actionNames[i]); if (button == null) continue; button.SetBounds(x, (int)(514 * uiScale), available * weights[i] / weights.Sum(), (int)(38 * uiScale)); x = button.Right + gap; }
            LayoutTranscript(homePractice);
        }
    }
}
