using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
namespace Notewll
{
    sealed class BallProfile
    {
        internal string Name;
        internal int Diameter;
        internal float Mass, PhysicalDiameter, Bounce, Friction;
        internal BallProfile(string name,int diameter,float mass,float physicalDiameter,float bounce,float friction)
        { Name=name; Diameter=diameter; Mass=mass; PhysicalDiameter=physicalDiameter; Bounce=bounce; Friction=friction; }
    }
    static class BallCatalog
    {
        internal const int ExpandedDiameter=140, TileDiameter=33;
        internal const float FanRadius=50f;
        internal static readonly string[] Appearances={"Color","Gradient","Basketball","Football / soccer ball","Cricket ball","Rubber ball","Tennis ball","Golf ball","Baseball","Volleyball","Moon","Mercury","Venus","Earth","Mars","Jupiter","Saturn","Uranus","Neptune"};
        internal static readonly string[] Gravities={"Moon","Mercury","Venus","Earth","Mars","Jupiter","Saturn","Uranus","Neptune"};
        // NASA NSSDCA Planetary Fact Sheet, relative to Earth (moon rounded to 0.166).
        // https://nssdc.gsfc.nasa.gov/planetary/factsheet/planet_table_ratio.html
        static readonly float[] gravity={.166f,.378f,.907f,1,.377f,2.36f,.916f,.889f,1.12f};
        static readonly BallProfile[] sports={
            new BallProfile("Basketball",32,.624f,.24f,.76f,.65f),
            new BallProfile("Football / soccer ball",30,.43f,.22f,.68f,.85f),
            new BallProfile("Cricket ball",28,.156f,.072f,.52f,1),
            new BallProfile("Rubber ball",24,.07f,.06f,.82f,.8f),
            new BallProfile("Tennis ball",26,.058f,.067f,.68f,1.15f),
            new BallProfile("Golf ball",22,.046f,.043f,.78f,.55f),
            new BallProfile("Baseball",26,.145f,.073f,.55f,.95f),
            new BallProfile("Volleyball",30,.27f,.21f,.72f,.8f)};
        static readonly Dictionary<string,Bitmap> textures=new Dictionary<string,Bitmap>();
        internal static BallProfile Profile(string name)
        {
            foreach(var profile in sports) if(profile.Name==name) return profile;
            return new BallProfile(name,28,.156f,.072f,.52f,1);
        }
        // Approximate physical display sizes, expressed in DPI-independent pixels.
        internal static int MinSizePercent(string name) { return (int)Math.Ceiling(.5*96/2.54/Profile(name).Diameter*100); }
        internal static int MaxSizePercent(string name) { return (int)Math.Floor(2.5*96/2.54/Profile(name).Diameter*100); }
        internal static int Diameter(Config config) { return (int)Math.Round(Profile(config.BallAppearance).Diameter*config.BallSizePercent/100.0); }
        internal static float Gravity(string name) { int i=Array.IndexOf(Gravities,name); return i<0 ? 1 : gravity[i]; }
        internal static Bitmap Texture(Config config)
        {
            if(config.BallAppearance!="Color" && config.BallAppearance!="Gradient") return BallImages.AddTo((Bitmap)Texture(config.BallAppearance).Clone(),config.BallImagePath);
            var original=Texture(config.BallAppearance); var result=new Bitmap(original.Width,original.Height);
            Color first=ColorTranslator.FromHtml(config.BallColor1), second=ColorTranslator.FromHtml(config.BallColor2);
            double angle=config.BallGradientAngle*Math.PI/180, nx=Math.Cos(angle), ny=Math.Sin(angle), span=Math.Abs(nx)+Math.Abs(ny);
            for(int y=0;y<result.Height;y++) for(int x=0;x<result.Width;x++)
            {
                Color pixel=original.GetPixel(x,y); if(pixel.A==0) continue;
                float t=config.BallAppearance=="Gradient" ? (float)Math.Max(0,Math.Min(1,.5+((x/255.0-.5)*nx+(y/255.0-.5)*ny)/span)) : 0;
                float light=.15f+.85f*Math.Max(pixel.R,Math.Max(pixel.G,pixel.B))/255f;
                result.SetPixel(x,y,Color.FromArgb(pixel.A,(int)((first.R+(second.R-first.R)*t)*light),(int)((first.G+(second.G-first.G)*t)*light),(int)((first.B+(second.B-first.B)*t)*light)));
            }
            return BallImages.AddTo(result,config.BallImagePath);
        }
        internal static Bitmap Texture(string name)
        {
            Bitmap image;
            if(!textures.TryGetValue(name,out image)) { image=BallSkin.Create(name); textures.Add(name,image); }
            return image;
        }
    }
    static class BallSkin
    {
        static Color Blend(Color a,Color b,float t) { t=Math.Max(0,Math.Min(1,t)); return Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t)); }
        internal static Bitmap Create(string name)
        {
            if(name=="Moon" || name=="Mercury")
            {
                var moon=LunarSurface.Create(256);
                if(name=="Mercury") using(var g=Graphics.FromImage(moon)) using(var tint=new SolidBrush(Color.FromArgb(35,120,105,90))) g.FillEllipse(tint,0,0,256,256);
                return moon;
            }
            Color main=name=="Color" || name=="Gradient" ? Color.White : Color.Ivory;
            switch(name) {
                case "Basketball": main=Color.FromArgb(229,113,32); break;
                case "Cricket ball": main=Color.FromArgb(177,30,43); break;
                case "Rubber ball": main=Color.FromArgb(57,154,222); break;
                case "Tennis ball": main=Color.FromArgb(209,229,61); break;
                case "Venus": main=Color.FromArgb(225,188,119); break;
                case "Earth": main=Color.FromArgb(40,115,203); break;
                case "Mars": main=Color.FromArgb(209,115,76); break;
                case "Jupiter": main=Color.FromArgb(215,186,151); break;
                case "Saturn": main=Color.FromArgb(222,205,156); break;
                case "Uranus": main=Color.FromArgb(151,220,224); break;
                case "Neptune": main=Color.FromArgb(48,104,213); break;
            }
            var image=new Bitmap(256,256);
            bool planet=Array.IndexOf(BallCatalog.Gravities,name)>=0;
            for(int y=0;y<256;y++) for(int x=0;x<256;x++)
            {
                float nx=(x-127.5f)/128,ny=(y-127.5f)/128,r=nx*nx+ny*ny;
                if(r>1) continue;
                float z=(float)Math.Sqrt(1-r);
                float light=.68f+.32f*Math.Max(0,-.32f*nx-.27f*ny+.9f*z);
                float noise=(float)(Math.Sin(x*1.37+y*3.17)*Math.Sin(x*.23-y*.61));
                Color c=main;
                if(planet)
                {
                    float band=(float)(.5+.5*Math.Sin(y*.21+3*Math.Sin(x*.018)+Math.Sin(y*.043)));
                    if(name=="Jupiter" || name=="Saturn" || name=="Venus") c=Blend(main,Color.FromArgb(153,107,74),band*(name=="Jupiter"?.4f:.22f));
                    if(name=="Mars") c=Blend(main,Color.FromArgb(100,67,58),(float)(.2+.18*Math.Sin(x*.026)*Math.Sin(y*.037)));
                    if(name=="Uranus" || name=="Neptune") c=Blend(main,Color.White,band*.09f);
                }
                float grain=name=="Color" || name=="Gradient" ? 0 : (name=="Basketball"?.05f:.018f)*noise;
                image.SetPixel(x,y,Color.FromArgb((int)Math.Max(0,Math.Min(255,c.R*(light+grain))),(int)Math.Max(0,Math.Min(255,c.G*(light+grain))),(int)Math.Max(0,Math.Min(255,c.B*(light+grain)))));
            }
            using(var g=Graphics.FromImage(image)) using(var sphere=new GraphicsPath())
            {
                g.SmoothingMode=SmoothingMode.AntiAlias; sphere.AddEllipse(1,1,254,254); g.SetClip(sphere);
                using(var dark=new Pen(Color.FromArgb(210,43,40,35),5)) using(var pale=new Pen(Color.FromArgb(240,248,244,211),5))
                {
                    if(name=="Basketball") { g.DrawLine(dark,128,0,128,256); g.DrawLine(dark,0,128,256,128); g.DrawEllipse(dark,60,-32,136,320); g.DrawEllipse(dark,-34,60,324,136); }
                    if(name=="Tennis ball") { g.DrawArc(pale,-95,-10,190,276,-70,140); g.DrawArc(pale,161,-10,190,276,110,140); }
                    if(name=="Cricket ball") {
                        g.DrawArc(pale,88,-8,70,272,85,190); g.DrawArc(pale,100,-8,70,272,85,190);
                        using(var stitches=new Pen(Color.FromArgb(220,239,210,174),2)) for(int y=8;y<256;y+=11) { g.DrawLine(stitches,105,y,113,y+3); g.DrawLine(stitches,132,y,140,y+3); }
                    }
                    if(name=="Rubber ball") { using(var red=new Pen(Color.FromArgb(220,245,109,60),13)) g.DrawEllipse(red,-26,70,308,112); }
                    if(name=="Football / soccer ball") {
                        foreach(var c in new[]{new PointF(128,128),new PointF(30,45),new PointF(220,50),new PointF(48,226),new PointF(220,220)}) {
                            var points=new PointF[5]; for(int j=0;j<5;j++) points[j]=new PointF(c.X+37*(float)Math.Cos(j*Math.PI*2/5-Math.PI/2),c.Y+37*(float)Math.Sin(j*Math.PI*2/5-Math.PI/2));
                            using(var fill=new SolidBrush(Color.FromArgb(35,39,44))) g.FillPolygon(fill,points);
                            foreach(var p in points) using(var seam=new Pen(Color.FromArgb(110,65,65,65),2)) g.DrawLine(seam,p,new PointF(128+(p.X-128)*2.6f,128+(p.Y-128)*2.6f));
                        }
                    }
                    if(name=="Baseball") {
                        using(var red=new Pen(Color.FromArgb(192,48,47),3)) { g.DrawArc(red,-60,8,170,240,-85,170); g.DrawArc(red,146,8,170,240,95,170); for(int y=25;y<240;y+=15) { float x=52+44*(float)Math.Sin(y*Math.PI/256); g.DrawLine(red,x-5,y-4,x+5,y+4); g.DrawLine(red,251-x,y-4,261-x,y+4); } }
                    }
                    if(name=="Volleyball") {
                        for(int i=0;i<6;i++) { var state=g.Save(); g.TranslateTransform(128,128); g.RotateTransform(i*60); g.DrawBezier(dark,0,0,50,-25,120,-20,145,30); g.DrawBezier(dark,10,20,70,-5,110,15,135,48); g.Restore(state); }
                    }
                    if(name=="Golf ball") using(var dimple=new Pen(Color.FromArgb(90,101,111,119),1.4f)) for(int y=12;y<256;y+=17) for(int x=10+(y/17%2)*8;x<256;x+=17) g.DrawEllipse(dimple,x,y,8,7);
                }
                if(name=="Earth") {
                    using(var land=new SolidBrush(Color.FromArgb(186,92,151,103))) {
                        g.FillClosedCurve(land,new[]{new PointF(25,50),new PointF(75,25),new PointF(113,47),new PointF(87,78),new PointF(107,116),new PointF(72,101),new PointF(50,75)});
                        g.FillClosedCurve(land,new[]{new PointF(88,123),new PointF(121,139),new PointF(119,176),new PointF(96,223),new PointF(81,179)});
                        g.FillClosedCurve(land,new[]{new PointF(150,58),new PointF(199,40),new PointF(241,64),new PointF(210,115),new PointF(185,106),new PointF(179,167),new PointF(148,129),new PointF(150,94),new PointF(132,79)});
                        g.FillEllipse(land,198,182,37,23);
                    }
                    using(var clouds=new Pen(Color.FromArgb(135,249,253,255),5)) { g.DrawArc(clouds,10,42,225,64,180,160); g.DrawArc(clouds,38,138,211,40,12,155); }
                }
                if(name=="Mars") using(var cap=new SolidBrush(Color.FromArgb(190,239,223,207))) g.FillEllipse(cap,85,-8,86,30);
                if(name=="Jupiter") using(var storm=new SolidBrush(Color.FromArgb(170,175,81,54))) g.FillEllipse(storm,157,150,57,28);
                if(name=="Saturn") { var state=g.Save(); g.TranslateTransform(128,128); g.RotateTransform(-24); using(var ring=new Pen(Color.FromArgb(155,245,228,178),13)) g.DrawEllipse(ring,-141,-30,282,60); using(var gap=new Pen(Color.FromArgb(140,95,80,58),3)) g.DrawEllipse(gap,-139,-30,278,60); g.Restore(state); }
            }
            return image;
        }
    }
}
