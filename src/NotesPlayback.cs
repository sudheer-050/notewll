using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Notewll
{
    sealed class NotesPlayback : IDisposable
    {
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)] static extern int mciSendString(string command, StringBuilder result, int capacity, IntPtr callback);
        readonly string alias = "notes" + Guid.NewGuid().ToString("N");
        bool opened;
        void Command(string command)
        {
            int error = mciSendString(command, null, 0, IntPtr.Zero); if (error != 0) throw new IOException("Windows could not play this recording (audio error " + error + ").");
        }
        public void Open(string path)
        {
            Dispose(); if (!File.Exists(path) || path.IndexOf('"') >= 0) throw new IOException("The selected recording is unavailable.");
            Command("open \"" + Path.GetFullPath(path) + "\" type mpegvideo alias " + alias); opened = true; Command("set " + alias + " time format milliseconds");
        }
        public void Play(int milliseconds = -1) { if (!opened) throw new IOException("Choose a recording first."); Command("play " + alias + (milliseconds < 0 ? "" : " from " + milliseconds)); }
        public void PlaySample(int start, int end) { if (!opened) throw new IOException("Choose a recording first."); Command("play " + alias + " from " + Math.Max(0, start) + " to " + Math.Max(start + 1, end)); }
        public void Pause() { if (opened) Command("pause " + alias); }
        public void Speed(double rate) { if (opened) Command("set " + alias + " speed " + (int)(rate * 1000)); }
        public int Position
        {
            get { if (!opened) return 0; var result = new StringBuilder(64); int value; return mciSendString("status " + alias + " position", result, result.Capacity, IntPtr.Zero) == 0 && Int32.TryParse(result.ToString(), out value) ? value : 0; }
        }
        public void Dispose() { if (opened) { mciSendString("close " + alias, null, 0, IntPtr.Zero); opened = false; } }
    }
}
