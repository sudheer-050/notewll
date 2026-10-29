using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using System.Windows.Forms;
namespace Notewll
{
    // Continuous hue + saturation/value range; no list of preset colors.
    sealed class BallColorRange : Control
    {
        float hue,saturation,value;
        bool draggingHue;
        internal event EventHandler Changed;
        internal Color SelectedColor { get { return Hsv(hue,saturation,value); } }
        internal BallColorRange() { DoubleBuffered=true; TabStop=true; AccessibleName="Color range. Hue strip on the right, saturation and brightness on the left."; Cursor=Cursors.Cross; }
        internal void SelectColor(Color color)
        {
            float max=Math.Max(color.R,Math.Max(color.G,color.B))/255f,min=Math.Min(color.R,Math.Min(color.G,color.B))/255f;
            hue=color.GetHue(); value=max; saturation=max==0 ? 0 : (max-min)/max; Invalidate();
            if(Changed!=null) Changed(this,EventArgs.Empty);
        }
        internal static Color Hsv(float h,float s,float v)
        {
            float c=v*s,x=c*(1-Math.Abs((h/60)%2-1)),m=v-c,r=0,g=0,b=0;
            if(h<60) { r=c;g=x; } else if(h<120) { r=x;g=c; } else if(h<180) { g=c;b=x; } else if(h<240) { g=x;b=c; } else if(h<300) { r=x;b=c; } else { r=c;b=x; }
            return Color.FromArgb((int)Math.Round((r+m)*255),(int)Math.Round((g+m)*255),(int)Math.Round((b+m)*255));
        }
        void Pick(Point point)
        {
            float height=Math.Max(1,Height-1),width=Math.Max(1,Width-42);
            if(draggingHue) hue=Math.Max(0,Math.Min(359.99f,point.Y/height*360));
            else { saturation=Math.Max(0,Math.Min(1,point.X/width)); value=1-Math.Max(0,Math.Min(1,point.Y/height)); }
            Invalidate(); if(Changed!=null) Changed(this,EventArgs.Empty);
        }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if(e.Button!=MouseButtons.Left) return; Focus(); draggingHue=e.X>Width-34; Capture=true; Pick(e.Location); }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if(Capture) Pick(e.Location); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Capture=false; }
        protected override bool IsInputKey(Keys key) { return key==Keys.Left || key==Keys.Right || key==Keys.Up || key==Keys.Down || base.IsInputKey(key); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if(e.Control) { if(e.KeyCode==Keys.Up) hue=(hue+3)%360; if(e.KeyCode==Keys.Down) hue=(hue+357)%360; }
            else { if(e.KeyCode==Keys.Left) saturation=Math.Max(0,saturation-.02f); if(e.KeyCode==Keys.Right) saturation=Math.Min(1,saturation+.02f); if(e.KeyCode==Keys.Up) value=Math.Min(1,value+.02f); if(e.KeyCode==Keys.Down) value=Math.Max(0,value-.02f); }
            Invalidate(); if(Changed!=null) Changed(this,EventArgs.Empty); e.Handled=true;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics; var square=new Rectangle(0,0,Math.Max(1,Width-42),Math.Max(1,Height-1));
            using(var saturationFill=new LinearGradientBrush(square,Color.White,Hsv(hue,1,1),0f)) g.FillRectangle(saturationFill,square);
            using(var brightness=new LinearGradientBrush(square,Color.Transparent,Color.Black,90f)) g.FillRectangle(brightness,square);
            var strip=new Rectangle(Width-28,0,27,Math.Max(1,Height-1));
            using(var rainbow=new LinearGradientBrush(strip,Color.Red,Color.Red,90f)) {
                rainbow.InterpolationColors=new ColorBlend { Colors=new[]{Color.Red,Color.Yellow,Color.Lime,Color.Cyan,Color.Blue,Color.Magenta,Color.Red},Positions=new[]{0f,1f/6,2f/6,3f/6,4f/6,5f/6,1f} }; g.FillRectangle(rainbow,strip);
            }
            g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var outline=new Pen(Color.Black,3)) using(var ink=new Pen(Color.White,1)) {
                float x=saturation*square.Width,y=(1-value)*square.Height;
                g.DrawEllipse(outline,x-5,y-5,10,10); g.DrawEllipse(ink,x-5,y-5,10,10);
                float marker=hue/360*strip.Height; g.DrawRectangle(outline,strip.X-1,marker-2,strip.Width+1,4); g.DrawRectangle(ink,strip.X-1,marker-2,strip.Width+1,4);
            }
            if(Focused) ControlPaint.DrawFocusRectangle(g,ClientRectangle);
        }
    }
    sealed class BallColorPicker : Form
    {
        readonly BallColorRange range=new BallColorRange();
        readonly BallPreview preview=new BallPreview();
        readonly TextBox hex=new ThemedTextBox { Width=110,MaxLength=7,AccessibleName="Hex color" };
        readonly Timer refresh=new Timer { Interval=40 };
        readonly Config draft;
        readonly bool second;
        bool updating;
        internal string SelectedHex { get { return hex.Text.ToUpperInvariant(); } }
        internal BallColorPicker(Config config,bool isSecond)
        {
            draft=config.Copy(); second=isSecond;
            Text="Choose ball color"; Icon=AppLogo.Icon; Font=new Font("Segoe UI",10);
            AutoScaleDimensions=new SizeF(96,96); AutoScaleMode=AutoScaleMode.None; ClientSize=new Size(900,430); MinimumSize=new Size(840,430); StartPosition=FormStartPosition.CenterParent; ShowInTaskbar=false;
            var root=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(18),ColumnCount=1,RowCount=3 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); Controls.Add(root);
            var main=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); root.Controls.Add(main,0,0);
            main.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            range.Dock=DockStyle.Fill; range.Margin=new Padding(0,0,18,16); main.Controls.Add(range,0,0);
            preview.Dock=DockStyle.Fill; preview.Margin=new Padding(0,0,0,16); main.Controls.Add(preview,1,0);
            var values=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false,AutoSize=true }; values.Controls.Add(new Label { Text="Hex color",AutoSize=true,Padding=new Padding(0,5,8,0) }); values.Controls.Add(hex);
            values.Controls.Add(new Label { Text="Live preview while you pick.",AutoSize=true,Padding=new Padding(12,5,0,0) }); root.Controls.Add(values,0,1);
            var actions=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,AutoSize=true };
            var use=DialogUi.Button("Use color",(s,e)=>{ if(Regex.IsMatch(hex.Text,"^#[0-9a-fA-F]{6}$")) { DialogResult=DialogResult.OK; Close(); } },true);
            var cancel=DialogUi.Button("Cancel",(s,e)=>{ DialogResult=DialogResult.Cancel; Close(); }); actions.Controls.Add(use); actions.Controls.Add(cancel); root.Controls.Add(actions,0,2); AcceptButton=use; CancelButton=cancel;
            range.Changed+=(s,e)=> { updating=true; var color=range.SelectedColor; hex.Text=String.Format("#{0:X2}{1:X2}{2:X2}",color.R,color.G,color.B); updating=false; if(!refresh.Enabled) refresh.Start(); };
            hex.TextChanged+=(s,e)=> { if(!updating && Regex.IsMatch(hex.Text,"^#[0-9a-fA-F]{6}$")) range.SelectColor(ColorTranslator.FromHtml(hex.Text)); };
            refresh.Tick+=(s,e)=>{ refresh.Stop(); UpdatePreview(); };
            range.SelectColor(ColorTranslator.FromHtml(second?draft.BallColor2:draft.BallColor1));
            ThemeColors.Apply(this,config.Theme); preview.BackColor=ThemeColors.Card; preview.ForeColor=ThemeColors.Ink;
            using(var graphics=CreateGraphics()) { float scale=graphics.DpiX/96f; if(scale!=1) Scale(new SizeF(scale,scale)); }
            Shown+=(s,e)=>UpdatePreview();
        }
        void UpdatePreview()
        {
            if(!Regex.IsMatch(hex.Text,"^#[0-9a-fA-F]{6}$")) return;
            if(second) draft.BallColor2=SelectedHex; else draft.BallColor1=SelectedHex;
            preview.Configure(draft);
        }
        protected override void Dispose(bool disposing) { if(disposing) refresh.Dispose(); base.Dispose(disposing); }
    }
}
