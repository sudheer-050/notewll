using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace Notewll
{
    sealed partial class NotesPane
    {
        internal void ShowRoadmapPage() { reviewingConversation = true; browsingNotes = false; ApplyNotesView(); tabs.SelectedIndex = 3; }
        public void AddBookmark(string kind)
        {
            if (!Recording || session.Paused) return;
            try { lock (session.Document) { session.Document.Bookmarks.Add(new NoteBookmark { Kind = kind, Seconds = session.ElapsedSeconds }); store.Save(session.Document); } uiError = "Bookmark saved: " + kind; }
            catch (Exception ex) { uiError = ex.Message; }
        }
        void Jump(ConversationHit hit)
        {
            if (Busy) { MessageBox.Show(FindForm(), "Finish recording before opening a source passage."); return; }
            SelectNote(hit.Note.Id);
            if (hit.Segment < 0) { tabs.SelectedIndex = 0; return; }
            reviewingConversation = true; ApplyNotesView(); tabs.SelectedIndex = 1; audioSource.SelectedItem = hit.Note.Segments[hit.Segment].Source;
            string stamp = "[" + TimeSpan.FromSeconds(hit.Seconds).ToString(@"hh\:mm\:ss") + "]";
            int at = transcript.Text.IndexOf(stamp, StringComparison.Ordinal); if (at >= 0) { transcript.Select(at, stamp.Length); transcript.ScrollToCaret(); }
            StartPlayback((int)Math.Min(Int32.MaxValue, hit.Seconds * 1000));
        }
        void BuildConversationTools()
        {
            var page = new TabPage("Find & act"); tabs.TabPages.Add(page);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); page.Controls.Add(layout);
            layout.Controls.Add(DialogUi.Text("Find supporting passages across saved conversations. Double-click a result to open its source. Suggested actions need your confirmation."), 0, 0);
            var searchRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            var query = new ThemedTextBox { Width = 260, AccessibleName = "Search by meaning" };
            var results = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true, IntegralHeight = false, AccessibleName = "Source passages" };
            var search = DialogUi.Button("Search by meaning", async (s, e) => {
                if (String.IsNullOrWhiteSpace(query.Text) || Busy) return; SaveEdits(); searchRow.Enabled = false;
                try { var passages = ConversationTools.Passages(documents.Where(n => ConversationTools.Matches(n, libraryQuery, n.Title + " " + n.WrittenNotes + " " + NoteStore.Transcript(n)))); string question = query.Text; var found = await Task.Run(() => SemanticSearch.Find(passages, question)); if (!IsDisposed) { results.Items.Clear(); results.Items.AddRange(found.Cast<object>().ToArray()); if (found.Count == 0) MessageBox.Show(FindForm(), "No supporting passages found. Try another wording."); } }
                catch (Exception ex) { if (!IsDisposed) MessageBox.Show(FindForm(), ex.Message, "Semantic search"); }
                finally { if (!IsDisposed) searchRow.Enabled = true; }
            }); searchRow.Controls.Add(query); searchRow.Controls.Add(search); layout.Controls.Add(searchRow, 0, 1); layout.Controls.Add(results, 0, 2);
            results.DoubleClick += (s, e) => { var hit = results.SelectedItem as ConversationHit; if (hit != null) Jump(hit); };
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true }; layout.Controls.Add(actions, 0, 3);
            actions.Controls.Add(DialogUi.Button("Highlights", (s, e) => { if (selected == null) return; results.Items.Clear(); lock (selected) results.Items.AddRange(ConversationTools.Highlights(selected).Cast<object>().ToArray()); }));
            actions.Controls.Add(DialogUi.Button("Action items", (s, e) => ReviewTasks()));
            actions.Controls.Add(DialogUi.Button("Bookmarks", (s, e) => ReviewBookmarks()));
            foreach (string label in new[] { "Important", "Question", "Follow up" }) { string kind = label; actions.Controls.Add(DialogUi.Button("Mark " + label.ToLowerInvariant(), (s, e) => AddBookmark(kind))); }
            reviewHints.SetToolTip(actions, "During recording: Ctrl+Alt+1 important, Ctrl+Alt+2 question, Ctrl+Alt+3 follow up. These shortcuts work across apps.");
            reviewHints.SetToolTip(browserSearch, "Use project:Name tag:topic person:Name, or project:\"Two words\", alongside keywords.");
        }
        void ReviewBookmarks()
        {
            var note = Recording ? session.Document : selected; if (note == null) return;
            using (var dialog = DialogUi.Create("Bookmarks", new Size(620, 420)))
            {
                var list = new ListBox { Dock = DockStyle.Fill, AccessibleName = "Saved bookmarks" }; var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
                var empty = DialogUi.Text("No bookmarks yet. Mark an important moment while recording to find it here."); empty.Dock = DockStyle.Top;
                Action refresh = () => { list.Items.Clear(); foreach (var b in note.Bookmarks) list.Items.Add(TimeSpan.FromSeconds(b.Seconds).ToString(@"hh\:mm\:ss") + " | " + b.Kind); empty.Visible = list.Items.Count == 0; foreach (Button button in footer.Controls) if (button.Text != "Close") button.Enabled = list.SelectedIndex >= 0; }; refresh();
                footer.Controls.Add(DialogUi.Button("Jump to moment", (s, e) => { if (list.SelectedIndex < 0 || Busy) return; var b = note.Bookmarks[list.SelectedIndex]; SelectNote(note.Id); reviewingConversation = true; ApplyNotesView(); tabs.SelectedIndex = 1; StartPlayback((int)(b.Seconds * 1000)); dialog.Close(); }));
                footer.Controls.Add(DialogUi.Button("Remove bookmark", (s, e) => { if (list.SelectedIndex < 0) return; try { lock (note) { note.Bookmarks.RemoveAt(list.SelectedIndex); store.Save(note); } refresh(); } catch (Exception ex) { MessageBox.Show(dialog, ex.Message); } }));
                list.SelectedIndexChanged += (s, e) => { foreach (Button button in footer.Controls) if (button.Text != "Close") button.Enabled = list.SelectedIndex >= 0; };
                foreach (Button button in footer.Controls) button.Enabled = false;
                footer.Controls.Add(DialogUi.Button("Close", (s, e) => dialog.Close()));
                dialog.Controls.Add(list); dialog.Controls.Add(empty); dialog.Controls.Add(footer); ThemeColors.Apply(dialog, config.Theme); dialog.ShowDialog(FindForm());
            }
        }
        void ReviewTasks()
        {
            if (selected == null || Busy) return; var note = selected;
            using (var dialog = DialogUi.Create("Review action items", new Size(850, 600)))
            {
                var json = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
                var draft = json.Deserialize<System.Collections.Generic.List<NoteTask>>(json.Serialize(note.Tasks));
                draft.AddRange(ConversationTools.Suggest(note));
                var grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false, AllowUserToDeleteRows = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, AccessibleName = "Review suggested tasks" };
                grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize; grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells; grid.RowHeadersVisible = false;
                grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "Confirmed", HeaderText = "Confirm", FillWeight = 35 });
                grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "Done", HeaderText = "Done", FillWeight = 30 });
                foreach (string field in new[] { "Text", "Owner", "Due" }) grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = field, HeaderText = field, FillWeight = field == "Text" ? 180 : 60 });
                grid.DataSource = new System.ComponentModel.BindingList<NoteTask>(draft);
                var source = new ThemedTextBox { Dock = DockStyle.Bottom, Multiline = true, Height = 92, ReadOnly = true, ScrollBars = ScrollBars.Vertical, AccessibleName = "Original supporting statement" };
                Action refresh = () => { if (grid.CurrentRow == null) return; var task = grid.CurrentRow.DataBoundItem as NoteTask; if (task == null) return; var hit = new ConversationHit { Note = note, Segment = task.Segment }; source.Text = hit.ToString(); }; grid.SelectionChanged += (s, e) => refresh();
                var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
                footer.Controls.Add(DialogUi.Button("Play source", (s, e) => { if (grid.CurrentRow != null) Jump(new ConversationHit { Note = note, Segment = ((NoteTask)grid.CurrentRow.DataBoundItem).Segment }); }));
                footer.Controls.Add(DialogUi.Button("Save review", (s, e) => { grid.EndEdit(); foreach (var task in draft) if (!task.Confirmed) task.Done = false; var previous = note.Tasks; try { note.Tasks = draft; store.Save(note); dialog.Close(); } catch (Exception ex) { note.Tasks = previous; MessageBox.Show(dialog, ex.Message); } }, true));
                footer.Controls.Add(DialogUi.Button("Cancel", (s, e) => dialog.Close()));
                var emptyTasks = DialogUi.Text("No action items were found in this conversation."); emptyTasks.Dock = DockStyle.Top; emptyTasks.Visible = draft.Count == 0;
                grid.Visible = draft.Count > 0;
                dialog.Controls.Add(grid); dialog.Controls.Add(emptyTasks); dialog.Controls.Add(source); dialog.Controls.Add(footer); dialog.Controls.Add(new Label { Dock = DockStyle.Top, Height = 72, Text = "Owners and dates are suggestions copied from speech, not verified facts. Edit them and tick Confirm to create a task. Delete a row to dismiss it. Originals remain unchanged.", AutoEllipsis = true, Visible = draft.Count > 0 });
                if (draft.Count == 0) { source.Visible = false; foreach (Button button in footer.Controls) { bool close = button.Text == "Cancel"; button.Visible = close; if (close) button.Text = "Close"; } dialog.ClientSize = new Size(650, 260); }
                ThemeColors.Apply(dialog, config.Theme); refresh(); dialog.ShowDialog(FindForm());
            }
        }
    }
}
