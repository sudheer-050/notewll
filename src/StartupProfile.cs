using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.Web.Script.Serialization;
namespace Notewll
{
    static class StartupProfile
    {
        public static void Run(bool warm)
        {
            var clock = Stopwatch.StartNew(); var config = Config.Load(); long configMs = clock.ElapsedMilliseconds;
            var store = new NoteStore(null, true); Application.EnableVisualStyles(); long constructMs, paintMs; int count;
            using (var form = new SettingsForm(config, "", "", store))
            {
                constructMs = clock.ElapsedMilliseconds - configMs; form.CloseToTray = false; form.Show(); Application.DoEvents(); form.Refresh(); Application.DoEvents();
                paintMs = clock.ElapsedMilliseconds; count = form.Notes.HistorySnapshot.Count; form.Close();
            }
            long? warmMs = null; string error = null;
            if (warm) { var timer = Stopwatch.StartNew(); try { if (config.SpeechLanguage != "en") SpeechQuality.Recognize(config, new byte[0], false); else if (config.AccurateDictationEnabled) AccurateSpeech.Warmup(config); else error = SpeechEngine.EnsureRunning(config, 30); } catch (Exception ex) { error = ex.Message; } warmMs = timer.ElapsedMilliseconds; SpeechQuality.Shutdown(); AccurateSpeech.Shutdown(); SpeechEngine.Shutdown(); }
            Console.Write(new JavaScriptSerializer().Serialize(new { ConfigMs = configMs, HubConstructionMs = constructMs, FirstPaintMs = paintMs, WarmEngineMs = warmMs, WarmError = error, SavedEntries = count, HistoryScans = store.ListReadCount, WorkingSetMB = Process.GetCurrentProcess().WorkingSet64 / 1048576, SpeechLanguage = config.SpeechLanguage, Accurate = config.AccurateDictationEnabled, ReadOnly = true }));
        }
    }
}
