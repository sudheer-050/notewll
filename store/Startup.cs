using System;
using System.Diagnostics;
using System.IO;
static class StoreStartup
{
    static void Main() { Process.Start(new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Notewll.exe"), "--background") { UseShellExecute = false, CreateNoWindow = true }); }
}
