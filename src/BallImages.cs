using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
namespace Notewll
{
    static class BallImages
    {
        internal static string Import(string path)
        {
            var file=new FileInfo(path);
            if(!file.Exists || file.Length>32*1024*1024) throw new InvalidDataException("Choose an image smaller than 32 MB.");
            using(var input=Image.FromFile(path))
            {
                if((long)input.Width*input.Height>64000000) throw new InvalidDataException("Choose an image smaller than 64 megapixels.");
                using(var image=new Bitmap(512,512,PixelFormat.Format32bppPArgb))
                using(var graphics=Graphics.FromImage(image))
                using(var data=new MemoryStream())
                {
                    graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    float side=Math.Min(input.Width,input.Height);
                    graphics.DrawImage(input,new Rectangle(0,0,512,512),(input.Width-side)/2,(input.Height-side)/2,side,side,GraphicsUnit.Pixel);
                    image.Save(data,ImageFormat.Png); var bytes=data.ToArray(); string hash;
                    using(var sha=SHA256.Create()) hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                    string folder=Path.Combine(Config.DirectoryPath,"ball-images"); Directory.CreateDirectory(folder);
                    string target=Path.Combine(folder,hash+".png");
                    if(!File.Exists(target)) File.WriteAllBytes(target,bytes);
                    return target;
                }
            }
        }
        internal static Bitmap AddTo(Bitmap surface,string path)
        {
            if(String.IsNullOrWhiteSpace(path) || !File.Exists(path)) return surface;
            try {
                using(var image=Image.FromFile(path))
                using(var graphics=Graphics.FromImage(surface))
                using(var circle=new GraphicsPath())
                {
                    var destination=new RectangleF(surface.Width*.12f,surface.Height*.12f,surface.Width*.76f,surface.Height*.76f);
                    circle.AddEllipse(destination); graphics.SetClip(circle); graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    float side=Math.Min(image.Width,image.Height);
                    graphics.DrawImage(image,destination,new RectangleF((image.Width-side)/2,(image.Height-side)/2,side,side),GraphicsUnit.Pixel);
                }
            } catch(ArgumentException) { } catch(IOException) { } catch(UnauthorizedAccessException) { } catch(OutOfMemoryException) { } catch(System.Runtime.InteropServices.ExternalException) { }
            return surface;
        }
    }
}
