using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

[assembly: System.Reflection.AssemblyTitle("Notewll")]
[assembly: System.Reflection.AssemblyProduct("Notewll")]
[assembly: System.Reflection.AssemblyVersion("0.10.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.10.0.0")]

namespace Notewll
{
    enum AppState { Idle, Recording, Processing, Error }

    sealed class StateMachine
    {
        public AppState Current { get; private set; }
        public StateMachine() { Current = AppState.Idle; }
        public void Move(AppState next)
        {
            bool ok = (Current == AppState.Idle && (next == AppState.Recording || next == AppState.Error))
                || (Current == AppState.Recording && (next == AppState.Processing || next == AppState.Idle || next == AppState.Error))
                || (Current == AppState.Processing && (next == AppState.Idle || next == AppState.Error))
                || (Current == AppState.Error && next == AppState.Idle);
            if (!ok) throw new InvalidOperationException("Invalid state transition: " + Current + " -> " + next);
            Current = next;
        }
    }

    sealed class Config
    {
        public const string DefaultMic = "system-default";

        public string Hotkey { get; set; }
        public string NotesHotkey { get; set; }
        public string NotesSource { get; set; }
        public string MeetingSource { get; set; }
        public int ToolbarX { get; set; }
        public int ToolbarY { get; set; }
        // "system-default" or the exact name of a capture device from the settings list.
        public string Microphone { get; set; }
        public bool CleanupEnabled { get; set; }
        public bool SmartCleanupEnabled { get; set; }
        public bool QualityEnabled { get; set; }
        public bool AccurateDictationEnabled { get; set; }
        public string QualityPython { get; set; }
        public string HandsFreePython { get; set; }
        public string HandsFreeApps { get; set; }
        public bool ScheduledBackups { get; set; }
        public bool AutoPaste { get; set; }
        public bool LiveWriting { get; set; }
        public string PasteMethod { get; set; }
        public bool StartWithWindows { get; set; }
        public bool AutoStartStt { get; set; }
        public string SttUrl { get; set; }
        public string SttLauncher { get; set; }
        public string SttPythonw { get; set; }

        public bool SetupGuideSeen { get; set; }
        public bool SetupMicrophoneVerified { get; set; }
        public bool SetupSpeechVerified { get; set; }
        public bool SetupInsertionConfirmed { get; set; }
        public string NotesSort { get; set; }
        public string ExportFormat { get; set; }
        public string WritingStyle { get; set; }
        public bool CheckUpdatesOnStart { get; set; }
        public string UpdateManifestUrl { get; set; }
        public string UpdatePublisherThumbprint { get; set; }
        public string Theme { get; set; }
        public bool ShowToolbar { get; set; }
        public bool ShowVoiceIndicator { get; set; }
        public bool ReduceNoise { get; set; }
        public bool ReducedMotion { get; set; }
        public string BallAppearance { get; set; }
        public string BallGravity { get; set; }
        public string BallColorMode { get; set; }
        public string BallColor1 { get; set; }
        public string BallColor2 { get; set; }
        public int BallGradientAngle { get; set; }
        public int BallSizePercent { get; set; }
        public int BallExpandedSizePercent { get; set; }
        public string BallImagePath { get; set; }
        public string NoteMicrophoneId { get; set; }
        public string NoteOutputId { get; set; }
        public int DeletedRetentionDays { get; set; }
        public int TypingWordsPerMinute { get; set; }
        public List<PersonalRule> Personalization { get; set; }
        public Config Copy() { var json = new JavaScriptSerializer(); var copy = json.Deserialize<Config>(json.Serialize(this)); if (Personalization != null) copy.Personalization = Personalization.Select(rule => rule.Copy()).ToList(); return copy; }
        public Config()
        {
            BallSizePercent=100; BallExpandedSizePercent=100; BallImagePath="";
            BallAppearance="Moon"; BallGravity="Earth"; BallColorMode="Original colors"; BallColor1="#B9C1CB"; BallColor2="#F2F0D9"; BallGradientAngle=45;
            TypingWordsPerMinute = 40;
            HandsFreeApps = ""; QualityPython = ""; HandsFreePython = ""; QualityEnabled = false;
            NotesSort = "Newest"; ExportFormat = "PDF"; WritingStyle = "Standard";
            UpdateManifestUrl = UpdatePublisherThumbprint = ""; Theme = "System"; ShowToolbar = true; ShowVoiceIndicator = false; ReduceNoise = true; ReducedMotion = false; NoteMicrophoneId = NoteOutputId = ""; Personalization = new List<PersonalRule>();
            SmartCleanupEnabled = false;
            Hotkey = "F8"; NotesHotkey = "Ctrl+Shift+N"; NotesSource = "Microphone"; MeetingSource = "Both"; ToolbarX = ToolbarY = Int32.MinValue; Microphone = DefaultMic; CleanupEnabled = true; AutoPaste = true; LiveWriting = true; PasteMethod = "Auto"; StartWithWindows = false; AutoStartStt = true;
            SttUrl = "http://127.0.0.1:5000";
            SttLauncher = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"Documents\projects\ollama-personal-model\holly_server.py");
            SttPythonw = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python310\pythonw.exe");
        }

        static string diagnosticDirectory;
        internal static void DiagnosticDataRoot(string path) { diagnosticDirectory = Path.GetFullPath(path); }
        public static string DirectoryPath { get { return diagnosticDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalDictation"); } }
        public static string FilePath { get { return Path.Combine(DirectoryPath, "config.json"); } }

        public static Config Load(string path = null)
        {
            path = path ?? FilePath;
            if (!File.Exists(path)) return new Config();
            try
            {
                string json = File.ReadAllText(path);
                var serializer = new JavaScriptSerializer(); var value = serializer.Deserialize<Config>(json);
                var legacy = serializer.Deserialize<Dictionary<string, object>>(json);
                if (!legacy.ContainsKey("AccurateDictationEnabled")) value.AccurateDictationEnabled = value.QualityEnabled;
                if (!legacy.ContainsKey("NotesHotkey") && legacy.ContainsKey("NotesMicrophoneHotkey")) value.NotesHotkey = Convert.ToString(legacy["NotesMicrophoneHotkey"]);
                if (!legacy.ContainsKey("SetupGuideSeen")) value.SetupGuideSeen = true;
                if (!legacy.ContainsKey("ShowVoiceIndicator")) value.ShowVoiceIndicator = true;
                value.Validate();
                return value;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Invalid configuration: " + ex.Message, ex);
            }
        }

        public static bool IsLoopback(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url ?? "", UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttp) return false;
            IPAddress ip;
            return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || (IPAddress.TryParse(uri.Host.Trim('[', ']'), out ip) && IPAddress.IsLoopback(ip));
        }

        public void Validate()
        {
            SmartCleanupEnabled = false; // Retired preference; old settings must not enable sentence rewriting.
            BallColorMode=BallAppearance=="Color" ? "Solid color" : BallAppearance=="Gradient" ? "Gradient" : "Original colors"; // Legacy field: appearance now determines the finish.
            if(!Regex.IsMatch(BallColor1??"","^#[0-9a-fA-F]{6}$") || !Regex.IsMatch(BallColor2??"","^#[0-9a-fA-F]{6}$")) throw new InvalidDataException("Choose valid ball colors.");
            if(BallGradientAngle<0 || BallGradientAngle>359) throw new InvalidDataException("Choose a gradient angle from 0 to 359 degrees.");
            if(Array.IndexOf(BallCatalog.Appearances,BallAppearance)<0) throw new InvalidDataException("Choose a valid ball appearance.");
            if(BallExpandedSizePercent<100 || BallExpandedSizePercent>200) throw new InvalidDataException("Choose an expanded ball size from 100% to 200%.");
            if(BallSizePercent<BallCatalog.MinSizePercent(BallAppearance) || BallSizePercent>BallCatalog.MaxSizePercent(BallAppearance)) throw new InvalidDataException("Choose a ball size within the slider range.");
            if(BallImagePath==null) BallImagePath="";
            if(Array.IndexOf(BallCatalog.Gravities,BallGravity)<0) throw new InvalidDataException("Choose a valid ball gravity.");
            if ((QualityEnabled || AccurateDictationEnabled) && !File.Exists(QualityPython)) throw new InvalidDataException("Run scripts/setup_accuracy.ps1 to prepare the local speech environment before enabling it.");
            Hotkey = Shortcut.Format(Shortcut.Parse(Hotkey));
            NotesHotkey = Shortcut.Format(Shortcut.Parse(NotesHotkey));
            if (Personalization == null) Personalization = new List<PersonalRule>();
            if (Array.IndexOf(new[] { "Newest", "Oldest", "Title", "Longest" }, NotesSort) < 0) throw new InvalidDataException("Choose a valid note sort order.");
            if (Array.IndexOf(new[] { "PDF", "Word", "Excel", "CSV" }, ExportFormat) < 0) throw new InvalidDataException("Choose a valid export format.");
            if (Array.IndexOf(new[] { "Standard", "Chat", "Email", "Code" }, WritingStyle) < 0) throw new InvalidDataException("Choose a valid writing style.");
            if (TypingWordsPerMinute < 10 || TypingWordsPerMinute > 250) throw new InvalidDataException("Choose a typing speed from 10 to 250 words per minute.");
            Theme = "System"; // Migrate saved appearances to the fixed application palette.
            if (DeletedRetentionDays < 0 || DeletedRetentionDays > 3650) throw new InvalidDataException("Choose a valid retention period.");
            Uri updateUri; if (!String.IsNullOrWhiteSpace(UpdateManifestUrl) && (!Uri.TryCreate(UpdateManifestUrl, UriKind.Absolute, out updateUri) || updateUri.Scheme != "https")) throw new InvalidDataException("Update feeds must use HTTPS.");
            if (!String.IsNullOrWhiteSpace(UpdatePublisherThumbprint) && !Regex.IsMatch(UpdatePublisherThumbprint.Replace(" ", ""), "^[A-Fa-f0-9]{40}$")) throw new InvalidDataException("Use the 40-character Windows certificate thumbprint.");
            PersonalRule.Validate(Personalization);
            if (Hotkey == NotesHotkey) throw new InvalidDataException("Choose a different shortcut for each action.");
            if (Array.IndexOf(new[] { "Microphone", "Computer", "Both" }, MeetingSource) < 0) throw new InvalidDataException("Choose valid meeting audio.");
            if (Array.IndexOf(new[] { "Microphone", "Computer", "Both" }, NotesSource) < 0) throw new InvalidDataException("Choose a valid note audio source.");
            if (Array.IndexOf(new[] { "Auto", "CtrlV", "CtrlShiftV", "ShiftInsert" }, PasteMethod) < 0) throw new InvalidDataException("Choose a supported paste method.");
            if (String.IsNullOrWhiteSpace(Microphone)) throw new InvalidDataException("Microphone must be 'system-default' or a device name.");
            if (!IsLoopback(SttUrl)) throw new InvalidDataException("SttUrl must be a loopback http address (127.0.0.1 or localhost); audio never leaves this PC.");
        }

        public void Save()
        {
            Validate();
            Directory.CreateDirectory(DirectoryPath);
            foreach (var rule in Personalization) Pronunciations.Commit(rule);
            File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(this));
            foreach (var rule in Personalization) rule.PronunciationPcm = null;
        }
    }

    static class DeviceSelector
    {
        // Returns the waveIn device index for the saved choice, or -1 for the Windows default (WAVE_MAPPER).
        // fellBack is true when a specific saved device is no longer present.
        public static int Resolve(string saved, IList<string> names, out bool fellBack)
        {
            fellBack = false;
            if (String.IsNullOrEmpty(saved) || saved == Config.DefaultMic) return -1;
            for (int i = 0; i < names.Count; i++)
                if (String.Equals(names[i], saved, StringComparison.OrdinalIgnoreCase)) return i;
            fellBack = true;
            return -1;
        }
    }

    // Conservative cleanup: the STT already punctuates, so this only removes things that are
    // unambiguously noise and never rewrites words that could carry meaning.
    static class TextCleanup
    {
        static readonly string Dup = "the|a|an|to|of|in|on|and|but|i|it|we|you|my|your|for|with|at|by";

        public static string Clean(string input)
        {
            if (String.IsNullOrWhiteSpace(input)) return "";
            string text = Regex.Replace(SmartCleanup.Basic(input.Trim()), @"\s+", " ");

            // "scratch that" retracts the clause (or previous sentence) before it.
            Match m;
            while ((m = Regex.Match(text, @"(?i)\bscratch that\b[,.;:!?]*\s*")).Success)
            {
                string before = text.Substring(0, m.Index).TrimEnd();
                if (before.Length > 0 && ",.;:!?".IndexOf(before[before.Length - 1]) >= 0) before = before.Substring(0, before.Length - 1);
                int boundary = Math.Max(before.LastIndexOf('.'), Math.Max(before.LastIndexOf('!'), before.LastIndexOf('?')));
                string keep = boundary >= 0 ? before.Substring(0, boundary + 1) : "";
                string after = text.Substring(m.Index + m.Length);
                if (keep.Length > 0 && after.Length > 0) after = Char.ToUpperInvariant(after[0]) + after.Substring(1);
                text = (keep + " " + after).Trim();
            }

            // "Tuesday, I mean Wednesday": only for capitalised/numeric single-word swaps. "I mean it" and
            // "what I mean is" are ordinary speech and are left alone.
            text = Regex.Replace(text, @"\b[A-Z0-9][\w'-]*,\s+(?:I mean|[Nn]o,? wait),?\s+([A-Z0-9][\w'-]*)(?=[\s.!?,]|$)", "$1");

            text = Regex.Replace(text, @"(?i)\bnew paragraph\b[,.]?\s*", "\n\n");
            text = Regex.Replace(text, @"(?i)\bnew line\b[,.]?\s*", "\n");

            text = Regex.Replace(text, @"(?i)(^|[\s,;])(?:um+|uh+|uhm+|erm+|hmm+)(?=[\s,;.!?]|$),?", "$1");
            text = Regex.Replace(text, @"(?i)\b(" + Dup + @")(?:\s+\1\b)+", "$1");
            text = Regex.Replace(text, @"[ \t]+([,.:;!?])", "$1");
            text = Regex.Replace(text, @",\s*,+", ",");
            text = Regex.Replace(text, @"^[\s,;:]+", "");
            text = Regex.Replace(text, @"[ \t]{2,}", " ").Trim();
            if (text.Length == 0) return "";
            text = Char.ToUpperInvariant(text[0]) + text.Substring(1);
            if (!Regex.IsMatch(text, @"[.!?""')\]]$|\n$")) text += ".";
            return text;
        }
    }
}
