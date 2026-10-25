using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace Notewll
{
    sealed class BallPreview : Control
    {
        readonly Timer timer=new Timer { Interval=15 };
        readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
        readonly BallPhysics ball=new BallPhysics();
        readonly DockButton[] icons={new DockButton { Symbol="conversation" },new DockButton { Symbol="microphone" },new DockButton { Symbol="note" }};
        Bitmap skin;
        string textureKey;
        float rotation;
        int diameter=28;
        float expandedScale=1;
        bool reduced;
        double lastTime;
        internal BallPreview()
        {
            DoubleBuffered=true; AccessibleName="Live ball and expanded tray preview";
            Controls.AddRange(icons); foreach(var icon in icons) { icon.Cursor=Cursors.Default; icon.AccessibleName="Expanded tray preview icon"; }
            timer.Tick+=(s,e)=> {
                double now=clock.Elapsed.TotalSeconds;
                float before=ball.Position.X;
                ball.Step(SceneArea,new Size(diameter,diameter),(float)(now-lastTime),DeviceScale);
                rotation=(rotation+(ball.Position.X-before)/Math.Max(1,diameter/2f)*180f/(float)Math.PI)%360;
                lastTime=now; Invalidate(); if(ball.Settled) timer.Stop();
            };
        }
        Rectangle SceneArea { get { return new Rectangle(10,30,Math.Max(40,Width/2-20),Math.Max(40,Height-54)); } }
        float DeviceScale { get { using(var g=CreateGraphics()) return g.DpiX/96f; } }
        float ExpansionFit { get { return Math.Min(1,Math.Max(.2f,(Width/2f-24)/(BallCatalog.ExpandedDiameter*DeviceScale*expandedScale))); } }
        float ExpansionPixels { get { return BallCatalog.ExpandedDiameter*DeviceScale*expandedScale*ExpansionFit; } }
        PointF ExpandedCenter { get { return new PointF(Math.Min(Width*.76f,Width-ExpansionPixels/2-8),Height-24); } }
        internal void Configure(Config config)
        {
            timer.Stop(); reduced=config.ReducedMotion;
            string key=config.BallAppearance+config.BallColor1+config.BallColor2+config.BallGradientAngle+config.BallImagePath;
            if(skin==null || key!=textureKey) { var next=BallCatalog.Texture(config); if(skin!=null) skin.Dispose(); skin=next; textureKey=key; }
            expandedScale=config.BallExpandedSizePercent/100f; rotation=0; diameter=(int)Math.Round(BallCatalog.Diameter(config)*DeviceScale);
            ball.Configure(config.BallAppearance,config.BallGravity);
            ball.Reset(new PointF(Math.Max(12,Width/4-diameter/2),32),PointF.Empty);
            LayoutIcons(); Invalidate(true);
        }
        void LayoutIcons()
        {
            var center=ExpandedCenter; float scale=DeviceScale*expandedScale*ExpansionFit;
            for(int i=0;i<icons.Length;i++)
            {
                double angle=(-145+i*55)*Math.PI/180;
                icons[i].SetBounds((int)(center.X+BallCatalog.FanRadius*scale*Math.Cos(angle)-BallCatalog.TileDiameter/2f*scale),(int)(center.Y+BallCatalog.FanRadius*scale*Math.Sin(angle)-BallCatalog.TileDiameter/2f*scale),(int)Math.Round(BallCatalog.TileDiameter*scale),(int)Math.Round(BallCatalog.TileDiameter*scale));
            }
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e); if(icons!=null) LayoutIcons(); }
        internal void Drop()
        {
            ball.Reset(new PointF(Math.Max(12,Width/4-diameter/2),32),new PointF(35*DeviceScale,0));
            if(reduced) { ball.Position.Y=Height-24-diameter; Invalidate(); return; }
            lastTime=clock.Elapsed.TotalSeconds; timer.Start();
        }
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if(!Visible) timer.Stop(); }
        internal Color IconColorAt(PointF point)
        {
            if(skin==null) return Color.White; var center=ExpandedCenter; float d=ExpansionPixels;
            int x=Math.Max(0,Math.Min(skin.Width-1,(int)(((point.X-center.X)/d+.5)*skin.Width)));
            int y=Math.Max(0,Math.Min(skin.Height-1,(int)(((point.Y-center.Y)/d+.5)*skin.Height)));
            var pixel=skin.GetPixel(x,y); return pixel.R*.299+pixel.G*.587+pixel.B*.114<135 ? Color.White : Color.FromArgb(45,53,65);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { PaintSurface(e.Graphics,Point.Empty); }
        internal void PaintSurface(Graphics graphics,Point offset)
        {
            var state=graphics.Save(); graphics.TranslateTransform(-offset.X,-offset.Y);
            using(var background=new SolidBrush(BackColor)) graphics.FillRectangle(background,ClientRectangle);
            graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var line=new Pen(Color.FromArgb(147,156,152),1)) graphics.DrawLine(line,10,Height-24,Width/2-10,Height-24);
            using(var text=new SolidBrush(ForeColor)) { graphics.DrawString("Ball",Font,text,10,5); graphics.DrawString(ExpansionFit<.99f ? "Tray (scaled)" : "Expanded tray",Font,text,Width/2+10,5); }
            if(skin!=null)
            {
                var ballState=graphics.Save();
                graphics.TranslateTransform(ball.Position.X+diameter/2f,ball.Position.Y+diameter/2f); graphics.RotateTransform(rotation);
                graphics.DrawImage(skin,new RectangleF(-diameter/2f,-diameter/2f,diameter,diameter)); graphics.Restore(ballState);
                var center=ExpandedCenter; float d=ExpansionPixels;
                var clipped=graphics.Save();
                using(var circle=new GraphicsPath()) {
                    circle.AddEllipse(center.X-d/2,center.Y-d/2,d,d);
                    graphics.SetClip(circle,CombineMode.Intersect);
                    graphics.SetClip(new RectangleF(center.X-d/2,center.Y-d/2,d,d/2),CombineMode.Intersect);
                    graphics.TranslateTransform(center.X,center.Y); graphics.RotateTransform(rotation);
                    graphics.DrawImage(skin,new RectangleF(-d/2,-d/2,d,d));
                }
                graphics.Restore(clipped);
            }
            graphics.Restore(state);
        }
        protected override void Dispose(bool disposing) { if(disposing) { timer.Dispose(); if(skin!=null) skin.Dispose(); } base.Dispose(disposing); }
    }
    sealed partial class SettingsForm
    {
        readonly ComboBox ballAppearance=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList }, ballGravity=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList };
        readonly TrackBar ballSize=new TrackBar { Minimum=50,Maximum=450,Value=100,TickStyle=TickStyle.None,SmallChange=1,LargeChange=10,AccessibleName="Ball size percentage" };
        readonly TrackBar ballExpandedSize=new TrackBar { Minimum=100,Maximum=200,Value=100,TickStyle=TickStyle.None,SmallChange=1,LargeChange=10,AccessibleName="Expanded ball size percentage" };
        readonly NumericUpDown ballAngle=new NumericUpDown { Minimum=0,Maximum=359,Value=45 };
        readonly BallPreview ballPreview=new BallPreview();
        HubButton ballFirst,ballSecond;
        Label ballDetails,ballAngleLabel;
        HubButton ballDropButton,ballResetButton,ballApplyButton;
        Label ballNote,ballSizeLabel,ballImageStatus,ballExpandedSizeLabel;
        HubButton removeBallImage;
        string selectedBallImage="";
        string firstColor,secondColor;
        bool ballLoading;
        Config BallDraft()
        {
            var config=source.Copy();
            config.BallAppearance=Convert.ToString(ballAppearance.SelectedItem); config.BallGravity=Convert.ToString(ballGravity.SelectedItem);
            config.BallColorMode=config.BallAppearance=="Color" ? "Solid color" : config.BallAppearance=="Gradient" ? "Gradient" : "Original colors"; config.BallColor1=firstColor; config.BallColor2=secondColor; config.BallGradientAngle=(int)ballAngle.Value; config.ReducedMotion=motionChoice.Checked;
            config.BallSizePercent=ballSize.Value; config.BallExpandedSizePercent=ballExpandedSize.Value; config.BallImagePath=selectedBallImage;
            return config;
        }
        void RefreshBallDraft()
        {
            if(ballLoading || ballAppearance.SelectedItem==null || ballGravity.SelectedItem==null) return;
            string name=Convert.ToString(ballAppearance.SelectedItem);
            ballLoading=true; ballSize.SetRange(BallCatalog.MinSizePercent(name),BallCatalog.MaxSizePercent(name)); ballLoading=false;
            var config=BallDraft(); var profile=BallCatalog.Profile(config.BallAppearance);
            float scale=ballAppearance.Width/290f;
            int previewHeight=Math.Max(116,Math.Max(BallCatalog.Diameter(config)+70,(int)Math.Ceiling(BallCatalog.ExpandedDiameter*config.BallExpandedSizePercent/200.0)+55));
            ballPreview.Height=(int)Math.Round(previewHeight*scale);
            ballSizeLabel.Text="Ball size: "+ballSize.Value+"%"+(ballSize.Value==100 ? " (default)" : "")+"   |   "+ballSize.Minimum+"% - "+ballSize.Maximum+"%";
            ballExpandedSizeLabel.Text="Expanded ball: "+ballExpandedSize.Value+"%"+(ballExpandedSize.Value==100 ? " (default)" : "")+"   |   100% - 200%";
            ballImageStatus.Text=String.IsNullOrEmpty(selectedBallImage) ? "No custom image" : System.IO.File.Exists(selectedBallImage) ? "Custom image added" : "Image unavailable";
            removeBallImage.Enabled=!String.IsNullOrEmpty(selectedBallImage);
            ballPreview.Configure(config);
            bool custom=config.BallAppearance=="Color" || config.BallAppearance=="Gradient";
            bool gradient=config.BallAppearance=="Gradient";
            ballFirst.Visible=custom; ballSecond.Visible=ballAngle.Visible=ballAngleLabel.Visible=gradient;
            int shift=custom ? 0 : -110;
            var scroll=ballAppearance.Parent as ScrollableControl; int scrollY=scroll==null ? 0 : scroll.AutoScrollPosition.Y;
            ballDetails.Top=(int)Math.Round((438+shift)*scale)+scrollY; ballPreview.Top=(int)Math.Round((510+shift)*scale)+scrollY;
            ballDropButton.Top=ballResetButton.Top=ballApplyButton.Top=(int)Math.Round((642+shift+previewHeight-116)*scale)+scrollY;
            ballNote.Top=(int)Math.Round((702+shift+previewHeight-116)*scale)+scrollY;
            ballFirst.Text="Choose color: "+firstColor; ballSecond.Text="Second color: "+secondColor;
            ballDetails.Text=String.Format("Size: {0}%  |  Mass: about {1:0} g  |  Gravity: {2:0.000} x Earth\n{3}",config.BallSizePercent,profile.Mass*1000,BallCatalog.Gravity(config.BallGravity),custom ? "Plain colored balls use standard cricket-ball properties." : Array.IndexOf(BallCatalog.Gravities,config.BallAppearance)>=0 ? "Planet skins use the same cricket-ball properties." : "Each sports ball has its own bounce and rolling feel.");
        }
        void PickBallColor(bool second)
        {
            var draft=BallDraft();
            using(var picker=new BallColorPicker(draft,second))
                if(picker.ShowDialog(this)==DialogResult.OK) {
                    if(second) secondColor=picker.SelectedHex; else firstColor=picker.SelectedHex;
                    RefreshBallDraft(); MarkDirty();
                }
        }
        void LoadBallChoices(Config config)
        {
            ballLoading=true; selectedBallImage=config.BallImagePath??""; firstColor=config.BallColor1; secondColor=config.BallColor2;
            ballAppearance.SelectedItem=config.BallAppearance; ballGravity.SelectedItem=config.BallGravity; ballSize.SetRange(BallCatalog.MinSizePercent(config.BallAppearance),BallCatalog.MaxSizePercent(config.BallAppearance)); ballSize.Value=Math.Max(ballSize.Minimum,Math.Min(ballSize.Maximum,config.BallSizePercent)); ballExpandedSize.Value=config.BallExpandedSizePercent; ballAngle.Value=config.BallGradientAngle;
            ballLoading=false; RefreshBallDraft();
        }
        void PickBallImage()
        {
            using(var picker=new OpenFileDialog { Title="Choose a ball image from your computer",Filter="Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif",CheckFileExists=true,Multiselect=false })
            {
                if(picker.ShowDialog(this)!=DialogResult.OK) return;
                try { selectedBallImage=BallImages.Import(picker.FileName); RefreshBallDraft(); MarkDirty(); }
                catch(Exception ex) { MessageBox.Show(this,"Could not add image: "+ex.Message,"Ball image",MessageBoxButtons.OK,MessageBoxIcon.Information); }
            }
        }
        void BuildBallPreferences()
        {
            var page=SettingsTab("Ball");
            Label(page,"Ball appearance",20,16,290,28,12,true,Ink); ballAppearance.Items.AddRange(BallCatalog.Appearances); ballAppearance.SetBounds(20,50,290,32); page.Controls.Add(ballAppearance);
            Label(page,"Gravity",330,16,290,28,12,true,Ink); ballGravity.Items.AddRange(BallCatalog.Gravities); ballGravity.SetBounds(330,50,290,32); page.Controls.Add(ballGravity);
            ballSizeLabel=Label(page,"Ball size: 100% (default)",20,98,620,28,11,true,Ink);
            ballSize.SetBounds(20,130,620,38); ballSize.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; page.Controls.Add(ballSize);
            ballExpandedSizeLabel=Label(page,"Expanded ball: 100% (default)",20,180,620,28,11,true,Ink);
            ballExpandedSize.SetBounds(20,212,620,38); ballExpandedSize.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; page.Controls.Add(ballExpandedSize);
            Button(page,"Choose local image",20,270,190,(s,e)=>PickBallImage());
            removeBallImage=Button(page,"Remove image",226,270,150,(s,e)=>{ selectedBallImage=""; RefreshBallDraft(); MarkDirty(); });
            ballImageStatus=Label(page,"No custom image",392,273,230,32,9,false,Muted);
            ballFirst=Button(page,"Choose color",20,328,290,(s,e)=>PickBallColor(false));
            ballSecond=Button(page,"Second color",330,328,290,(s,e)=>PickBallColor(true));
            ballAngleLabel=Label(page,"Gradient angle (degrees)",20,382,300,28,10,false,Muted); ballAngle.SetBounds(330,380,110,32); page.Controls.Add(ballAngle);
            ballDetails=Label(page,"",20,278,620,60,10,false,Muted);
            ballPreview.SetBounds(20,350,620,116); ballPreview.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; ballPreview.BackColor=ThemeColors.Card; page.Controls.Add(ballPreview);
            ballDropButton=Button(page,"Drop preview",20,482,175,(s,e)=>ballPreview.Drop());
            ballResetButton=Button(page,"Reset ball defaults",213,482,195,(s,e)=>{ LoadBallChoices(new Config()); MarkDirty(); });
            ballApplyButton=Button(page,"Apply",426,482,175,(s,e)=>SavePreferences());
            ballNote=Label(page,"The chosen look stays on the expanded tray. Choose Color or Gradient for a plain custom-colored ball.\nSize uses approximate display measurements. Your local image rotates with the ball.",20,542,620,66,9,false,Muted);
            ballAppearance.SelectedIndexChanged+=(s,e)=>{ if(!ballLoading) { RefreshBallDraft(); ballPreview.Drop(); MarkDirty(); } };
            ballGravity.SelectedIndexChanged+=(s,e)=>{ if(!ballLoading) { RefreshBallDraft(); ballPreview.Drop(); MarkDirty(); } };
            ballExpandedSize.ValueChanged+=(s,e)=>{ if(!ballLoading) { RefreshBallDraft(); MarkDirty(); } };
            ballSize.ValueChanged+=(s,e)=>{ if(!ballLoading) { RefreshBallDraft(); MarkDirty(); } };
            ballAngle.ValueChanged+=(s,e)=>{ if(!ballLoading) { RefreshBallDraft(); MarkDirty(); } };
            motionChoice.CheckedChanged+=(s,e)=>RefreshBallDraft();
            LoadBallChoices(source);
        }
    }
}
