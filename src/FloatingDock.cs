using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Notewll
{
    sealed class DockButton : Control
    {
        internal static readonly Color Canvas = Color.FromArgb(195, 199, 199);
        public bool Active, Working;
        public float Morph = 1;
        public string Symbol;
        internal static float IconPixels(float scale) { return 28f * scale; }
        public DockButton()
        {
            BackColor = Canvas; TabStop = false; Cursor = Cursors.Hand; AccessibleRole = AccessibleRole.PushButton;
            // A plain control has no native button hover renderer to paint over our glyphs.
            SetStyle(ControlStyles.Selectable | ControlStyles.StandardDoubleClick, false);
            SetStyle(ControlStyles.StandardClick | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if(Width<=0 || Height<=0) return;
            var old=Region;
            using(var circle=new GraphicsPath()) { circle.AddEllipse(0,0,Width,Height); Region=new Region(circle); }
            if(old!=null) old.Dispose();
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Respect the invalidated clip and the caller's translated drawing origin.
            var dock = Parent as FloatingDock;
            if (dock != null) dock.PaintSurface(e.Graphics, new Point(Left, Top));
            else if(Parent is BallPreview) ((BallPreview)Parent).PaintSurface(e.Graphics,new Point(Left,Top));
            else using (var fill = new SolidBrush(Canvas)) e.Graphics.FillRectangle(fill, ClientRectangle);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            var saved = g.Save();
            if (Morph > 0)
            {
                using(var fill=new SolidBrush(Color.FromArgb((int)(128*Morph),Color.White)))
                    g.FillEllipse(fill,.5f,.5f,Width-1,Height-1);
            }
            float scale = Math.Min(Width, Height) / 44f;
            float iconScale = IconPixels(scale) / 28f;
            if (Morph < 1)
            {
                using (var dot = new SolidBrush(Color.FromArgb((int)(255 * (1 - Morph)), Parent is FloatingDock ? ((FloatingDock)Parent).IconColorAt(new PointF(Left+Width/2f,Top+Height/2f)) : Parent is BallPreview ? ((BallPreview)Parent).IconColorAt(new PointF(Left+Width/2f,Top+Height/2f)) : Color.FromArgb(65,75,89))))
                    g.FillEllipse(dot, Width / 2f - 1.6f, Height / 2f - 1.6f, 3.2f, 3.2f);
                if (Morph <= 0) { g.Restore(saved); return; }
                iconScale *= Morph;
            }
            g.TranslateTransform(Width / 2f, Height / 2f); g.ScaleTransform(iconScale, iconScale); g.TranslateTransform(-22, -22);
            Color ink = !Enabled ? Color.FromArgb(100,100,100) : Color.Black;
            ink = Color.FromArgb((int)(255 * Morph), ink);
            using (var pen = new Pen(ink, 2.2f))
            {
                pen.StartCap = pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                if (Working) { g.DrawArc(pen, 11, 11, 22, 22, 25, 280); g.DrawLine(pen, 19, 19, 25, 25); g.DrawLine(pen, 25, 19, 19, 25); }
                else if (Active) { using (var path = Surface.Shape(new RectangleF(15, 15, 14, 14), 3)) using (var fill = new SolidBrush(ink)) g.FillPath(fill, path); }
                else if (Symbol == "microphone")
                {
                    using (var path = Surface.Shape(new RectangleF(18, 10, 8, 16), 4)) g.DrawPath(pen, path);
                    g.DrawLine(pen, 14, 19, 14, 23); g.DrawLine(pen, 30, 19, 30, 23); g.DrawArc(pen, 14, 15, 16, 16, 0, 180); g.DrawLine(pen, 22, 31, 22, 35); g.DrawLine(pen, 17, 35, 27, 35);
                }
                else if (Symbol == "note")
                {
                    using (var path = new GraphicsPath()) { path.AddLines(new[] { new PointF(14, 10), new PointF(25, 10), new PointF(32, 17), new PointF(32, 32), new PointF(12, 32), new PointF(12, 12), new PointF(14, 10) }); g.DrawPath(pen, path); }
                    g.DrawLines(pen, new[] { new PointF(25, 10), new PointF(25, 17), new PointF(32, 17) }); g.DrawLine(pen, 17, 22, 27, 22); g.DrawLine(pen, 17, 27, 24, 27);
                }
                else
                {
                    using (var path = Surface.Shape(new RectangleF(9, 10, 20, 15), 4)) g.DrawPath(pen, path);
                    g.DrawLines(pen, new[] { new PointF(14, 25), new PointF(12, 29), new PointF(20, 25) });
                    g.DrawLines(pen, new[] { new PointF(33, 19), new PointF(35, 21), new PointF(35, 31), new PointF(31, 31), new PointF(33, 35), new PointF(26, 31), new PointF(22, 31) });
                    g.DrawLine(pen, 15, 17, 23, 17);
                }
            }
            g.Restore(saved);
        }
    }
    // A non-activating controller: clicking Write keeps the user's original text field focused.
    sealed class FloatingDock : Form
    {
        readonly DockButton write = new DockButton { Text = "Dictate", AccessibleName = "Toggle dictation" }, note = new DockButton { Text = "Note", AccessibleName = "Toggle personal note" }, meeting = new DockButton { Text = "Meeting", AccessibleName = "Toggle meeting note" };
        readonly Panel grip = new Panel { Cursor = Cursors.SizeAll, BackColor = Color.Transparent };
        readonly BallPhysics ball = new BallPhysics();
        readonly Timer falling = new Timer { Interval = 15 };
        readonly System.Diagnostics.Stopwatch motionClock = System.Diagnostics.Stopwatch.StartNew();
        double physicsTime, dragTime;
        PointF releaseVelocity;
        Rectangle fallArea;
        bool caughtInFlight;
        float rotation;
        readonly Timer hover = new Timer { Interval = 15 };
        readonly Timer morphAnimation = new Timer { Interval = 15 };
        float morph;
        int dockEdge; // left, right, top, bottom
        PointF fanCenter;
        int trayDiameter;
        readonly Timer signalAnimation = new Timer { Interval = 25 };
        public Func<int> InputLevel;
        public bool ShowVoiceIndicator = true;
        int signalMode;
        float signalLevel, signalPhase;
        bool reducedMotion;
        public bool HasVoiceIndicator { get { return signalMode != 0 && Visible; } }
        bool dragging, expanded, dragMoved;
        int lastMotion;
        BallProfile profile=BallCatalog.Profile("Moon");
        int idleDiameter=28;
        float expandedScale=1;
        Bitmap skin=(Bitmap)BallCatalog.Texture("Moon").Clone();
        internal string Appearance { get; private set; }
        internal string Gravity { get; private set; }
        internal void ConfigureBall(Config config)
        {
            ConfigureBall(config.BallAppearance,config.BallGravity);
            var next=BallCatalog.Texture(config); skin.Dispose(); skin=next; idleDiameter=BallCatalog.Diameter(config); expandedScale=config.BallExpandedSizePercent/100f; SetExpanded(expanded,true);
        }
        internal void ConfigureBall(string appearance,string gravity)
        {
            if(Array.IndexOf(BallCatalog.Appearances,appearance)<0 || Array.IndexOf(BallCatalog.Gravities,gravity)<0) throw new ArgumentException("Unknown ball preference.");
            if(falling.Enabled) FinishFall();
            profile=BallCatalog.Profile(appearance); var next=BallCatalog.Texture(new Config { BallAppearance=appearance }); skin.Dispose(); skin=next;
            idleDiameter=profile.Diameter; expandedScale=1;
            Appearance=appearance; Gravity=gravity; rotation=0;
            ball.Configure(appearance,gravity); SetExpanded(expanded,true);
        }
        float dpi = 1;
        Point restingPosition;
        Point dragStart, locationStart;
        public Action ToggleWrite, ToggleNote, ToggleMeeting, CancelWrite;
        bool processingDictation, enabled = true;
        public string VisibilityDescription { get { return !enabled ? "Hidden in preferences" : snoozedUntil != DateTime.MinValue ? "Hidden until " + snoozedUntil.ToString("t") : "Ready on the screen edge"; } }
        DateTime snoozedUntil;
        public Action<Point> PositionSaved;
        public FloatingDock()
        {
            Text = "Notewll toolbar"; Icon = AppLogo.Icon; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; AutoScaleMode = AutoScaleMode.None; BackColor = DockButton.Canvas; DoubleBuffered = true; Opacity = .96; ClientSize = new Size(28, 28); StartPosition = FormStartPosition.Manual;
            Controls.AddRange(new Control[] { grip, write, note, meeting });
            write.Click += (s, e) => { if (processingDictation) { if (CancelWrite != null) CancelWrite(); } else if (ToggleWrite != null) ToggleWrite(); };
            note.Click += (s, e) => { if (ToggleNote != null) ToggleNote(); }; meeting.Click += (s, e) => { if (ToggleMeeting != null) ToggleMeeting(); };
            grip.MouseDown += DragStart; MouseDown += DragStart;
            grip.MouseMove += DragMove; MouseMove += DragMove;
            grip.MouseUp += DragEnd; MouseUp += DragEnd;
            grip.MouseCaptureChanged += CaptureChanged; MouseCaptureChanged += CaptureChanged;
            using (var graphics = CreateGraphics()) dpi = graphics.DpiX / 96f;
            write.Symbol = "microphone"; note.Symbol = "note"; meeting.Symbol = "conversation";
            hover.Tick += (s, e) =>
            {
                if (snoozedUntil != DateTime.MinValue) { if (DateTime.Now >= snoozedUntil) { snoozedUntil = DateTime.MinValue; if (enabled) Show(); } return; }
                if (!Visible) return;
                if (dragging) { if (dragMoved) AdvanceDrag(); return; }
                if (!falling.Enabled) CheckHover();
            };

            foreach (Control child in Controls) child.MouseLeave += (s, e) => CheckHover();
            MouseLeave += (s, e) => CheckHover();
            falling.Tick += (s,e) => AdvanceFall();
            morphAnimation.Tick += (s, e) => AdvanceMorph();
            signalAnimation.Tick += (s, e) => AdvanceSignal();
            MouseClick += (s, e) =>
            {
                if (e.Button != MouseButtons.Left || dragMoved) return;
                if (signalMode == 0) return; // Idle clicks are handled on release, after drag capture ends.
                if (processingDictation) { if (CancelWrite != null) CancelWrite(); }
                else if (write.Active) { if (ToggleWrite != null) ToggleWrite(); }
                else if (meeting.Active) { if (ToggleMeeting != null) ToggleMeeting(); }
                else if (note.Active && ToggleNote != null) ToggleNote();
            };
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplaysChanged;
            SetExpanded(false, true); hover.Start();
        }
        public void Reveal() { enabled = true; snoozedUntil = DateTime.MinValue; Show(); }
        public void SetVisibility(bool value) { enabled = value; snoozedUntil = DateTime.MinValue; if (enabled) Show(); else Hide(); }
        public void Configure(bool reduced) { reducedMotion = reduced; if (reduced && falling.Enabled) FinishFall(); if (reduced) SetExpanded(expanded, true); UpdateSignalTimer(); }
        void UpdateSignalTimer()
        {
            if (Visible && signalMode == 1 && !reducedMotion) signalAnimation.Start();
            else signalAnimation.Stop();
        }
        void AdvanceSignal()
        {
            if (signalMode != 1 || reducedMotion) return;
            float sample = InputLevel == null ? 0 : Math.Max(0, InputLevel());
            float wanted = sample <= 70 ? 0 : Math.Min(1f, (float)Math.Sqrt((sample - 70) / 1600f));
            float previous = signalLevel;
            signalLevel += (wanted - signalLevel) * (wanted > signalLevel ? .35f : .3f);
            if (wanted == 0 && signalLevel < .008f) signalLevel = 0;
            if (signalLevel > 0) signalPhase += .075f;
            if (signalLevel > 0 || previous != signalLevel) Invalidate();
        }
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); UpdateSignalTimer(); if (!Visible && falling.Enabled) FinishFall(); }
        public void Snooze() { if (!enabled) return; snoozedUntil = DateTime.Now.AddHours(1); Hide(); }
        void DisplaysChanged(object sender, EventArgs e) { if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)(() => RestorePosition(restingPosition.X, restingPosition.Y))); }
        int Px(int value) { return (int)Math.Round(value * dpi); }
        void CheckHover()
        {
            if (!Visible || dragging || falling.Enabled || signalMode != 0) return;
            var point = PointToClient(Cursor.Position);
            if (expanded && !(ClientRectangle.Contains(point) && Region != null && Region.IsVisible(point))) SetExpanded(false);
        }
        void AdvanceMorph()
        {
            float target = expanded ? 1 : 0;
            morph = Math.Max(0, Math.Min(1, morph + (expanded ? .04f : -.055f)));
            LayoutDock();
            if (morph == target) morphAnimation.Stop();
        }
        internal void SetExpanded(bool value, bool force = false)
        {
            if (!force && expanded == value) return;
            expanded = value;
            if (force || reducedMotion || signalMode != 0)
            {
                morphAnimation.Stop(); morph = value ? 1 : 0; LayoutDock();
            }
            else morphAnimation.Start();
        }
        void LayoutDock()
        {
            var area = Screen.FromPoint(new Point(restingPosition.X + Px(idleDiameter/2), restingPosition.Y + Px(idleDiameter/2))).WorkingArea;
            int left = Math.Abs(restingPosition.X - area.Left), right = Math.Abs(area.Right - restingPosition.X - Px(idleDiameter));
            int top = Math.Abs(restingPosition.Y - area.Top), bottom = Math.Abs(area.Bottom - restingPosition.Y - Px(idleDiameter));
            int nearest = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
            dockEdge = nearest == left ? 0 : nearest == right ? 1 : nearest == top ? 2 : 3;
            float eased = morph * morph * (3 - 2 * morph);
            int diameter = signalMode != 0 ? 36 : (int)Math.Round(idleDiameter + (BallCatalog.ExpandedDiameter*expandedScale-idleDiameter) * eased);
            trayDiameter = Px(diameter);
            bool side = dockEdge < 2;
            int depth = (int)Math.Round(trayDiameter * (1 - .5f * eased));
            ClientSize = signalMode != 0 ? new Size(trayDiameter, trayDiameter)
                : side ? new Size(depth, trayDiameter) : new Size(trayDiameter, depth);
            float centerInset = trayDiameter / 2f * (1 - eased);
            fanCenter = signalMode != 0 ? new PointF(Width / 2f, Height / 2f)
                : new PointF(dockEdge == 0 ? centerInset : dockEdge == 1 ? Width - centerInset : Width / 2f,
                    dockEdge == 2 ? centerInset : dockEdge == 3 ? Height - centerInset : Height / 2f);
            grip.Visible = false;
            var buttons = new[] { write, note, meeting };
            float inward = dockEdge == 0 ? 0 : dockEdge == 1 ? 180 : dockEdge == 2 ? 90 : -90;
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].Visible = morph > 0 && signalMode == 0;
                // Dots travel out first, then unfurl into their action glyphs.
                buttons[i].Morph = Math.Max(0, Math.Min(1, (morph - .25f) / .65f));
                double angle = (inward + (i - 1) * 55) * Math.PI / 180;
                float startX = fanCenter.X + Px((i - 1) * 5);
                float startY = fanCenter.Y;
                float endX = fanCenter.X + BallCatalog.FanRadius*expandedScale*dpi * (float)Math.Cos(angle);
                float endY = fanCenter.Y + BallCatalog.FanRadius*expandedScale*dpi * (float)Math.Sin(angle);
                int buttonSize = Px((int)Math.Round(6 + (BallCatalog.TileDiameter*expandedScale-6) * eased));
                buttons[i].SetBounds((int)Math.Round(startX + (endX - startX) * eased - buttonSize / 2f),
                    (int)Math.Round(startY + (endY - startY) * eased - buttonSize / 2f), buttonSize, buttonSize);
            }
            var position = new Point(restingPosition.X + Px(idleDiameter/2) - Width / 2, restingPosition.Y + Px(idleDiameter/2) - Height / 2);
            if (dockEdge == 0) position.X = area.Left;
            else if (dockEdge == 1) position.X = area.Right - Width;
            else if (dockEdge == 2) position.Y = area.Top;
            else position.Y = area.Bottom - Height;
            Location = Clamp(position, area, Size);
            var oldRegion = Region;
            using (var shape = new GraphicsPath())
            {
                shape.AddEllipse(fanCenter.X - trayDiameter / 2f, fanCenter.Y - trayDiameter / 2f, trayDiameter, trayDiameter);
                var region = new Region(shape); region.Intersect(ClientRectangle); Region = region;
            }
            if (oldRegion != null) oldRegion.Dispose();
            Invalidate(true);
        }

        internal Color IconColorAt(PointF point)
        {
            double angle=-rotation*Math.PI/180;
            float dx=point.X-fanCenter.X,dy=point.Y-fanCenter.Y;
            float x=(float)(dx*Math.Cos(angle)-dy*Math.Sin(angle)),y=(float)(dx*Math.Sin(angle)+dy*Math.Cos(angle));
            int sx=Math.Max(0,Math.Min(skin.Width-1,(int)((x/Math.Max(1,trayDiameter)+.5f)*skin.Width)));
            int sy=Math.Max(0,Math.Min(skin.Height-1,(int)((y/Math.Max(1,trayDiameter)+.5f)*skin.Height)));
            var color=skin.GetPixel(sx,sy);
            return color.R*.299+color.G*.587+color.B*.114<135 ? Color.White : Color.FromArgb(45,53,65);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { PaintSurface(e.Graphics, Point.Empty); }
        internal void PaintSurface(Graphics graphics, Point offset)
        {
            var state = graphics.Save();
            graphics.TranslateTransform(-offset.X, -offset.Y);
            using (var background = new SolidBrush(signalMode != 0 ? Color.FromArgb(39,43,49) : DockButton.Canvas))
                graphics.FillRectangle(background, ClientRectangle);
            if (signalMode == 0)
            {
                graphics.TranslateTransform(fanCenter.X,fanCenter.Y); graphics.RotateTransform(rotation); graphics.TranslateTransform(-fanCenter.X,-fanCenter.Y);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(skin, new RectangleF(fanCenter.X - trayDiameter / 2f,
                    fanCenter.Y - trayDiameter / 2f, trayDiameter, trayDiameter));
            }
            graphics.Restore(state);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (signalMode != 0)
            {
                var state = g.Save();
                g.ScaleTransform(dpi * .75f, dpi * .75f);
                Overlay.DrawSignal(g, new PointF(Width / dpi / 1.5f, Height / dpi / 1.5f), signalMode == 1, signalLevel, signalPhase);
                g.Restore(state);
                return;
            }
            if (morph == 0)
            using (var dot = new SolidBrush(IconColorAt(fanCenter)))
            {
                for (int i = 0; i < 3; i++)
                {
                    float x = fanCenter.X + Px((i - 1) * 5);
                    float y = fanCenter.Y;
                    g.FillEllipse(dot, x - 1.6f * dpi, y - 1.6f * dpi, 3.2f * dpi, 3.2f * dpi);
                }
            }
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { var value = base.CreateParams; value.ExStyle |= 0x08000000 | 0x80; return value; } }
        protected override void WndProc(ref Message message) { if (message.Msg == 0x02E0) { dpi = (message.WParam.ToInt64() & 0xffff) / 96f; SetExpanded(expanded, true); message.Result = IntPtr.Zero; return; } if (message.Msg == 0x21) { message.Result = (IntPtr)3; return; } base.WndProc(ref message); }
        static Point Clamp(Point position, Rectangle area, Size size) { return new Point(Math.Max(area.Left, Math.Min(area.Right - size.Width, position.X)), Math.Max(area.Top, Math.Min(area.Bottom - size.Height, position.Y))); }
        internal static Point EdgePosition(Point point, Rectangle area, Size size, int margin)
        {
            var inset = Rectangle.Inflate(area, -margin, -margin);
            var result = Clamp(point, inset, size);
            int left = Math.Abs(point.X - inset.Left), right = Math.Abs(inset.Right - point.X - size.Width);
            int top = Math.Abs(point.Y - inset.Top), bottom = Math.Abs(inset.Bottom - point.Y - size.Height);
            int distance = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
            if (distance == left) result.X = inset.Left;
            else if (distance == right) result.X = inset.Right - size.Width;
            else if (distance == top) result.Y = inset.Top;
            else result.Y = inset.Bottom - size.Height;
            return result;
        }
        public void RestorePosition(int x, int y)
        {
            falling.Stop();
            var point = x == Int32.MinValue || y == Int32.MinValue ? new Point(Screen.PrimaryScreen.WorkingArea.Right - 1, Screen.PrimaryScreen.WorkingArea.Bottom - 1) : new Point(x, y);
            var area = Screen.FromPoint(point).WorkingArea;
            restingPosition = EdgePosition(point, area, new Size(Px(idleDiameter), Px(idleDiameter)), Px(3));
            SetExpanded(expanded, true);
        }
        void BeginFall()
        {
            var center=new Point(Left+Width/2,Top+Height/2);
            SetExpanded(false,true);
            fallArea=Screen.FromPoint(center).WorkingArea;
            Location=Clamp(new Point(center.X-Width/2,center.Y-Height/2),fallArea,Size);
            if (unchecked(Environment.TickCount-lastMotion)>100) releaseVelocity=PointF.Empty;
            ball.Reset(new PointF(Left,Top),releaseVelocity);
            physicsTime=motionClock.Elapsed.TotalSeconds;
            if(reducedMotion) FinishFall(); else falling.Start();
        }
        void AdvanceFall()
        {
            double now=motionClock.Elapsed.TotalSeconds;
            float dt=(float)(now-physicsTime); physicsTime=now;
            float before=ball.Position.X;
            ball.Step(fallArea,Size,dt,dpi);
            rotation=(rotation+(ball.Position.X-before)/Math.Max(1,Width/2f)*180f/(float)Math.PI)%360;
            Location=new Point((int)Math.Round(ball.Position.X),(int)Math.Round(ball.Position.Y));
            Invalidate();
            if(ball.Settled) FinishFall();
        }
        void FinishFall()
        {
            falling.Stop();
            var area=Screen.FromControl(this).WorkingArea;
            restingPosition=new Point(Math.Max(area.Left+Px(3),Math.Min(area.Right-Width-Px(3),Left)),area.Bottom-Height);
            SetExpanded(false,true);
            if(PositionSaved!=null) PositionSaved(restingPosition);
        }
        void DragStart(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || signalMode != 0 && sender == this) return; caughtInFlight=falling.Enabled; falling.Stop(); dragging = true; dragMoved = false; releaseVelocity=PointF.Empty; dragTime=motionClock.Elapsed.TotalSeconds; dragStart = Cursor.Position; locationStart = Location; lastMotion = Environment.TickCount;
            ((Control)sender).Capture = true;
        }
        void CaptureChanged(object sender, EventArgs e)
        {
            if (dragging && !((Control)sender).Capture) { dragging = false; if (dragMoved || caughtInFlight) BeginFall(); }
        }
        void DragMove(object sender, MouseEventArgs e)
        {
            if (!dragging) return; var point = Cursor.Position;
            if (Math.Abs(point.X - dragStart.X) >= SystemInformation.DragSize.Width / 2 || Math.Abs(point.Y - dragStart.Y) >= SystemInformation.DragSize.Height / 2) dragMoved = true;
            if (!dragMoved) return;
            AdvanceDrag();
        }
        void AdvanceDrag()
        {
            var cursor=Cursor.Position;
            var next=Clamp(new Point(locationStart.X+cursor.X-dragStart.X,locationStart.Y+cursor.Y-dragStart.Y),Screen.FromPoint(cursor).WorkingArea,Size);
            double now=motionClock.Elapsed.TotalSeconds;
            float dt=(float)(now-dragTime); dragTime=now;
            if (next!=Location)
            {
                lastMotion=Environment.TickCount;
                if(dt>.001f)
                {
                    float vx=Math.Max(-2000*dpi,Math.Min(2000*dpi,(next.X-Left)/dt));
                    float vy=Math.Max(-2000*dpi,Math.Min(2000*dpi,(next.Y-Top)/dt));
                    releaseVelocity=new PointF(releaseVelocity.X*.35f+vx*.65f,releaseVelocity.Y*.35f+vy*.65f);
                }
            }
            Location=next;
        }
        void DragEnd(object sender, MouseEventArgs e)
        {
            if (!dragging) return; dragging = false; ((Control)sender).Capture = false;
            if (dragMoved || caughtInFlight) BeginFall();
            else if (signalMode == 0) SetExpanded(true);
        }
        public void UpdateState(bool writing, bool processing, bool notesRecording, bool noteBusy, bool isMeeting, string message)
        {
            write.Text = processing ? "Cancel transcription" : "Dictate"; note.Text = "Note"; meeting.Text = "Meeting";
            processingDictation = processing; write.Working = processing;
            write.Active = writing; note.Active = notesRecording && !isMeeting; meeting.Active = notesRecording && isMeeting;
            write.Enabled = !noteBusy; note.Enabled = !writing && !processing && (!noteBusy || notesRecording && !isMeeting); meeting.Enabled = !writing && !processing && (!noteBusy || notesRecording && isMeeting);
            int nextSignal = ShowVoiceIndicator ? (writing || notesRecording ? 1 : processing ? 2 : 0) : 0;
            if (nextSignal != signalMode)
            {
                if (falling.Enabled) FinishFall();
                signalMode = nextSignal; signalLevel = signalPhase = 0;
                SetExpanded(expanded, true);
            }
            AccessibleName = signalMode == 1 ? "Recording speech. Click to stop." : signalMode == 2 ? "Transcribing speech. Click to cancel." : "Notewll toolbar";
            UpdateSignalTimer();
            grip.Invalidate(); write.Invalidate(); note.Invalidate(); meeting.Invalidate();
        }
        protected override void Dispose(bool disposing) { if (disposing) { skin.Dispose(); falling.Dispose(); hover.Dispose(); morphAnimation.Dispose(); signalAnimation.Dispose(); Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplaysChanged; } base.Dispose(disposing); }
    }
}
