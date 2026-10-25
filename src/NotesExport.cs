using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;

namespace Notewll
{
    static class NoteExport
    {
        internal static NoteDocument Scope(NoteDocument note, int scope, bool names)
        {
            if (scope < 0 || scope > 2) throw new InvalidDataException("Choose an export scope.");
            NoteDocument copy; lock (note) { var json = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }; copy = json.Deserialize<NoteDocument>(json.Serialize(note)); }
            if (scope == 0) { copy.WrittenNotes = ""; copy.Summary = ""; }
            if (scope == 1) { copy.Segments.Clear(); copy.Summary = ""; }
            if (!names) { copy.UserName = ""; copy.Participants.Clear(); foreach (var segment in copy.Segments) { segment.Introduction = ""; segment.SpeakerLabel = ""; segment.VoiceId = null; segment.VoiceEmbedding = null; } }
            if (!names && !String.IsNullOrWhiteSpace(copy.Summary)) copy.Summary = NotesSummary.Generate(copy);
            return copy;
        }
        static string Xml(string text) { return SecurityElement.Escape(text ?? ""); }
        internal static string PreviewText(NoteDocument note)
        {
            DateTime created;
            string date = DateTime.TryParse(note.CreatedUtc, out created) ? created.ToLocalTime().ToString("MMM d, yyyy - h:mm tt") : note.CreatedUtc;
            return note.Title + "\n\n" + date + " | " + note.Mode + "\n\n" +
                (String.IsNullOrWhiteSpace(note.UserName) ? "" : "Name: " + note.UserName + "\n\n") +
                (note.Participants.Count == 0 ? "" : "Participants: " + String.Join(", ", note.Participants) + "\n\n") +
                (String.IsNullOrWhiteSpace(note.WrittenNotes) ? "" : "Personal notes\n\n" + note.WrittenNotes + "\n\n") + (String.IsNullOrWhiteSpace(NoteStore.Transcript(note)) ? "" : "Conversation\n\n" + NoteStore.Transcript(note)) + (String.IsNullOrWhiteSpace(note.Summary) ? "" : "\n\nLocal review (source excerpts)\n\n" + note.Summary);
        }
        static void Part(ZipArchive zip, string name, string xml)
        {
            using (var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false))) writer.Write(xml);
        }
        static string[][] Rows(NoteDocument note)
        {
            var rows = new List<string[]> {
                new[] { "Title", note.Title }, new[] { "Created UTC", note.CreatedUtc },
                new[] { "Name", note.UserName }, new[] { "Participants", String.Join(", ", note.Participants) },
                new[] { "Personal notes", note.WrittenNotes }, new[] { "Time", "Source", "Conversation (read-only in app)" }
            };
            if (!String.IsNullOrWhiteSpace(note.Summary)) rows.Insert(5, new[] { "Local review (source excerpts)", note.Summary });
            foreach (var segment in note.Segments.Where(s => s.Complete && String.IsNullOrEmpty(s.Error) && !String.IsNullOrWhiteSpace(s.RawText ?? s.Text)).OrderBy(s => s.StartFrame).ThenBy(s => s.Source))
                rows.Add(new[] { TimeSpan.FromSeconds((segment.TranscriptStartFrame ?? segment.StartFrame) / 16000.0).ToString(@"hh\:mm\:ss"), NoteStore.Speaker(note, segment), segment.RawText ?? segment.Text });
            return rows.ToArray();
        }
        public static void SavePreview(string approvedText, string path)
        {
            string full = Path.GetFullPath(path), temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { NotesPdf.Save(approvedText, temp); if (File.Exists(full)) File.Replace(temp, full, null); else File.Move(temp, full); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public static void Save(NoteDocument note, string path)
        {
            string full = Path.GetFullPath(path); string temporary = Path.Combine(Path.GetDirectoryName(full), ".export-" + Guid.NewGuid().ToString("N") + Path.GetExtension(full));
            try { Write(note, temporary); if (File.Exists(full)) File.Replace(temporary, full, null, true); else File.Move(temporary, full); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        static void Write(NoteDocument note, string path)
        {
            lock (note)
            {
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension == ".csv")
                {
                    Func<string, string> cell = value => { value = value ?? ""; if (value.Length > 0 && "=+-@\t\r\n".Contains(value.TrimStart().FirstOrDefault())) value = "'" + value; return "\"" + value.Replace("\"", "\"\"") + "\""; };
                    File.WriteAllText(path, String.Join("\r\n", Rows(note).Select(row => String.Join(",", row.Select(cell)))), new UTF8Encoding(true));
                }
                else if (extension == ".xlsx") Excel(note, path);
                else if (extension == ".docx") Word(note, path);
                else if (extension == ".pdf") Pdf(note, path);
                else throw new InvalidDataException("Choose Excel, CSV, PDF or Word document.");
            }
        }
        static void Excel(NoteDocument note, string path)
        {
            using (var file = File.Create(path)) using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                Part(zip, "[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>");
                Part(zip, "_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                Part(zip, "xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Conversation\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
                Part(zip, "xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
                Part(zip, "xl/styles.xml", "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills><borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf/></cellStyleXfs><cellXfs count=\"1\"><xf applyAlignment=\"1\"><alignment wrapText=\"1\" vertical=\"top\"/></xf></cellXfs></styleSheet>");
                var xml = new StringBuilder("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><cols><col min=\"1\" max=\"1\" width=\"25\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"32\" customWidth=\"1\"/><col min=\"3\" max=\"3\" width=\"100\" customWidth=\"1\"/></cols><sheetData>");
                int index = 0;
                foreach (var row in Rows(note))
                {
                    xml.Append("<row r=\"").Append(++index).Append("\">");
                    for (int col = 0; col < row.Length; col++) xml.Append("<c r=\"").Append((char)('A' + col)).Append(index).Append("\" t=\"inlineStr\"><is><t xml:space=\"preserve\">").Append(Xml(row[col])).Append("</t></is></c>");
                    xml.Append("</row>");
                }
                xml.Append("</sheetData><sheetProtection sheet=\"1\" objects=\"1\" scenarios=\"1\"/></worksheet>");
                Part(zip, "xl/worksheets/sheet1.xml", xml.ToString());
            }
        }
        static void Word(NoteDocument note, string path)
        {
            using (var file = File.Create(path)) using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                Part(zip, "[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/><Override PartName=\"/word/settings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml\"/></Types>");
                Part(zip, "_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
                Part(zip, "word/_rels/document.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/settings\" Target=\"settings.xml\"/></Relationships>");
                Part(zip, "word/settings.xml", "<w:settings xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:documentProtection w:edit=\"readOnly\" w:enforcement=\"1\"/></w:settings>");
                var xml = new StringBuilder("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>");
                foreach (string line in PreviewText(note).Replace("\r", "").Split('\n')) xml.Append("<w:p><w:r><w:rPr><w:rFonts w:ascii=\"Calibri\" w:hAnsi=\"Calibri\"/><w:sz w:val=\"22\"/></w:rPr><w:t xml:space=\"preserve\">").Append(Xml(line)).Append("</w:t></w:r></w:p>");
                xml.Append("<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/><w:pgMar w:top=\"1134\" w:right=\"1134\" w:bottom=\"1134\" w:left=\"1134\"/></w:sectPr></w:body></w:document>");
                Part(zip, "word/document.xml", xml.ToString());
            }
        }
        static void Pdf(NoteDocument note, string path) { NotesPdf.Save(PreviewText(note), path); }
    }
}
