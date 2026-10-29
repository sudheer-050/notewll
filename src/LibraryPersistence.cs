using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
namespace Notewll
{
    static class RuleLibrary
    {
        public static List<PersonalRule> Merge(IEnumerable<PersonalRule> current, IEnumerable<PersonalRule> incoming, string kind)
        {
            var added = incoming.Select(r => r == null ? null : r.Copy()).ToList();
            PersonalRule.Validate(added);
            if (added.Any(r => r.Kind != kind)) throw new ArgumentException("This page imports " + (kind == "Word" ? "dictionary words" : "snippets") + " only.");
            foreach (var r in added) { r.Origin = "Imported"; r.PronunciationId = null; r.PronunciationPcm = null; if (String.IsNullOrEmpty(r.CreatedUtc)) r.CreatedUtc = DateTime.UtcNow.ToString("o"); }
            var proposed = current.Select(r => r.Copy()).Concat(added).ToList();
            if (proposed.Count > 1000) throw new ArgumentException("Keep at most 1,000 saved entries.");
            PersonalRule.Validate(proposed); return proposed;
        }
    }
    sealed partial class SettingsForm
    {
        void SaveLibrary(List<PersonalRule> proposed)
        {
            var next = source.Copy(); next.Personalization = proposed.Select(r => r.Copy()).ToList(); next.Validate();
            if (OnSaved != null) OnSaved(next);
            source = next; pendingRules = next.Personalization.Select(r => r.Copy()).ToList();
            RefreshCleaned(); if (refreshLibrary != null) refreshLibrary();
        }
        void ExportLibrary(System.Collections.Generic.IEnumerable<PersonalRule> entries)
        {
            using (var file = new SaveFileDialog { Filter = "Saved entries|*.json", DefaultExt = "json", AddExtension = true }) if (file.ShowDialog(this) == DialogResult.OK) try { var rules = entries.Select(r => r.Copy()).ToList(); foreach (var r in rules) { r.PronunciationId = null; r.PronunciationPcm = null; } NotesData.WriteRules(file.FileName, rules); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Export not completed"); }
        }
        void LibraryFiles(Control anchor, string kind, System.Collections.Generic.IEnumerable<PersonalRule> selected)
        {
            var menu = DialogUi.PopupMenu();
            menu.Items.Add("Import JSON...", null, (s, e) => { using (var file = new OpenFileDialog { Filter = "Saved entries|*.json" }) if (file.ShowDialog(this) == DialogResult.OK) try { SaveLibrary(RuleLibrary.Merge(pendingRules, NotesData.ReadRules(file.FileName), kind)); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Import not completed"); } });
            menu.Items.Add("Export all JSON...", null, (s, e) => ExportLibrary(pendingRules.Where(r => r.Kind == kind)));
            var selection = selected.ToArray(); menu.Items.Add("Export selected JSON...", null, (s, e) => ExportLibrary(selection)).Enabled = selection.Length > 0;
            menu.Show(anchor, new System.Drawing.Point(0, anchor.Height));
        }
    }
}
