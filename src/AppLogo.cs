using System.Drawing;
using System.Reflection;

namespace Notewll
{
    static class AppLogo
    {
        public static readonly Icon Icon = LoadIcon();
        public static readonly Image Image = LoadImage();
        static Icon LoadIcon()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AppLogo.ico"))
            using (var icon = new Icon(stream)) return (Icon)icon.Clone();
        }
        static Image LoadImage()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AppLogo.png"))
            using (var image = new Bitmap(stream)) return new Bitmap(image);
        }
    }
}
