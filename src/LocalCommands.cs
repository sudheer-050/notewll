using System;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace Notewll
{
    static class LocalCommands
    {
        public static string Apply(string original, string instruction)
        {
            original = original ?? ""; string command = (instruction ?? "").Trim().TrimEnd('.', '!', '?');
            switch (command.ToLowerInvariant())
            {
                case "uppercase": case "make uppercase": return original.ToUpperInvariant();
                case "lowercase": case "make lowercase": return original.ToLowerInvariant();
                case "sentence case": return original.Length == 0 ? "" : Char.ToUpperInvariant(original[0]) + original.Substring(1);
                case "bullet list": case "make a bullet list": return String.Join(Environment.NewLine, original.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => "\u2022 " + s.Trim()));
                case "numbered list": case "make a numbered list": return String.Join(Environment.NewLine, original.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select((s, i) => (i + 1) + ". " + s.Trim()));
                case "remove last sentence":
                    string text = original.TrimEnd(); var boundaries = Regex.Matches(text, @"[.!?][""')\]]*\s+(?=\S)");
                    return boundaries.Count == 0 ? "" : text.Substring(0, boundaries[boundaries.Count - 1].Index + boundaries[boundaries.Count - 1].Length).TrimEnd();
            }
            var replace = Regex.Match(command, @"^replace\s+(.+?)\s+with\s+(.+)$", RegexOptions.IgnoreCase);
            var remove = Regex.Match(command, @"^remove\s+(.+)$", RegexOptions.IgnoreCase);
            string from = replace.Success ? replace.Groups[1].Value.Trim('"') : remove.Success ? remove.Groups[1].Value.Trim('"') : null;
            if (String.IsNullOrWhiteSpace(from)) throw new ArgumentException("Try uppercase, lowercase, sentence case, numbered list, bullet list, remove last sentence, replace X with Y, or remove X.");
            string pattern = @"(?<![\p{L}\p{N}_])" + Regex.Escape(from) + @"(?![\p{L}\p{N}_])";
            if (!Regex.IsMatch(original, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) throw new ArgumentException("That text was not found. Your original is unchanged.");
            string value = replace.Success ? replace.Groups[2].Value.Trim('"') : "";
            return Regex.Replace(original, pattern, m => value, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        }
    }
    static class LocalCommandDialog
    {
        public static int OpenCount;
        public static void Show(IWin32Window owner, Config config, string original, Action<string> apply, Func<bool> canRecord = null)
        {
            using (var dialog = DialogUi.Create("Edit text by voice", new Size(740, 650)))
            {
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8 };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                for (int i = 0; i < 8; i++) layout.RowStyles.Add(new RowStyle(i == 1 || i == 5 ? SizeType.Percent : SizeType.AutoSize, i == 1 ? 40 : i == 5 ? 60 : 0));
                var input = new ThemedTextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, Text = original ?? "", AccessibleName = "Original text for voice editing" };
                var command = new ThemedTextBox { Height = 36, Dock = DockStyle.Top, AccessibleName = "Editing instruction" };
                var result = new ThemedTextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, AccessibleName = "Proposed edit" };
                var status = DialogUi.Text("Edits stay on this device. Preview before applying; your source transcript stays unchanged.");
                var controls = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
                var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
                var accept = DialogUi.Button(apply == null ? "Copy result" : "Apply edit", (s, e) => { try { if (apply == null) Clipboard.SetDataObject(new DataObject(DataFormats.UnicodeText, result.Text), true); else apply(result.Text); dialog.Close(); } catch (Exception ex) { status.Text = ex.Message; } }, true); accept.Enabled = false;
                Action preview = () => { try { result.Text = LocalCommands.Apply(input.Text, command.Text); accept.Enabled = true; status.Text = "Review the proposed edit below. Original text above is unchanged."; } catch (Exception ex) { status.Text = ex.Message; accept.Enabled = false; } };
                controls.Controls.Add(DialogUi.Button("Preview edit", (s, e) => preview()));
                controls.Controls.Add(DialogUi.Button("Use copied text", (s, e) => { if (Clipboard.ContainsText()) input.Text = Clipboard.GetText(); }));
                var record = DialogUi.Button("Record instruction", null); controls.Controls.Add(record);
                MicRecorder microphone = null; bool processing = false; var timer = new Timer { Interval = 15000 };
                Func<Task> stop = async () => {
                    if (microphone == null) return; timer.Stop(); byte[] pcm = microphone.Stop(); microphone.Dispose(); microphone = null; record.Enabled = false; processing = true; status.Text = "Recognizing the instruction locally...";
                    try { string instruction = await Task.Run(() => DictationSpeech.Transcribe(config, pcm)); if (!dialog.IsDisposed) { command.Text = instruction; preview(); } }
                    catch (Exception ex) { if (!dialog.IsDisposed) status.Text = ex.Message; }
                    finally { Array.Clear(pcm, 0, pcm.Length); processing = false; if (!dialog.IsDisposed) { record.Enabled = true; record.Text = "Record instruction"; } }
                };
                record.Click += async (s, e) => { if (microphone != null) { await stop(); return; } if (processing || canRecord != null && !canRecord()) { status.Text = "Finish the other recording first."; return; } try { microphone = new MicRecorder(config.Microphone); microphone.Start(); record.Text = "Stop instruction"; status.Text = "Listening for an editing instruction (15 seconds maximum)."; timer.Start(); } catch (Exception ex) { if (microphone != null) microphone.Dispose(); microphone = null; status.Text = ex.Message; } };
                timer.Tick += async (s, e) => await stop();
                command.TextChanged += (s, e) => { accept.Enabled = false; result.Clear(); }; input.TextChanged += (s, e) => { accept.Enabled = false; result.Clear(); };
                actions.Controls.Add(accept); actions.Controls.Add(DialogUi.Button("Cancel", (s, e) => dialog.Close()));
                layout.Controls.Add(DialogUi.Text("Original text. Type or record: replace Tuesday with Wednesday, uppercase, lowercase, bullet list, or remove last sentence."), 0, 0);
                layout.Controls.Add(input, 0, 1); layout.Controls.Add(DialogUi.Text("Instruction"), 0, 2); layout.Controls.Add(command, 0, 3); layout.Controls.Add(controls, 0, 4); layout.Controls.Add(result, 0, 5); layout.Controls.Add(status, 0, 6); layout.Controls.Add(actions, 0, 7);
                dialog.Controls.Add(layout); ThemeColors.Apply(dialog, config.Theme); OpenCount++;
                try { dialog.ShowDialog(owner); }
                finally { OpenCount--; timer.Dispose(); if (microphone != null) microphone.Dispose(); }
            }
        }
    }
}
