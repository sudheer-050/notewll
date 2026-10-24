using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace LocalDictation
{
    sealed partial class NotesPane
    {
        Surface reviewSheet, personalSheet;
        Control writtenOriginalHost;
        Panel personalBody;
        TextBox personalTitle;
        Label personalDate, personalSaveState;
        TableLayoutPanel personalLayout;
        bool personalLayoutRunning;
        bool reviewingConversation;
        bool PersonalEditing { get { return !reviewingConversation && !Busy && (selected != null ? ActivityHistory.Kind(selected) == "Note" : writingFresh); } }
        void BuildPersonalNoteEditor()
        {
            writtenOriginalHost = written.Parent;
            personalSheet = new Surface { Visible = false, AccessibleName = "Editable personal note" }; editorHost.Controls.Add(personalSheet);
            personalLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = ThemeColors.Card, Margin = Padding.Empty };
            personalLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            personalLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); personalLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); personalLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); personalLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); personalSheet.Controls.Add(personalLayout);
            personalTitle = new TextBox { Multiline = true, WordWrap = false, MaxLength = 180, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 20, FontStyle.Bold), BackColor = ThemeColors.Card, AccessibleName = "Edit personal note title", Margin = Padding.Empty };
            personalTitle.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; written.Focus(); } };
            personalTitle.TextChanged += (s, e) => {
                if (populating || !PersonalEditing) return;
                string value = personalTitle.Text.Replace("\r", " ").Replace("\n", " ").Trim(); title.Text = value;
                if (selected != null) { selected.AutoTitle = value.Length == 0; if (value.Length > 0) selected.Title = value; }
                Edited(s, e);
            };
            personalDate = BrowserLabel("A new note, just for you.", 9); personalDate.Dock = DockStyle.Fill; personalDate.Margin = Padding.Empty;
            personalBody = new Panel { Dock = DockStyle.Fill, BackColor = ThemeColors.Card, Margin = Padding.Empty };
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = Padding.Empty, BackColor = ThemeColors.Card };
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
            personalSaveState = BrowserLabel("Saved on this device", 9); personalSaveState.Dock = DockStyle.Fill; personalSaveState.AutoEllipsis = true;
            var save = new HubButton { Text = "Save", Dock = DockStyle.Fill, AccessibleName = "Save personal note" }; save.Click += (s, e) => { saveTimer.Stop(); SaveEdits(); };
            var copyNote = new HubButton { Text = "Copy", Dock = DockStyle.Fill }; copyNote.Click += (s, e) => { try { Clipboard.SetDataObject(written.Text, true, 5, 100); } catch (Exception ex) { personalSaveState.Text = "Could not copy: " + ex.Message; } };
            var exportNote = new HubButton { Text = "Export", Dock = DockStyle.Fill }; exportNote.Click += (s, e) => ShowExportDialog();
            footer.Controls.Add(personalSaveState, 0, 0); footer.Controls.Add(save, 1, 0); footer.Controls.Add(copyNote, 2, 0); footer.Controls.Add(exportNote, 3, 0);
            personalLayout.Controls.Add(personalTitle, 0, 0); personalLayout.Controls.Add(personalDate, 0, 1); personalLayout.Controls.Add(personalBody, 0, 2); personalLayout.Controls.Add(footer, 0, 3);
            editorHost.SizeChanged += (s, e) => LayoutNoteEditors();
        }
        void LayoutNoteEditors()
        {
            if (personalSheet == null || personalLayoutRunning) return; personalLayoutRunning = true;
            try
            {
                float scale = BrowserScale; int gap = (int)(50 * scale), pad = (int)(28 * scale);
                // Lay out Back and the sheet explicitly: Dock.Top previously covered the top of Dock.Fill.
                backToLibrary.SetBounds(0, 0, (int)(180 * scale), (int)(38 * scale));
                reviewSheet.Dock = personalSheet.Dock = DockStyle.None;
                var bounds = new Rectangle(0, gap, editorHost.ClientSize.Width, Math.Max(1, editorHost.ClientSize.Height - gap));
                reviewSheet.Bounds = personalSheet.Bounds = bounds;
                personalSheet.Padding = new Padding(pad);
                personalLayout.RowStyles[0].Height = (int)(60 * scale); personalLayout.RowStyles[1].Height = (int)(30 * scale); personalLayout.RowStyles[3].Height = (int)(44 * scale);
                personalBody.Padding = new Padding(0, (int)(12 * scale), 0, (int)(12 * scale));
                var footer = (TableLayoutPanel)personalLayout.GetControlFromPosition(0, 3); for (int i = 1; i < footer.ColumnStyles.Count; i++) footer.ColumnStyles[i].Width = (int)(80 * scale);
                bool personal = PersonalEditing;
                if (personal && written.Parent != personalBody) { personalBody.Controls.Add(written); written.Font = new Font("Segoe UI", 11); written.ReadOnly = false; }
                else if (!personal && written.Parent != writtenOriginalHost) { writtenOriginalHost.Controls.Add(written); written.Font = Font; }
                personalSheet.Visible = personal; reviewSheet.Visible = !personal;
                if (personal) { personalDate.Text = selected == null ? "New personal note" : ActivityHistory.LocalDate(selected).ToString("dddd, MMMM d, yyyy 'at' HH:mm"); }
            }
            finally { personalLayoutRunning = false; }
        }
        void RefreshPersonalSaveState()
        {
            if (personalSaveState == null || selected == null) return;
            personalSaveState.Text = "Saved on this device  /  " + ActivityHistory.Words(selected) + " words";
            if (ActivityHistory.Kind(selected) == "Note" && !personalTitle.Focused)
            {
                bool before = populating; populating = true; personalTitle.Text = selected.Title; title.Text = selected.Title; populating = before;
            }
        }
    }
}
