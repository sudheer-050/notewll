using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Notewll
{
    static class NotesPdf
    {
        static byte[] Ascii(string value) { return Encoding.ASCII.GetBytes(value); }
        static byte[] Stream(byte[] data, string attributes = "")
        {
            using (var output = new MemoryStream()) { byte[] start = Ascii("<< /Length " + data.Length + " " + attributes + " >>\nstream\n"), end = Ascii("\nendstream"); output.Write(start, 0, start.Length); output.Write(data, 0, data.Length); output.Write(end, 0, end.Length); return output.ToArray(); }
        }
        static List<string> Wrap(string text, Graphics graphics, Font font, float width)
        {
            var lines = new List<string>();
            foreach (string paragraph in text.Replace("\r", "").Split('\n'))
            {
                if (paragraph.Length == 0) { lines.Add(""); continue; }
                string rest = paragraph;
                while (rest.Length > 0)
                {
                    int low = 1, high = rest.Length;
                    while (low < high) { int middle = (low + high + 1) / 2; if (graphics.MeasureString(rest.Substring(0, middle), font).Width <= width) low = middle; else high = middle - 1; }
                    int count = low;
                    if (count < rest.Length) { int space = rest.LastIndexOf(' ', count - 1, count); if (space > 0) count = space; if (count < rest.Length && Char.IsLowSurrogate(rest[count])) count--; }
                    if (count < 1) count = Math.Min(2, rest.Length);
                    lines.Add(rest.Substring(0, count).TrimEnd()); rest = rest.Substring(count).TrimStart(' ');
                }
            }
            return lines;
        }
        public static void Save(string text, string path)
        {
            var objects = new List<byte[]> { null, null, null }; var pageIds = new List<int>(); var structureIds = new List<int>(); var parentTree = new StringBuilder();
            Func<byte[], int> add = data => { objects.Add(data); return objects.Count; };
            using (var sample = new Bitmap(1, 1)) using (var measure = Graphics.FromImage(sample)) using (var font = new Font("Segoe UI", 18))
            {
                var lines = Wrap(text, measure, font, 1040); const int perPage = 47;
                for (int first = 0; first < Math.Max(1, lines.Count); first += perPage)
                {
                    string[] pageLines = lines.Skip(first).Take(perPage).ToArray(); byte[] jpeg;
                    using (var bitmap = new Bitmap(1240, 1754)) using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.Clear(Color.White); graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                        for (int i = 0; i < pageLines.Length; i++) graphics.DrawString(pageLines[i], font, Brushes.Black, 100, 105 + i * 32);
                        using (var footer = new Font("Segoe UI", 13)) graphics.DrawString("Notewll | Conversation snapshot | " + (pageIds.Count + 1), footer, Brushes.Gray, 100, 1655);
                        using (var bytes = new MemoryStream()) { bitmap.Save(bytes, ImageFormat.Jpeg); jpeg = bytes.ToArray(); }
                    }
                    int imageId = add(Stream(jpeg, "/Type /XObject /Subtype /Image /Width 1240 /Height 1754 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode"));
                    var resources = new StringBuilder(); var content = new StringBuilder("/Artifact BMC q 595 0 0 842 0 0 cm /Image Do Q EMC\n"); var roles = new List<string>();
                    for (int lineIndex = 0; lineIndex < pageLines.Length; lineIndex++)
                    {
                        string line = pageLines[lineIndex]; if (line.Length == 0) continue;
                        var tokens = new List<string>(); var enumerator = StringInfo.GetTextElementEnumerator(line); while (enumerator.MoveNext()) tokens.Add(enumerator.GetTextElement());
                        var alphabet = tokens.Distinct().ToList(); if (alphabet.Count > 255) throw new IOException("A PDF line contains too many distinct characters.");
                        var cmap = new StringBuilder("/CIDInit /ProcSet findresource begin 12 dict begin begincmap /CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def /CMapName /Conversation def /CMapType 2 def 1 begincodespacerange <00> <FF> endcodespacerange ");
                        for (int start = 0; start < alphabet.Count; start += 100)
                        {
                            int count = Math.Min(100, alphabet.Count - start); cmap.Append(count).Append(" beginbfchar\n");
                            for (int i = start; i < start + count; i++) cmap.Append('<').Append((i + 1).ToString("X2")).Append("> <").Append(BitConverter.ToString(Encoding.BigEndianUnicode.GetBytes(alphabet[i])).Replace("-", "")).Append(">\n");
                            cmap.Append("endbfchar\n");
                        }
                        cmap.Append("endcmap CMapName currentdict /CMap defineresource pop end end"); int cmapId = add(Stream(Ascii(cmap.ToString())));
                        int fontId = add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding /ToUnicode " + cmapId + " 0 R >>"));
                        string name = "F" + lineIndex; resources.Append('/').Append(name).Append(' ').Append(fontId).Append(" 0 R ");
                        string encoded = String.Join("", tokens.Select(token => (alphabet.IndexOf(token) + 1).ToString("X2")));
                        double y = 842 - (105 + lineIndex * 32 + 24) * 842.0 / 1754;
                        int mcid = roles.Count; roles.Add(first == 0 && lineIndex == 0 ? "H1" : new[] { "Personal notes", "Conversation", "Local review (source excerpts)" }.Contains(line) ? "H2" : "P");
                        string actualText = "FEFF" + BitConverter.ToString(Encoding.BigEndianUnicode.GetBytes(line + "\n")).Replace("-", "");
                        content.Append('/').Append(roles[mcid]).Append(" << /MCID ").Append(mcid).Append(" /ActualText <").Append(actualText).Append("> >> BDC BT /").Append(name).Append(" 11.5 Tf 3 Tr 48 ").Append(y.ToString("0.###", CultureInfo.InvariantCulture)).Append(" Td <").Append(encoded).Append("> Tj ET EMC\n");
                    }
                    int contentId = add(Stream(Ascii(content.ToString())));
                    int parentIndex = pageIds.Count;
                    int pageId = add(Ascii("<< /Type /Page /Parent 2 0 R /StructParents " + parentIndex + " /Tabs /S /MediaBox [0 0 595 842] /Resources << /XObject << /Image " + imageId + " 0 R >> /Font << " + resources + ">> >> /Contents " + contentId + " 0 R >>")); pageIds.Add(pageId);
                    var pageStructures = new List<int>();
                    for (int mcid = 0; mcid < roles.Count; mcid++) { int element = add(Ascii("<< /Type /StructElem /S /" + roles[mcid] + " /P 3 0 R /Pg " + pageId + " 0 R /K " + mcid + " >>")); pageStructures.Add(element); structureIds.Add(element); }
                    parentTree.Append(parentIndex).Append(" [").Append(String.Join(" ", pageStructures.Select(id => id + " 0 R"))).Append("] ");
                }
            }
            int parentTreeId = add(Ascii("<< /Nums [" + parentTree + "] >>"));
            objects[0] = Ascii("<< /Type /Catalog /Pages 2 0 R /Lang (en-US) /MarkInfo << /Marked true >> /StructTreeRoot 3 0 R >>"); objects[1] = Ascii("<< /Type /Pages /Count " + pageIds.Count + " /Kids [" + String.Join(" ", pageIds.Select(id => id + " 0 R")) + "] >>");
            objects[2] = Ascii("<< /Type /StructTreeRoot /K [" + String.Join(" ", structureIds.Select(id => id + " 0 R")) + "] /ParentTree " + parentTreeId + " 0 R /ParentTreeNextKey " + pageIds.Count + " >>");
            using (var file = File.Create(path))
            {
                Action<string> write = value => { var bytes = Ascii(value); file.Write(bytes, 0, bytes.Length); }; var offsets = new List<long> { 0 }; write("%PDF-1.4\n");
                for (int i = 0; i < objects.Count; i++) { offsets.Add(file.Position); write((i + 1) + " 0 obj\n"); file.Write(objects[i], 0, objects[i].Length); write("\nendobj\n"); }
                long xref = file.Position; write("xref\n0 " + offsets.Count + "\n0000000000 65535 f \n"); foreach (long offset in offsets.Skip(1)) write(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
                write("trailer\n<< /Size " + offsets.Count + " /Root 1 0 R >>\nstartxref\n" + xref + "\n%%EOF\n");
            }
        }
    }
}
