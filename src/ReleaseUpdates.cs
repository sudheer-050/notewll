using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Notewll
{
    static class ReleaseUpdates
    {
        public sealed class Manifest { public string Version { get; set; } public string InstallerUrl { get; set; } public string Sha256 { get; set; } }
        internal static Manifest ParseManifest(string json)
        {
            if (json.Length > 65536) throw new IOException("Update manifest is too large.");
            var manifest = new JavaScriptSerializer().Deserialize<Manifest>(json); Version version; Uri uri;
            if (manifest == null || !Version.TryParse(manifest.Version, out version) || !Uri.TryCreate(manifest.InstallerUrl, UriKind.Absolute, out uri) || uri.Scheme != "https" || !System.Text.RegularExpressions.Regex.IsMatch(manifest.Sha256 ?? "", "^[a-fA-F0-9]{64}$")) throw new IOException("Update manifest is invalid.");
            return manifest;
        }
        static async Task<Manifest> FetchManifest(Config config)
        {
            config.Validate(); ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create(config.UpdateManifestUrl); request.Timeout = 15000;
            using (var response = await request.GetResponseAsync())
            {
                if (response.ResponseUri.Scheme != "https" || response.ContentLength > 65536) throw new IOException("The update feed must remain HTTPS and smaller than 64 KB.");
                using (var input = response.GetResponseStream()) using (var output = new MemoryStream())
                {
                    var buffer = new byte[4096]; int count;
                    while ((count = await input.ReadAsync(buffer, 0, buffer.Length)) > 0) { if (output.Length + count > 65536) throw new IOException("Update manifest is too large."); output.Write(buffer, 0, count); }
                    return ParseManifest(System.Text.Encoding.UTF8.GetString(output.ToArray()).TrimStart('\uFEFF'));
                }
            }
        }
        public static async Task<string> AvailableVersion(Config config)
        {
            if (StoreDistribution.Enabled) return null;
            if (String.IsNullOrWhiteSpace(config.UpdateManifestUrl) || String.IsNullOrWhiteSpace(config.UpdatePublisherThumbprint)) return null;
            var manifest = await FetchManifest(config); return new Version(manifest.Version) > typeof(Config).Assembly.GetName().Version ? manifest.Version : null;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct FileInfoTrust { public uint Size; [MarshalAs(UnmanagedType.LPWStr)] public string Path; public IntPtr File, Subject; }
        [StructLayout(LayoutKind.Sequential)] struct TrustData { public uint Size; public IntPtr Callback, Sip; public uint Ui, Revocation, Choice; public IntPtr File; public uint State; public IntPtr StateData, Url; public uint Flags, Context; }
        [DllImport("wintrust.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);
        internal static bool Trusted(string path, string expectedPublisher)
        {
            var file = new FileInfoTrust { Size = (uint)Marshal.SizeOf(typeof(FileInfoTrust)), Path = path }; IntPtr pointer = Marshal.AllocHGlobal(Marshal.SizeOf(file));
            try
            {
                Marshal.StructureToPtr(file, pointer, false);
                var data = new TrustData { Size = (uint)Marshal.SizeOf(typeof(TrustData)), Ui = 2, Choice = 1, File = pointer }; var action = new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
                if (WinVerifyTrust((IntPtr)(-1), ref action, ref data) != 0) return false;
                using (var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path))) return certificate.Thumbprint.Equals(expectedPublisher.Replace(" ", ""), StringComparison.OrdinalIgnoreCase);
            }
            finally { Marshal.DestroyStructure(pointer, typeof(FileInfoTrust)); Marshal.FreeHGlobal(pointer); }
        }
        public static void OpenDownloads(IWin32Window owner)
        {
            if (StoreDistribution.Enabled) { StoreDistribution.OpenUpdates(); return; }
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://github.com/sudheer-050/notewll/releases", UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(owner, "Could not open GitHub Releases: " + ex.Message, "Get Notewll installer"); }
        }
        public static async Task Check(IWin32Window owner, Config config)
        {
            if (StoreDistribution.Enabled) { StoreDistribution.OpenUpdates(); return; }
            if (String.IsNullOrWhiteSpace(config.UpdateManifestUrl) || String.IsNullOrWhiteSpace(config.UpdatePublisherThumbprint))
            {
                OpenDownloads(owner);
                return;
            }
            string target = null;
            try
            {
                config.Validate(); ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var client = new WebClient())
                {
                    var manifest = await FetchManifest(config); var version = new Version(manifest.Version); var uri = new Uri(manifest.InstallerUrl);
                    if (version <= typeof(Config).Assembly.GetName().Version) { MessageBox.Show(owner, "You have the latest version in this feed.", "Up to date"); return; }
                    if (MessageBox.Show(owner, "Download and verify Notewll " + version + "? The installer will ask before updating the app.", "Update available", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                    string folder = Path.Combine(Config.DirectoryPath, "Updates"); Directory.CreateDirectory(folder); target = Path.Combine(folder, "Setup-" + Guid.NewGuid().ToString("N") + ".exe");
                    await client.DownloadFileTaskAsync(uri, target);
                    bool valid = await Task.Run(() => { using (var hash = SHA256.Create()) using (var input = File.OpenRead(target)) if (!BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase)) return false; return Trusted(target, config.UpdatePublisherThumbprint); });
                    if (!valid) throw new IOException("The update did not match the expected hash and trusted publisher signature. It was not run.");
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = target, UseShellExecute = true }); target = null;
                }
            }
            catch (Exception ex) { MessageBox.Show(owner, ex.Message, "Update could not finish"); }
            finally { if (target != null && File.Exists(target)) File.Delete(target); }
        }
    }
}
