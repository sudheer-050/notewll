using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Notewll
{
    static class LunarSurface
    {
        static double Noise(int x, int y)
        {
            unchecked { int n = x * 374761393 + y * 668265263; n = (n ^ (n >> 13)) * 1274126177; return (n & 0x7fffffff) / (double)Int32.MaxValue; }
        }
        static double Smooth(double x, double y)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            double u = x - ix, v = y - iy; u = u * u * (3 - 2 * u); v = v * v * (3 - 2 * v);
            return (Noise(ix, iy) * (1-u) + Noise(ix+1, iy) * u) * (1-v)
                + (Noise(ix, iy+1) * (1-u) + Noise(ix+1, iy+1) * u) * v;
        }
        internal static Bitmap Create(int size)
        {
            var image = new Bitmap(size, size);
            for (int y=0; y<size; y++) for (int x=0; x<size; x++)
            {
                double nx = (x+.5) / size * 2 - 1, ny = (y+.5) / size * 2 - 1;
                double r = nx*nx + ny*ny;
                if (r > 1) continue;
                double z = Math.Sqrt(1-r);
                double maria = Smooth(x/43.0+12, y/37.0+8);
                double terrain = 22 * Smooth(x/9.0,y/9.0) + 14 * Smooth(x/3.0,y/3.0) + 8 * Noise(x,y);
                double light = Math.Max(.12, -.32 * nx - .27 * ny + .9 * z);
                int shade = (int)Math.Max(0, Math.Min(255, (178 + 40 * maria + terrain - (maria < .46 ? 20 : 0)) * (.72 + .28 * light)));
                image.SetPixel(x,y,Color.FromArgb(shade,shade,Math.Min(255,shade+3)));
            }
            using (var g = Graphics.FromImage(image))
            using (var sphere = new GraphicsPath())
            {
                sphere.AddEllipse(1,1,size-2,size-2); g.SetClip(sphere); g.SmoothingMode = SmoothingMode.AntiAlias;
                var random = new Random(19);
                for(int i=0; i<100; i++)
                {
                    float x=(float)random.NextDouble()*size, y=(float)random.NextDouble()*size;
                    float d=(float)(2+Math.Pow(random.NextDouble(),3)*size*.13);
                    using (var floor = new SolidBrush(Color.FromArgb(23,45,49,53))) g.FillEllipse(floor,x,y,d,d*.8f);
                    using (var shadow = new Pen(Color.FromArgb(39,31,33,36),Math.Max(.6f,d*.07f))) g.DrawArc(shadow,x,y,d,d*.8f,190,155);
                    using (var rim = new Pen(Color.FromArgb(48,250,250,243),Math.Max(.5f,d*.055f))) g.DrawArc(rim,x,y,d,d*.8f,12,140);
                }
            }
            return image;
        }
    }
}
