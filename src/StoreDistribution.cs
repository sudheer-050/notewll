using System;
using System.Diagnostics;
#if STORE_BUILD
using Windows.ApplicationModel;
#endif

namespace Notewll
{
    static class StoreDistribution
    {
        public static string DisplayVersion { get { return typeof(StoreDistribution).Assembly.GetName().Version.ToString(3); } }
        public static bool Enabled
        {
            get
            {
#if STORE_BUILD
                return true;
#else
                return false;
#endif
            }
        }
        public static void OpenUpdates() { Process.Start(new ProcessStartInfo("ms-windows-store://downloadsandupdates") { UseShellExecute = true }); }
        public static bool StartupEnabled
        {
            get
            {
#if STORE_BUILD
                return StartupTask.GetAsync("NotewllStartup").AsTask().GetAwaiter().GetResult().State == StartupTaskState.Enabled;
#else
                return false;
#endif
            }
        }
        public static void SetStartup(bool enabled)
        {
#if STORE_BUILD
            var task = StartupTask.GetAsync("NotewllStartup").AsTask().GetAwaiter().GetResult();
            if (!enabled) { task.Disable(); return; }
            var state = task.RequestEnableAsync().AsTask().GetAwaiter().GetResult();
            if (state != StartupTaskState.Enabled) throw new InvalidOperationException("Windows has disabled Notewll startup. Enable Notewll in Windows Settings > Apps > Startup.");
#endif
        }
    }
}
