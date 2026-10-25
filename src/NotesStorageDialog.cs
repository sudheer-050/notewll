using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;
namespace Notewll
{
    static partial class NotesStorageDialog
    {
        sealed class Deleted { public string Folder, Title; public override string ToString() { return Title + "  -  " + NotesData.Size(NotesData.Bytes(Folder)); } }
        public static void Show(IWin32Window owner, NoteStore store, Config config)
        {
            using (var form = DialogUi.Create("Storage & backup", new Size(760, 620)))
            {
                var tabs = new AppTabs { Dock = DockStyle.Fill };
                TabPage overview = new TabPage("Storage"), backups = new TabPage("Backups"), deleted = new TabPage("Recently deleted"), retentionPage = new TabPage("Retention");
                foreach (var page in new[] { overview, backups, deleted, retentionPage }) { page.Padding = new Padding(16); tabs.TabPages.Add(page); }
                var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
                var progress = new Label { Dock = DockStyle.Bottom, Height = 36, AutoEllipsis = true, Text = "Your notes stay on this computer." };
                footer.Controls.Add(DialogUi.Button("Close", (s, e) => form.Close())); form.Controls.Add(tabs); form.Controls.Add(progress); form.Controls.Add(footer);
                var sizes = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(0, 0, 0, 20) }; sizes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65)); sizes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
                var sizeValues = new Label[4]; string[] headings = { "Application and speech model", "Active notes and recordings", "Recently deleted", "Download cache" };
                for (int i = 0; i < headings.Length; i++) { sizes.Controls.Add(DialogUi.Text(headings[i]), 0, i); sizeValues[i] = DialogUi.Text("Calculating..."); sizes.Controls.Add(sizeValues[i], 1, i); }
                overview.Controls.Add(sizes);
                var overviewActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.TopDown };
                overview.Controls.Add(overviewActions);
                var backupActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
                backups.Controls.Add(backupActions); backupActions.Controls.Add(DialogUi.Text("Back up active notes and audio to a ZIP file. Restore keeps existing note IDs and never overwrites them. Deleted notes, app settings and the model are not included."));
                var list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, AccessibleName = "Recently deleted notes" };
                var empty = new Label { Text = "No recently deleted notes. Deleted notes appear here until restored or erased.", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 8, 0, 12) };
                var deletedActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true };
                deleted.Controls.Add(list); deleted.Controls.Add(empty); deleted.Controls.Add(deletedActions);
                string deletedRoot = Path.Combine(store.Root, ".deleted"), cache = Path.Combine(Config.DirectoryPath, "SetupCache");
                Action refresh = () =>
                {
                    long erased = NotesData.Bytes(deletedRoot), total = NotesData.Bytes(store.Root);
                    long[] values = { NotesData.ApplicationBytes(Path.GetDirectoryName(typeof(Config).Assembly.Location)), Math.Max(0, total - erased), erased, NotesData.Bytes(cache) };
                    for (int i = 0; i < values.Length; i++) sizeValues[i].Text = NotesData.Size(values[i]);
                    list.Items.Clear(); if (Directory.Exists(deletedRoot)) foreach (string folder in Directory.GetDirectories(deletedRoot)) try { var note = new JavaScriptSerializer().Deserialize<NoteDocument>(File.ReadAllText(Path.Combine(folder, "note.json"))); NoteStore.Validate(note); list.Items.Add(new Deleted { Folder = folder, Title = note.Title }); } catch (IOException) { } catch (ArgumentException) { } catch (InvalidOperationException) { }
                    empty.Visible = list.Items.Count == 0;
                };
                bool busy = false; form.FormClosing += (s, e) => { if (busy) e.Cancel = true; };
                Action<FlowLayoutPanel, string, Func<Task>> action = (panel, label, work) =>
                {
                    var button = DialogUi.Button(label, async (s, e) => { if (busy) return; busy = true; tabs.Enabled = false; footer.Enabled = false; progress.Text = label + "..."; try { await work(); refresh(); progress.Text = "Ready."; } catch (Exception ex) { progress.Text = "Could not finish. Your existing notes are kept."; MessageBox.Show(form, ex.Message, label); } finally { busy = false; tabs.Enabled = true; footer.Enabled = true; } }); panel.Controls.Add(button);
                };
                action(overviewActions, "Open notes folder", () => { Directory.CreateDirectory(store.Root); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = store.Root, UseShellExecute = true }); return Task.FromResult(0); });
                action(overviewActions, "Clear download cache", async () => { if (Directory.Exists(cache) && MessageBox.Show(form, "Remove setup downloads? Installed speech files and notes stay in place.", "Clear cache", MessageBoxButtons.YesNo) == DialogResult.Yes) await Task.Run(() => NotesData.DeleteTree(cache, Config.DirectoryPath)); });
                action(backupActions, "Back up active notes...", async () => { using (var file = new SaveFileDialog { Filter = "Notes backup|*.zip", DefaultExt = "zip", AddExtension = true, FileName = "Notewll-notes-" + DateTime.Now.ToString("yyyy-MM-dd") + ".zip" }) if (file.ShowDialog(form) == DialogResult.OK) { await Task.Run(() => NotesData.Backup(store, file.FileName)); MessageBox.Show(form, "Backup saved. Keep a copy on another drive."); } });
                action(backupActions, "Restore a backup...", async () => { using (var file = new OpenFileDialog { Filter = "Notes backup|*.zip;*.nwb" }) if (file.ShowDialog(form) == DialogResult.OK) { string preview = await Task.Run(() => LocalSnapshots.Read(file.FileName, plain => NotesData.RestorePreview(store, plain)));
                    if (MessageBox.Show(form, preview + "\r\n\r\nRestore now?", "Restore preview", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                    int count = await Task.Run(() => LocalSnapshots.Read(file.FileName, plain => NotesData.Restore(store, plain))); MessageBox.Show(form, count + " notes restored. Existing notes were kept."); } });
                action(backupActions, "Save encrypted snapshot...", async () => { using (var file = new SaveFileDialog { Filter = "Encrypted snapshot|*.nwb", DefaultExt = "nwb", AddExtension = true }) if (file.ShowDialog(form) == DialogResult.OK) await Task.Run(() => LocalSnapshots.Save(store, file.FileName)); });
                var schedule = new CheckBox { Text = "Daily encrypted snapshots while Notewll is running", AutoSize = true, Checked = config.ScheduledBackups }; backupActions.Controls.Add(schedule);
                schedule.CheckedChanged += (s, e) => { config.ScheduledBackups = schedule.Checked; config.Save(); };
                backupActions.Controls.Add(DialogUi.Text("Encrypted snapshots open only with this Windows account on this PC. Keep ZIP backups for migration. Snapshots include saved notes and audio, skip active recordings, and are retained until you remove them. Last automatic backup error: " + (String.IsNullOrEmpty(LocalSnapshots.LastError) ? "None reported" : LocalSnapshots.LastError)));
                action(backupActions, "Open scheduled snapshots", () => { Directory.CreateDirectory(LocalSnapshots.Root); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = LocalSnapshots.Root, UseShellExecute = true }); return Task.FromResult(0); });
                action(deletedActions, "Restore selected", () => { var item = list.SelectedItem as Deleted; if (item == null) return Task.FromResult(0); var note = new JavaScriptSerializer().Deserialize<NoteDocument>(File.ReadAllText(Path.Combine(item.Folder, "note.json"))); NoteStore.Validate(note); string target = store.Folder(note); if (Directory.Exists(target)) throw new IOException("A note already has this ID. It will not be overwritten."); Directory.Move(item.Folder, target); note.DeletedUtc = null; store.Save(note); return Task.FromResult(0); });
                action(deletedActions, "Erase selected permanently", async () => { var item = list.SelectedItem as Deleted; if (item != null && MessageBox.Show(form, "Erase this note and its audio permanently? This cannot be undone.", "Erase note", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) await Task.Run(() => NotesData.DeleteTree(item.Folder, deletedRoot)); });
                var retentionFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false }; retentionPage.Controls.Add(retentionFlow);
                retentionFlow.Controls.Add(DialogUi.Text("How long should recently deleted notes be kept? Active notes are never removed by this setting."));
                var retention = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280, AccessibleName = "Deleted note retention" }; retention.Items.AddRange(new object[] { "Keep until I erase them", "Erase after 7 days", "Erase after 30 days", "Erase after 90 days" }); retention.SelectedIndex = Math.Max(0, Array.IndexOf(new[] { 0, 7, 30, 90 }, config.DeletedRetentionDays)); retentionFlow.Controls.Add(retention);
                action(retentionFlow, "Save retention", () => { int days = new[] { 0, 7, 30, 90 }[retention.SelectedIndex]; if (days != 0 && MessageBox.Show(form, "Deleted notes older than " + days + " days will be permanently erased now and on future starts. Continue?", "Retention", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return Task.FromResult(0); config.DeletedRetentionDays = days; config.Save(); store.CleanupDeleted(days); return Task.FromResult(0); });
                AddFeatureManager(tabs, form, config);
                form.Shown += (s, e) => { try { refresh(); } catch (Exception ex) { progress.Text = "Storage could not be calculated: " + ex.Message; } };
                // Wrap descriptive text to each page; never use fixed-height labels for DPI-scaled paragraphs.
                foreach (var flow in new[] { backupActions, retentionFlow }) flow.Resize += (s, e) => { foreach (Label label in flow.Controls.OfType<Label>()) label.MaximumSize = new Size(Math.Max(200, flow.ClientSize.Width - 30), 0); };
                ThemeColors.Apply(form, config.Theme); form.ShowDialog(owner);
            }
        }
    }
}
