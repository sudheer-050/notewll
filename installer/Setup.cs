using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyVersion("0.10.0.0")]
[assembly: AssemblyTitle("Notewll Setup")]
[assembly: AssemblyProduct("Notewll")]
namespace LocalDictationSetup
{
    sealed class SetupForm : Form
    {
        const string Version = "0.10.0", RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\LocalDictation";
        const string ModelUrl = "https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip";
        const string RuntimeUrl = "https://github.com/alphacep/vosk-api/releases/download/v0.3.45/vosk-win64-0.3.45.zip";
        readonly string destination = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "LocalDictation");
        readonly Label status = new Label { Left = 34, Top = 290, Width = 550, Height = 65 };
        readonly ProgressBar progress = new ProgressBar { Left = 34, Top = 265, Width = 550, Height = 10 };
        readonly CheckBox desktop = new CheckBox { Left = 34, Top = 190, Width = 500, Text = "Create a desktop shortcut", Checked = true };
        readonly Label heading = new Label { Left = 30, Top = 28, Width = 550, Height = 58, Text = "Notewll", Font = new Font("Segoe UI", 28) };
        readonly Label description = new Label { Left = 34, Top = 95, Width = 550, Height = 90 };
        readonly Button close = new Button { Left = 270, Top = 370, Width = 120, Height = 42, Text = "Close", Visible = false };
        readonly Button action = new Button { Left = 402, Top = 370, Width = 182, Height = 42, Text = "Install", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(39, 42, 47), ForeColor = Color.White };
        readonly CheckBox removeSpeech = new CheckBox { Left = 34, Top = 190, Width = 550, Text = "Remove downloaded speech files and setup cache" };
        readonly CheckBox removeNotes = new CheckBox { Left = 34, Top = 222, Width = 550, Text = "Permanently erase my notes, recordings and preferences" };
        readonly bool uninstall;
        bool busy, finished;
        CancellationTokenSource cancellation = new CancellationTokenSource();
        const string ModelHash = "30f26242c4eb449f948e42cb302dd7a686cb29a3423a8367f99ff41780942498", RuntimeHash = "f1dcc9cca460630f81ea8f71794f69c80bed6556d2a4e6237b5785e1d2dff34b";
        [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
        public SetupForm(bool uninstall)
        {
            Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            this.uninstall = uninstall; AutoScaleMode = AutoScaleMode.None; Font = new Font("Segoe UI", 10);
            Text = uninstall ? "Uninstall Notewll" : "Notewll Setup"; BackColor = Color.FromArgb(250, 249, 246);
            ClientSize = new Size(620, 440); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
            description.Text = uninstall ? "Remove the application. Your notes, audio and preferences will be kept." : "One file. Offline English voice typing and notes.\nIncludes the app, speech engine and English model.\nEnhanced recognition models are optional downloads.";
            Controls.AddRange(new Control[] { heading, description, desktop, removeSpeech, removeNotes, progress, status, action, close });
            close.Text = "Cancel"; close.Visible = true; close.Click += (s, e) => { if (busy) { cancellation.Cancel(); close.Enabled = false; status.Text = "Canceling safely..."; } else Close(); };
            status.Text = "Windows 64-bit | Version " + Version + "\nReady to install. Your saved notes and preferences will be kept.";
            removeSpeech.Visible = removeNotes.Visible = uninstall;
            if (uninstall) { desktop.Visible = false; action.Text = "Uninstall"; status.Text = "Your saved notes remain in LocalAppData\\LocalDictation\\Notes."; }
            action.Click += async (s, e) =>
            {
                if (!finished) { await Run(); return; }
                if (uninstall) { Close(); return; }
                try { Process.Start(new ProcessStartInfo { FileName = Path.Combine(destination, "LocalDictation.exe"), WorkingDirectory = destination, UseShellExecute = true }); Close(); }
                catch (Exception ex) { status.Text = "Could not open the app: " + ex.Message + "\nTry the Notewll shortcut in the Start menu."; }
            };
            FormClosing += (s, e) => { if (busy) e.Cancel = true; };
            using (var graphics = CreateGraphics()) { float scale = graphics.DpiX / 96f; if (scale != 1) Scale(new SizeF(scale, scale)); }
        }
        static byte[] Resource(string name)
        {
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            using (var output = new MemoryStream()) { input.CopyTo(output); return output.ToArray(); }
        }
        bool StopRunning()
        {
            foreach (var process in Process.GetProcessesByName("LocalDictation")) using (process)
            {
                try { if (!String.Equals(process.MainModule.FileName, Path.Combine(destination, "LocalDictation.exe"), StringComparison.OrdinalIgnoreCase)) continue; }
                catch { continue; }
                try { using (var signal = EventWaitHandle.OpenExisting("LocalDictation.Exit")) signal.Set(); }
                catch (WaitHandleCannotBeOpenedException) { status.Text = "Exit Notewll from its tray menu, then click the button again. Notes will be kept."; return false; }
                if (!process.WaitForExit(8000)) { status.Text = "The app is still saving. Close it from its tray menu and try again."; return false; }
            }
            return true;
        }
        async Task Run()
        {
            if (busy) return;
            if (!Environment.Is64BitOperatingSystem) { status.Text = "This package supports 64-bit Windows."; return; }
            if (!StopRunning()) return; cancellation.Dispose(); cancellation = new CancellationTokenSource(); busy = true; action.Enabled = false; close.Enabled = true;
            try
            {
                if (uninstall) { if (removeNotes.Checked && MessageBox.Show(this, "Permanently erase all saved notes, audio, deleted items and preferences? This cannot be undone.", "Erase local data", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return; RemoveApplication(); return; }
                Directory.CreateDirectory(destination);
                string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalDictation", "SetupCache"); Directory.CreateDirectory(cache);
                if (!File.Exists(Path.Combine(destination, "native", ".installed")))
                {
                    string zip = Path.Combine(cache, "vosk-win64-0.3.45.zip"); await Unpack(RuntimeUrl, zip, "1/4 - Offline engine"); await Task.Run(() => Extract(zip, Path.Combine(destination, "native"), true, cancellation.Token)); cancellation.Token.ThrowIfCancellationRequested(); File.WriteAllText(Path.Combine(destination, "native", ".installed"), "0.3.45");
                }
                if (!File.Exists(Path.Combine(destination, "model-small-en-us", ".installed")))
                {
                    string zip = Path.Combine(cache, "vosk-model-small-en-us-0.15.zip"); await Unpack(ModelUrl, zip, "2/4 - English model"); await Task.Run(() => Extract(zip, Path.Combine(destination, "model-small-en-us"), false, cancellation.Token)); cancellation.Token.ThrowIfCancellationRequested(); File.WriteAllText(Path.Combine(destination, "model-small-en-us", ".installed"), "small-en-us-0.15");
                }
                if (!File.Exists(Path.Combine(destination, "native", "libvosk.dll")) || !File.Exists(Path.Combine(destination, "model-small-en-us", "am", "final.mdl"))) throw new IOException("The speech files are incomplete. Retry setup.");
                string exe = Path.Combine(destination, "LocalDictation.exe");
                if (File.Exists(exe)) File.Copy(exe, Path.Combine(destination, "LocalDictation.previous.exe"), true);
                File.WriteAllBytes(exe, Resource("Application")); Directory.CreateDirectory(Path.Combine(destination, "engine")); File.WriteAllBytes(Path.Combine(destination, "engine", "semantic_worker.py"), Resource("SemanticWorker")); File.WriteAllBytes(Path.Combine(destination, "engine", "setup_semantic.ps1"), Resource("SemanticSetup")); Directory.CreateDirectory(Path.Combine(destination, "engine")); File.WriteAllBytes(Path.Combine(destination, "engine", "voice_match_worker.py"), Resource("VoiceMatchWorker")); File.WriteAllBytes(Path.Combine(destination, "engine", "setup_handsfree.ps1"), Resource("VoiceMatchSetup")); File.WriteAllBytes(Path.Combine(destination, "engine", "quality_worker.py"), Resource("QualityWorker")); File.WriteAllBytes(Path.Combine(destination, "engine", "accurate_worker.py"), Resource("AccurateWorker")); File.WriteAllBytes(Path.Combine(destination, "engine", "silero_vad.onnx"), Resource("SpeechDetector")); File.WriteAllBytes(Path.Combine(destination, "THIRD_PARTY_NOTICES.md"), Resource("Notices"));
                cancellation.Token.ThrowIfCancellationRequested(); status.Text = "3/4 - Checking the installed speech model...";
                var verified = await Task.Run(() =>
                {
                    using (var check = Process.Start(new ProcessStartInfo { FileName = exe, Arguments = "--installed-speech-check", WorkingDirectory = destination, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
                    {
                        if (!check.WaitForExit(60000)) { check.Kill(); return "Speech model check timed out."; }
                        return check.ExitCode == 0 ? null : check.StandardError.ReadToEnd() + check.StandardOutput.ReadToEnd();
                    }
                });
                if (verified != null) { File.Delete(Path.Combine(destination, "native", ".installed")); File.Delete(Path.Combine(destination, "model-small-en-us", ".installed")); throw new IOException(verified); }
                cancellation.Token.ThrowIfCancellationRequested(); status.Text = "4/4 - Finishing installation...";
                string uninstaller = Path.Combine(destination, "Uninstall.exe");
                if (!String.Equals(Assembly.GetExecutingAssembly().Location, uninstaller, StringComparison.OrdinalIgnoreCase)) File.WriteAllBytes(uninstaller, Resource("Uninstaller"));
                foreach (var parent in new[] { Environment.SpecialFolder.Programs, Environment.SpecialFolder.DesktopDirectory })
                foreach (string oldName in new[] { "Local Dictation.lnk", "Dotflow.lnk", "Dotwell.lnk" })
                {
                    string legacy = Path.Combine(Environment.GetFolderPath(parent), oldName);
                    if (File.Exists(legacy)) { object shell = null, link = null; try { shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")); link = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { legacy }); string target = Convert.ToString(link.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, link, null)); if (String.Equals(target, exe, StringComparison.OrdinalIgnoreCase)) File.Delete(legacy); } finally { if (link != null) Marshal.FinalReleaseComObject(link); if (shell != null) Marshal.FinalReleaseComObject(shell); } }
                }
                MakeShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Notewll.lnk"), exe);
                if (desktop.Checked) MakeShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Notewll.lnk"), exe);
                using (var key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    key.SetValue("DisplayName", "Notewll"); key.SetValue("DisplayVersion", Version); key.SetValue("InstallLocation", destination);
                    key.SetValue("DisplayIcon", exe); key.SetValue("UninstallString", "\"" + uninstaller + "\" --uninstall"); key.SetValue("NoModify", 1); key.SetValue("NoRepair", 1);
                    key.SetValue("EstimatedSize", InstalledKilobytes(destination), RegistryValueKind.DWord);
                }
                ShowSuccess();
            }
            catch (Exception ex) { status.Text = cancellation.IsCancellationRequested ? "Installation canceled. Your saved notes are kept. Click Retry to continue." : "Setup could not finish: " + ex.Message + "\nYour saved notes are kept. Click Retry to continue."; action.Text = "Retry"; }
            finally { busy = false; action.Enabled = true; close.Enabled = true; }
        }
        void ShowSuccess()
        {
            finished = true;
            Text = "Notewll - Installed";
            heading.Text = "You're all set";
            description.Text = "Notewll installed successfully.\nYour English speech model is ready. Open the app to start writing and taking notes.";
            progress.Style = ProgressBarStyle.Continuous; desktop.Visible = progress.Visible = false;
            status.Text = "No further downloads are needed for speech recognition.\nYou can use the app offline.";
            action.Text = "Open app";
            close.Visible = true; close.Text = "Close";
        }
        static int InstalledKilobytes(string directory)
        {
            long bytes = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length);
            return (int)Math.Min(Int32.MaxValue, (bytes + 1023) / 1024);
        }
        async Task Unpack(string url, string target, string message)
        {
            string expected = url == ModelUrl ? ModelHash : RuntimeHash;
            status.Text = message + " - Unpacking included speech files...";
            progress.Style = ProgressBarStyle.Marquee;
            await Task.Run(() => {
                if (File.Exists(target) && MatchesHash(target, expected)) return;
                string temporary = target + ".partial";
                try {
                    using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream(url == ModelUrl ? "SpeechModelZip" : "SpeechRuntimeZip"))
                    using (var output = File.Create(temporary)) {
                        byte[] buffer = new byte[131072]; int read;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0) { cancellation.Token.ThrowIfCancellationRequested(); output.Write(buffer, 0, read); }
                    }
                    if (!MatchesHash(temporary, expected)) throw new IOException("Included speech files failed the integrity check.");
                    File.Copy(temporary, target, true);
                } finally { if (File.Exists(temporary)) File.Delete(temporary); }
            });
        }

        static bool MatchesHash(string path, string expected) { using (var hash = SHA256.Create()) using (var file = File.OpenRead(path)) return BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "").Equals(expected, StringComparison.OrdinalIgnoreCase); }
        public static void Extract(string archive, string target, bool librariesOnly, CancellationToken token = default(CancellationToken))
        {
            string directory = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar; Directory.CreateDirectory(directory);
            using (var zip = ZipFile.OpenRead(archive)) foreach (var entry in zip.Entries)
            {
                token.ThrowIfCancellationRequested();
                string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar); int separator = relative.IndexOf(Path.DirectorySeparatorChar);
                if (separator >= 0) relative = relative.Substring(separator + 1);
                if (String.IsNullOrWhiteSpace(relative) || String.IsNullOrEmpty(entry.Name)) continue;
                if (librariesOnly) { if (!relative.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue; relative = Path.GetFileName(relative); }
                string path = Path.GetFullPath(Path.Combine(directory, relative));
                if (!path.StartsWith(directory, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The archive contains an invalid path.");
                Directory.CreateDirectory(Path.GetDirectoryName(path)); entry.ExtractToFile(path, true);
            }
        }
        static void MakeShortcut(string path, string exe)
        {
            object shell = null, link = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")); link = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                link.GetType().InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { exe });
                link.GetType().InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { Path.GetDirectoryName(exe) }); link.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
            }
            finally { if (link != null) Marshal.ReleaseComObject(link); if (shell != null) Marshal.ReleaseComObject(shell); }
        }
        static void RemoveKnownDirectory(string directory, string root)
        {
            directory = Path.GetFullPath(directory); root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!directory.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Unsafe cleanup path.");
            if (!Directory.Exists(directory)) return;
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked directories must be removed separately.");
            foreach (string child in Directory.GetDirectories(directory)) RemoveKnownDirectory(child, root);
            foreach (string file in Directory.GetFiles(directory)) { if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked files must be removed separately."); File.Delete(file); }
            Directory.Delete(directory);
        }
        void RemoveApplication()
        {
            // Delete only known program files. Never recurse into user data or remove the running uninstaller.
            foreach (string file in new[] { "LocalDictation.exe", "LocalDictation.previous.exe", "THIRD_PARTY_NOTICES.md" }) { string path = Path.Combine(destination, file); if (File.Exists(path)) File.Delete(path); }
            foreach (var parent in new[] { Environment.SpecialFolder.Programs, Environment.SpecialFolder.DesktopDirectory }) { string path = Path.Combine(Environment.GetFolderPath(parent), "Notewll.lnk"); if (File.Exists(path)) File.Delete(path); }
            string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalDictation");
            if (removeSpeech.Checked) { RemoveKnownDirectory(Path.Combine(destination, "native"), destination); RemoveKnownDirectory(Path.Combine(destination, "model-small-en-us"), destination); RemoveKnownDirectory(Path.Combine(data, "SetupCache"), data); }
            if (removeNotes.Checked) { RemoveKnownDirectory(Path.Combine(data, "Notes"), data); string preferences = Path.Combine(data, "config.json"); if (File.Exists(preferences)) File.Delete(preferences); }
            Registry.CurrentUser.DeleteSubKeyTree(RegistryPath, false);
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)) if (key != null) key.DeleteValue("LocalDictation", false);
            status.Text = "Application removed. " + (removeNotes.Checked ? "Selected notes and preferences erased. " : "Your notes and preferences are kept. ") + (removeSpeech.Checked ? "Downloaded speech files removed." : "Downloaded speech files are kept."); finished = true; action.Text = "Close";
        }
        [STAThread] static int Main(string[] args)
        {
            if (args.Length == 3 && args[0] == "--verify-hash") { Console.Write(MatchesHash(args[1], args[2]) ? "valid" : "invalid"); return 0; }
            if (args.Length == 2 && args[0] == "--installed-size") { Console.Write(InstalledKilobytes(args[1])); return 0; }
            if (args.Length == 2 && args[0] == "--test-bundle") {
                try {
                    string target = Path.GetFullPath(args[1]); Directory.CreateDirectory(target);
                    foreach (string name in new[] { "SpeechRuntimeZip", "SpeechModelZip" }) {
                        string zip = Path.Combine(target, name + ".zip"); File.WriteAllBytes(zip, Resource(name));
                        if (!MatchesHash(zip, name == "SpeechModelZip" ? ModelHash : RuntimeHash)) throw new IOException("Invalid embedded archive");
                        Extract(zip, Path.Combine(target, name == "SpeechModelZip" ? "model-small-en-us" : "native"), name == "SpeechRuntimeZip"); File.Delete(zip);
                    }
                    File.WriteAllBytes(Path.Combine(target, "LocalDictation.exe"), Resource("Application"));
                    File.WriteAllBytes(Path.Combine(target, "Uninstall.exe"), Resource("Uninstaller"));
                    Directory.CreateDirectory(Path.Combine(target,"engine")); File.WriteAllBytes(Path.Combine(target,"engine","voice_match_worker.py"),Resource("VoiceMatchWorker")); File.WriteAllBytes(Path.Combine(target,"engine","setup_handsfree.ps1"),Resource("VoiceMatchSetup"));
                    Console.Write("bundle-ok|offline=true"); return 0;
                } catch (Exception ex) { Console.Error.Write(ex.Message); return 2; }
            }
            if (args.Length > 0 && args[0] == "--inspect") { using (var hash = SHA256.Create()) Console.Write("version=" + Version + "|platform=Windows-x64|scope=current-user|engine=Vosk-small-English|model-download=none|offline-install=true|payload-sha256=" + BitConverter.ToString(hash.ComputeHash(Resource("Application"))).Replace("-", "").ToLowerInvariant()); return 0; }
            if (args.Length > 0 && args[0] == "--inspect-handsfree") { using (var hash = SHA256.Create()) Console.Write("worker-sha256=" + BitConverter.ToString(hash.ComputeHash(Resource("VoiceMatchWorker"))).Replace("-", "").ToLowerInvariant() + "|setup-sha256=" + BitConverter.ToString(hash.ComputeHash(Resource("VoiceMatchSetup"))).Replace("-", "").ToLowerInvariant()); return 0; }
            if (args.Length > 0 && args[0] == "--inspect-accuracy") { using (var hash = SHA256.Create()) Console.Write("worker-sha256=" + BitConverter.ToString(hash.ComputeHash(Resource("AccurateWorker"))).Replace("-", "").ToLowerInvariant() + "|detector-sha256=" + BitConverter.ToString(hash.ComputeHash(Resource("SpeechDetector"))).Replace("-", "").ToLowerInvariant()); return 0; }
            if (args.Length == 4 && args[0] == "--test-extract") { try { Extract(args[1], args[2], args[3] == "dll"); return 0; } catch (Exception ex) { Console.Error.Write(ex.Message); return 2; } }
            Application.EnableVisualStyles();
            using (var form = new SetupForm(
#if UNINSTALLER
true
#else
args.Contains("--uninstall")
#endif
))
            {
                if (args.Length == 2 && (args[0] == "--preview" || args[0] == "--preview-success")) { if (args[0] == "--preview-success") form.ShowSuccess(); form.Show(); Application.DoEvents(); using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(args[1]); } return 0; }
                Application.Run(form); return 0;
            }
        }
    }
}
