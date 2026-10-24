using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace LocalDictation
{
    sealed partial class NotesPane
    {
        void ShowExportDialog()
        {
            if (selected == null) { uiError = "Choose a saved note to export."; return; }
            if (Busy && ReferenceEquals(selected, session.Document)) { uiError = "Stop and save this recording before exporting it."; return; }
            SaveEdits(); var note = selected;
            using (var dialog = DialogUi.Create("Export a PDF snapshot", new Size(760, 650)))
            {
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
                for (int i = 0; i < 6; i++) layout.RowStyles.Add(new RowStyle(i == 4 ? SizeType.Percent : SizeType.AutoSize, i == 4 ? 100 : 0));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); dialog.Controls.Add(layout);
                layout.Controls.Add(DialogUi.Text("Export a PDF snapshot"), 0, 0);
                var choices = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1 };
                var scope = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Export content" };
                scope.Items.AddRange(new object[] { "Conversation only", "Personal notes only", "Full record" }); scope.SelectedIndex = ActivityHistory.Kind(note) == "Note" ? 1 : 0;
                var names = new CheckBox { Text = "Include participant and speaker labels", AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 10, 0, 8) };
                choices.Controls.Add(scope); choices.Controls.Add(names); layout.Controls.Add(choices, 0, 1);
                layout.Controls.Add(DialogUi.Text("Spoken names remain in the text. This choice only controls participant and speaker labels."), 0, 2);
                layout.Controls.Add(DialogUi.Text("Select text in the preview and choose Redact selected text. Only the exported PDF text changes. Audio is NOT redacted or included. Changing content choices resets redactions."), 0, 3);
                var preview = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, DetectUrls = false, WordWrap = true, AccessibleName = "PDF content preview", Margin = new Padding(0, 8, 0, 12) }; layout.Controls.Add(preview, 0, 4);
                Func<NoteDocument> snapshot = () => NoteExport.Scope(note, scope.SelectedIndex, names.Checked);
                Action refresh = () => preview.Text = DialogUi.Lines(NoteExport.PreviewText(snapshot()));
                scope.SelectedIndexChanged += (s, e) => refresh(); names.CheckedChanged += (s, e) => refresh();
                bool writingFile = false; dialog.FormClosing += (s, e) => { if (writingFile) e.Cancel = true; };
                var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = true };
                var save = DialogUi.Button("Save PDF...", async (s, e) =>
                {
                    using (var file = new SaveFileDialog { Filter = "PDF snapshot|*.pdf", DefaultExt = "pdf", AddExtension = true, FileName = String.Concat(note.Title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)) })
                    {
                        if (file.ShowDialog(dialog) != DialogResult.OK) return;
                        string approvedText = preview.Text; writingFile = true; var button = (Button)s; button.Enabled = false; button.Text = "Saving PDF...";
                        try { await Task.Run(() => NoteExport.SavePreview(approvedText, file.FileName)); button.Text = "Save another PDF"; if (MessageBox.Show(dialog, "PDF saved. Open it now?", "Export complete", MessageBoxButtons.YesNo) == DialogResult.Yes) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = file.FileName, UseShellExecute = true }); }
                        catch (Exception ex) { MessageBox.Show(dialog, ex.Message, "Export could not finish"); button.Text = "Retry PDF export"; }
                        finally { writingFile = false; button.Enabled = true; }
                    }
                }, true);
                footer.Controls.Add(DialogUi.Button("Redact selected text", (s, e) => { if (preview.SelectionLength == 0) return; int start = preview.SelectionStart; string redacted = preview.Text.Remove(start, preview.SelectionLength).Insert(start, "[REDACTED]"); preview.Text = redacted; preview.Select(start, 10); }));
                footer.Controls.Add(save); footer.Controls.Add(DialogUi.Button("Close", (s, e) => dialog.Close())); layout.Controls.Add(footer, 0, 5);
                refresh(); ThemeColors.Apply(dialog, config.Theme); dialog.ShowDialog(FindForm());
            }
        }
    }
}
