using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace Notewll
{
    // Notewll's online prompt service. The OAuth client is a Google "Desktop app" client: its secret is not
    // confidential by design (Google treats installed-app secrets as public), and PKCE protects the sign-in.
    static class NotewllCloud
    {
        // The Desktop app OAuth client is bundled at build time from .local\google-oauth.txt (line 1: client id,
        // line 2: installed-app secret) so it never lives in the public repository.
        static string[] bundled;
        static string Bundled(int line)
        {
            if (bundled == null)
            {
                try
                {
                    using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("NotewllCloud.GoogleClient"))
                        bundled = stream == null ? new string[0] : new StreamReader(stream).ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                }
                catch { bundled = new string[0]; }
            }
            return bundled.Length > line ? bundled[line].Trim() : "";
        }
        public static string DefaultClientId { get { return Bundled(0); } }
        public static string DefaultClientSecret { get { return Bundled(1); } }
        public const string DefaultServiceUrl = "https://sudheerkumargandiboyana.com/notewll/api/v1/prompt";
        // Test hooks: point at a local service / fake Google. Production builds use the defaults.
        public static string ServiceUrl { get { return Environment.GetEnvironmentVariable("NOTEWLL_PROMPT_SERVICE_URL") ?? DefaultServiceUrl; } }
        public static string TokenUrl { get { return Environment.GetEnvironmentVariable("NOTEWLL_GOOGLE_TOKEN_URL") ?? "https://oauth2.googleapis.com/token"; } }
        public static string ClientId { get { return Environment.GetEnvironmentVariable("NOTEWLL_GOOGLE_CLIENT_ID") ?? DefaultClientId; } }
        public static string ClientSecret { get { return Environment.GetEnvironmentVariable("NOTEWLL_GOOGLE_CLIENT_SECRET") ?? DefaultClientSecret; } }
        public static bool Configured { get { return ClientId.Length > 0; } }
    }

    // Sign in with Google using the installed-app flow: system browser, PKCE, one-time loopback redirect.
    // Only the refresh token and the email are kept, encrypted with Windows DPAPI for the current user.
    static class GoogleAuth
    {
        static string StorePath { get { return Path.Combine(Config.DirectoryPath, "google-signin.bin"); } }
        static readonly object gate = new object();
        static string idToken; static DateTime idTokenExpires;

        sealed class Saved { public string RefreshToken { get; set; } public string Email { get; set; } }

        static Saved Load()
        {
            try { if (!File.Exists(StorePath)) return null; var plain = ProtectedData.Unprotect(File.ReadAllBytes(StorePath), Entropy, DataProtectionScope.CurrentUser); return new JavaScriptSerializer().Deserialize<Saved>(Encoding.UTF8.GetString(plain)); }
            catch { return null; }
        }
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Notewll Google sign-in");
        static void SaveState(Saved saved)
        {
            Directory.CreateDirectory(Config.DirectoryPath);
            File.WriteAllBytes(StorePath, ProtectedData.Protect(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(saved)), Entropy, DataProtectionScope.CurrentUser));
        }

        public static string SignedInEmail { get { var s = Load(); return s == null || String.IsNullOrEmpty(s.RefreshToken) ? null : s.Email ?? "your Google account"; } }

        public static void SignOut()
        {
            var saved = Load();
            lock (gate) { idToken = null; }
            try { if (File.Exists(StorePath)) File.Delete(StorePath); } catch { }
            if (saved != null && !String.IsNullOrEmpty(saved.RefreshToken))
                try { Post("https://oauth2.googleapis.com/revoke", "token=" + Uri.EscapeDataString(saved.RefreshToken)); } catch { } // best effort
        }

        // Opens the browser for Google sign-in and waits for the redirect. Returns the signed-in email.
        public static string SignIn(TimeSpan timeout)
        {
            if (!NotewllCloud.Configured) throw new InvalidOperationException("Google sign-in is not set up in this build of Notewll yet.");
            string verifier = Random(64), challenge = Base64Url(SHA256.Create().ComputeHash(Encoding.ASCII.GetBytes(verifier))), state = Random(24);
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            try
            {
                string redirect = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/";
                string url = "https://accounts.google.com/o/oauth2/v2/auth?response_type=code&scope=" + Uri.EscapeDataString("openid email") +
                    "&client_id=" + Uri.EscapeDataString(NotewllCloud.ClientId) + "&redirect_uri=" + Uri.EscapeDataString(redirect) +
                    "&code_challenge=" + challenge + "&code_challenge_method=S256&state=" + state + "&access_type=offline&prompt=" + Uri.EscapeDataString("select_account consent");
                if (Environment.GetEnvironmentVariable("NOTEWLL_TEST_PRINT_AUTH_URL") == "1") { Console.WriteLine("auth-url:" + url); Console.Out.Flush(); }
                else Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                string code = WaitForCode(listener, state, timeout);
                var token = Post(NotewllCloud.TokenUrl, "grant_type=authorization_code&code=" + Uri.EscapeDataString(code) +
                    "&client_id=" + Uri.EscapeDataString(NotewllCloud.ClientId) + "&client_secret=" + Uri.EscapeDataString(NotewllCloud.ClientSecret) +
                    "&redirect_uri=" + Uri.EscapeDataString(redirect) + "&code_verifier=" + verifier);
                string refresh = token.ContainsKey("refresh_token") ? token["refresh_token"] as string : null, id = token.ContainsKey("id_token") ? token["id_token"] as string : null;
                if (String.IsNullOrEmpty(refresh) || String.IsNullOrEmpty(id)) throw new IOException("Google did not complete the sign-in. Try again.");
                string email = Claim(id, "email") ?? "your Google account";
                SaveState(new Saved { RefreshToken = refresh, Email = email });
                lock (gate) { idToken = id; idTokenExpires = DateTime.UtcNow.AddMinutes(50); }
                return email;
            }
            finally { listener.Stop(); }
        }

        static string WaitForCode(TcpListener listener, string state, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (!listener.Pending()) { System.Threading.Thread.Sleep(100); continue; }
                using (var client = listener.AcceptTcpClient())
                using (var stream = client.GetStream())
                {
                    client.ReceiveTimeout = 5000;
                    var reader = new StreamReader(stream, Encoding.ASCII);
                    string line = reader.ReadLine() ?? ""; // GET /?state=...&code=... HTTP/1.1
                    var parts = line.Split(' ');
                    var query = parts.Length > 1 && parts[1].Contains("?") ? Query(parts[1].Substring(parts[1].IndexOf('?') + 1)) : new Dictionary<string, string>();
                    bool ok = query.ContainsKey("code") && query.ContainsKey("state") && query["state"] == state;
                    string html = "<!doctype html><meta charset=utf-8><title>Notewll</title><body style=\"font-family:Segoe UI,sans-serif;padding:40px\"><h2>" +
                        (ok ? "You're signed in to Notewll." : "Sign-in did not finish.") + "</h2><p>You can close this tab and return to Notewll.</p>";
                    byte[] body = Encoding.UTF8.GetBytes(html);
                    byte[] head = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
                    stream.Write(head, 0, head.Length); stream.Write(body, 0, body.Length);
                    if (ok) return query["code"];
                    if (query.ContainsKey("error")) throw new IOException(query["error"] == "access_denied" ? "Google sign-in was cancelled." : "Google sign-in failed: " + query["error"]);
                }
            }
            throw new TimeoutException("Google sign-in timed out. Try again.");
        }

        // A current Google ID token for the prompt service, refreshed silently from the saved sign-in.
        public static string IdToken()
        {
            lock (gate) if (idToken != null && DateTime.UtcNow < idTokenExpires) return idToken;
            var saved = Load();
            if (saved == null || String.IsNullOrEmpty(saved.RefreshToken)) throw new InvalidOperationException("Sign in with Google in Preferences > Writing to use AI prompts.");
            Dictionary<string, object> token;
            try
            {
                token = Post(NotewllCloud.TokenUrl, "grant_type=refresh_token&refresh_token=" + Uri.EscapeDataString(saved.RefreshToken) +
                    "&client_id=" + Uri.EscapeDataString(NotewllCloud.ClientId) + "&client_secret=" + Uri.EscapeDataString(NotewllCloud.ClientSecret));
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response != null && response.StatusCode == HttpStatusCode.BadRequest) { SignOut(); throw new InvalidOperationException("Your Google sign-in expired. Sign in again in Preferences > Writing."); }
                throw new IOException("Could not reach Google to refresh your sign-in. Your words were typed as spoken.");
            }
            string id = token.ContainsKey("id_token") ? token["id_token"] as string : null;
            if (String.IsNullOrEmpty(id)) throw new IOException("Google did not return a sign-in token.");
            lock (gate) { idToken = id; idTokenExpires = DateTime.UtcNow.AddMinutes(50); }
            return id;
        }

        static Dictionary<string, object> Post(string url, string form)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create(url); request.Method = "POST"; request.ContentType = "application/x-www-form-urlencoded"; request.Timeout = 20000;
            byte[] data = Encoding.ASCII.GetBytes(form); using (var s = request.GetRequestStream()) s.Write(data, 0, data.Length);
            using (var response = request.GetResponse()) using (var reader = new StreamReader(response.GetResponseStream()))
            { string text = reader.ReadToEnd(); return text.Length == 0 ? new Dictionary<string, object>() : new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(text); }
        }

        static Dictionary<string, string> Query(string q)
        {
            var result = new Dictionary<string, string>();
            foreach (var pair in q.Split('&')) { int eq = pair.IndexOf('='); if (eq > 0) result[Uri.UnescapeDataString(pair.Substring(0, eq))] = Uri.UnescapeDataString(pair.Substring(eq + 1).Replace('+', ' ')); }
            return result;
        }
        static string Claim(string jwt, string name)
        {
            try { var part = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/'); part = part.PadRight(part.Length + (4 - part.Length % 4) % 4, '=');
                  var claims = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(Convert.FromBase64String(part))); return claims.ContainsKey(name) ? claims[name] as string : null; }
            catch { return null; }
        }
        static string Random(int bytes) { var b = new byte[bytes]; using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b); return Base64Url(b); }
        static string Base64Url(byte[] b) { return Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }
    }
}
