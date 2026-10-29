using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace Notewll
{
    sealed partial class NotesPane
    {
        Panel editorHost, notesBrowser, browserHeader, browserContent, browserSearchFrame;
        BufferedScrollPanel browserList;
        HubButton backToLibrary, browserClear;
        TextBox browserSearch;
        Label browserHeading, browserCaption, browserCount;
        LibraryFilter[] browserFilters;
        int browserKind;
        bool browsingNotes = true, browserLayout;
        float BrowserScale { get { using (var g = CreateGraphics()) return g.DpiX / 96f; } }
        static Label BrowserLabel(string text, float size, bool bold = false)
        { return new Label { Text = text, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = bold ? ThemeColors.Ink : ThemeColors.Muted, BackColor = ThemeColors.Card, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, UseMnemonic = false }; }
        void BuildNotesBrowser(TextBox search)
        {
            backToLibrary = new HubButton { Text = "Back to history", Height = 40, AccessibleName = "Back to history" };
            backToLibrary.Click += (s, e) => { if (!Busy) ShowLibrary(); else MessageBox.Show(FindForm(), "Finish this recording or transcription before returning to history."); };
            editorHost.Controls.Add(backToLibrary); BuildPersonalNoteEditor();
            notesBrowser = new Panel { Dock = DockStyle.Fill, BackColor = ThemeColors.Paper, AccessibleName = "Notes history browser" }; Controls.Add(notesBrowser);
            browserHeader = new Panel { Dock = DockStyle.Top, BackColor = ThemeColors.Paper }; browserList = new BufferedScrollPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = ThemeColors.Paper, AccessibleName = "History grouped by date" };
            browserContent = new Panel { BackColor = ThemeColors.Paper }; browserList.Controls.Add(browserContent); notesBrowser.Controls.Add(browserList); notesBrowser.Controls.Add(browserHeader);
            browserHeading = BrowserLabel("Your history", 20, true); browserCaption = BrowserLabel("Dictated text, personal notes and recorded conversations.", 9); browserHeader.Controls.AddRange(new Control[] { browserHeading, browserCaption });
            browserFilters = new[] { "All", "Dictations", "Notes", "Conversations", "Meetings" }.Select((text, i) => new LibraryFilter { Text = text, Selected = i == 0, AccessibleName = text }).ToArray(); browserHeader.Controls.AddRange(browserFilters);
            for (int i = 0; i < browserFilters.Length; i++) { int at = i; browserFilters[i].Click += (s, e) => { browserKind = at; foreach (var button in browserFilters) { button.Selected = button == browserFilters[at]; button.Invalidate(); } Refilter(); }; }
            browserSearch = search;
            browserSearchFrame = new SearchField((ThemedTextBox)browserSearch, "Search your history...") { AccessibleName = "History search field" };
            browserHeader.Controls.Add(browserSearchFrame);
            browserClear = new HubButton { Text = "Clear history..." }; browserClear.Click += (s, e) => { if (ClearHistory != null) ClearHistory(); }; browserHeader.Controls.Add(browserClear);
            browserCount = BrowserLabel("", 9); browserHeader.Controls.Add(browserCount);
            browserHeading.BackColor = browserCaption.BackColor = browserCount.BackColor = ThemeColors.Paper;
            notesBrowser.SizeChanged += (s, e) => LayoutNotesBrowser(); browserList.SizeChanged += (s, e) => LayoutNotesBrowser();
            LayoutNotesBrowser(); RenderNotesBrowser(); ApplyNotesView();
        }
        internal void ShowLibrary()
        {
            if (Busy) { browsingNotes = false; ApplyNotesView(); return; }
            browsingNotes = true; ReloadLibrary(); ApplyNotesView();
        }
        void ApplyNotesView()
        {
            if (notesBrowser == null) return;
            LayoutNoteEditors();
            notesBrowser.Visible = browsingNotes; editorHost.Visible = !browsingNotes;
            if (browsingNotes) notesBrowser.BringToFront(); else editorHost.BringToFront();
        }
        void LayoutNotesBrowser()
        {
            if (notesBrowser == null || browserLayout) return; browserLayout = true;
            try
            {
                float scale = BrowserScale; int pad = (int)(18 * scale), gap = (int)(10 * scale), width = Math.Max(200, notesBrowser.ClientSize.Width - pad * 2);
                browserHeader.Height = (int)(204 * scale);
                int clearWidth = (int)(150 * scale);
                browserHeading.SetBounds(pad, (int)(8 * scale), width - clearWidth - gap, (int)(38 * scale));
                browserClear.SetBounds(pad + width - clearWidth, (int)(10 * scale), clearWidth, (int)(34 * scale));
                browserCaption.SetBounds(pad, (int)(48 * scale), width, (int)(24 * scale));
                int filterWidth = (width - gap * (browserFilters.Length - 1)) / browserFilters.Length; for (int i = 0; i < browserFilters.Length; i++) browserFilters[i].SetBounds(pad + i * (filterWidth + gap), (int)(78 * scale), filterWidth, (int)(34 * scale));
                browserSearchFrame.SetBounds(pad, (int)(126 * scale), width, (int)(SearchField.FieldHeight * scale));
                browserCount.SetBounds(pad, (int)(172 * scale), width, (int)(22 * scale));
                var offset = browserList.AutoScrollPosition; int contentWidth = Math.Max(100, browserList.ClientSize.Width - pad * 2), y = 0;
                browserContent.SuspendLayout();
                foreach (Control child in browserContent.Controls) { bool row = child.Tag is NoteDocument; int height = (int)((row ? 68 : child.Tag as string == "Empty" ? 130 : 38) * scale); child.SetBounds(0, y, contentWidth, height); y += height + (row ? (int)(8 * scale) : 0); }
                browserContent.SetBounds(pad + offset.X, offset.Y, contentWidth, y); browserContent.ResumeLayout();
            }
            finally { browserLayout = false; }
        }
        void RenderNotesBrowser()
        {
            if (browserContent == null) return;
            foreach (Control child in browserContent.Controls.Cast<Control>().ToArray()) child.Dispose();
            var entries = history.Items.Cast<NoteDocument>().OrderByDescending(ActivityHistory.LocalDate).Take(500).ToArray();
            foreach (var day in entries.GroupBy(n => ActivityHistory.LocalDate(n).Date))
            {
                string title = day.Key == DateTime.Today ? "Today" : day.Key == DateTime.Today.AddDays(-1) ? "Yesterday" : day.Key.ToString("dddd");
                var heading = BrowserLabel(title + "  \u00b7  " + day.Key.ToString("MMMM d, yyyy"), 9, true); heading.BackColor = ThemeColors.Paper; heading.Padding = new Padding((int)(12 * BrowserScale), 0, 0, 0); browserContent.Controls.Add(heading);
                foreach (var note in day) browserContent.Controls.Add(BrowserEntry(note));
            }
            if (entries.Length == 0)
            {
                string title = String.IsNullOrWhiteSpace(libraryQuery) ? new[] { "No history yet", "No dictations yet", "No notes yet", "No conversations yet", "No meetings yet" }[browserKind] : "No matching entries";
                var empty = BrowserLabel(title + Environment.NewLine + (String.IsNullOrWhiteSpace(libraryQuery) ? "Your saved entries will appear here." : "Try another search or category."), 11);
                empty.Tag = "Empty"; empty.TextAlign = ContentAlignment.MiddleCenter; empty.BackColor = ThemeColors.Paper; browserContent.Controls.Add(empty);
            }
            browserCount.Text = history.Items.Count + (history.Items.Count == 1 ? " entry" : " entries") + " \u00b7 Newest first" + (history.Items.Count > 500 ? " \u00b7 Showing latest 500; search to narrow results" : "");
            browserCaption.Text = new[] { "Dictations, voice notes, conversations and online meetings.", "Text dictated into other apps.", "Voice notes and notes you wrote.", "Microphone conversations with reviewable speaker labels.", "Calls recorded from computer audio and microphone." }[browserKind];
            for (int i = 0; i < browserFilters.Length; i++) { browserFilters[i].Count = i == 0 ? documents.Count : documents.Count(n => ActivityHistory.Kind(n) == new[] { "", "Dictation", "Note", "Conversation", "Meeting" }[i]); browserFilters[i].Invalidate(); }
            LayoutNotesBrowser();
        }
        Control BrowserEntry(NoteDocument note)
        {
            float scale = BrowserScale;
            var shell = new Surface { Tag = note, Padding = new Padding((int)(12 * scale), (int)(10 * scale), (int)(12 * scale), (int)(10 * scale)), AccessibleName = ActivityHistory.LocalDate(note).ToString("HH:mm") + " " + ActivityHistory.Kind(note) + " " + note.Title };
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = ThemeColors.Card, Margin = Padding.Empty };
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64 * scale)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94 * scale)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34 * scale));
            var time = BrowserLabel(ActivityHistory.LocalDate(note).ToString("HH:mm"), 9); time.Dock = DockStyle.Fill;
            string text = ActivityHistory.Text(note); if (String.IsNullOrWhiteSpace(text)) text = note.Title;
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = note.Kind == "Dictation" ? 1 : 2, Margin = Padding.Empty, BackColor = ThemeColors.Card };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); content.RowStyles.Add(new RowStyle(SizeType.Percent, note.Kind == "Dictation" ? 100 : 50)); if (note.Kind != "Dictation") content.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            var preview = new LinkLabel { Text = (note.Kind == "Dictation" ? text : note.Title).Replace("\r", " ").Replace("\n", " "), Font = new Font("Segoe UI", 10, note.Kind == "Dictation" ? FontStyle.Regular : FontStyle.Bold), Dock = DockStyle.Fill, LinkBehavior = LinkBehavior.NeverUnderline, LinkColor = ThemeColors.Ink, ActiveLinkColor = ThemeColors.Accent, VisitedLinkColor = ThemeColors.Ink, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, UseMnemonic = false, AccessibleName = "Open " + note.Title, Margin = Padding.Empty };
            preview.LinkClicked += (s, e) => SelectNote(note.Id); reviewHints.SetToolTip(preview, text); content.Controls.Add(preview, 0, 0);
            if (note.Kind != "Dictation") { string excerpt = ActivityHistory.Text(note); if (String.IsNullOrWhiteSpace(excerpt)) excerpt = note.Kind == "Meeting" || note.Kind == "Conversation" ? "Open conversation" : "Open note"; var summary = BrowserLabel(excerpt.Replace("\r", " ").Replace("\n", " "), 8.5f); summary.Dock = DockStyle.Fill; summary.Margin = Padding.Empty; summary.Click += (s, e) => SelectNote(note.Id); summary.Cursor = Cursors.Hand; content.Controls.Add(summary, 0, 1); }
            var type = new LibraryBadge { Text = ActivityHistory.Kind(note), Dock = DockStyle.Fill, Margin = new Padding((int)(5 * scale), (int)(12 * scale), (int)(5 * scale), (int)(12 * scale)) };
            var more = new HubButton { Text = "\u22ef", Dock = DockStyle.Fill, AccessibleName = "Options for " + note.Title, Margin = new Padding(0, (int)(8 * scale), 0, (int)(8 * scale)) };
            more.Click += (s, e) => ShowBrowserEntryMenu(note, more);
            row.Controls.Add(time, 0, 0); row.Controls.Add(content, 1, 0); row.Controls.Add(type, 2, 0); row.Controls.Add(more, 3, 0); shell.Controls.Add(row); return shell;
        }
        internal ContextMenuStrip ShowBrowserEntryMenu(NoteDocument note, Control anchor)
        {
            var menu = DialogUi.PopupMenu();
            menu.Items.Add("Open", null, (s, e) => SelectNote(note.Id));
            menu.Items.Add("Project and tags...", null, (s, e) => { SelectNote(note.Id); EditDetails(); });
            menu.Items.Add("Info", null, (s, e) => NotesEntryInfo.Show(FindForm(), store, note));
            menu.Items.Add("Copy text", null, (s, e) => { try { Clipboard.SetDataObject(ActivityHistory.Text(note), true, 5, 100); } catch (Exception ex) { MessageBox.Show(FindForm(), ex.Message); } });
            menu.Items.Add(new ToolStripSeparator());
            var remove = menu.Items.Add("Delete", null, (s, e) => { Select(note); DeleteSelected(); RenderNotesBrowser(); }); remove.Enabled = !Busy;
            menu.Show(anchor, new Point(0, anchor.Height)); return menu;
        }

    }
}
