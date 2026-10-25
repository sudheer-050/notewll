using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows.Forms;
namespace Notewll
{
    static class BallVerification
    {
        static BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        static object Field(object target,string name) { return target.GetType().GetField(name,Flags).GetValue(target); }
        static void Require(bool value,string message) { if(!value) throw new Exception(message); }
        static Bitmap DockImage(FloatingDock dock)
        {
            var result=new Bitmap(dock.Width,dock.Height); dock.DrawToBitmap(result,new Rectangle(Point.Empty,dock.Size));
            for(int y=0;y<result.Height;y++) for(int x=0;x<result.Width;x++) if(!dock.Region.IsVisible(x,y)) result.SetPixel(x,y,Color.Transparent);
            return result;
        }
        static void Snapshot(Form form,string path)
        {
            Application.DoEvents(); form.Refresh();
            using(var image=new Bitmap(form.Width,form.Height))
            {
                form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));
                // DrawToBitmap omits native owner-drawn combo content; exercise its real draw handler for the capture.
                using(var graphics=Graphics.FromImage(image))
                foreach(var name in new[]{"ballAppearance","ballGravity","ballColorMode"})
                {
                    var field=form.GetType().GetField(name,Flags); if(field==null) continue;
                    var choice=(ComboBox)field.GetValue(form); var screen=choice.PointToScreen(Point.Empty); var point=new Point(screen.X-form.Left,screen.Y-form.Top);
                    var bounds=new Rectangle(point.X+3,point.Y+1,choice.Width-24,choice.Height-2);
                    var parentScreen=choice.Parent.PointToScreen(Point.Empty);
                    var state=graphics.Save();
                    graphics.SetClip(new Rectangle(parentScreen.X-form.Left,parentScreen.Y-form.Top,choice.Parent.ClientSize.Width,choice.Parent.ClientSize.Height));
                    var args=new DrawItemEventArgs(graphics,choice.Font,bounds,choice.SelectedIndex,DrawItemState.ComboBoxEdit);
                    typeof(ComboBox).GetMethod("OnDrawItem",Flags).Invoke(choice,new object[]{args}); graphics.Restore(state);
                }
                image.Save(path);
            }
        }
        static float RedCenterY(Bitmap image)
        {
            float sum=0; int count=0;
            for(int y=0;y<image.Height;y++) for(int x=0;x<image.Width;x++) {
                var c=image.GetPixel(x,y); if(c.R>150 && c.R>c.B*1.5 && c.R>c.G*1.5) { sum+=y; count++; }
            }
            Require(count>0,"Image marker is missing."); return sum/count;
        }
        internal static void Run(string root)
        {
            Directory.CreateDirectory(root); Config.DiagnosticDataRoot(Path.Combine(root,"data"));
            Application.EnableVisualStyles();
            var json=new JavaScriptSerializer();
            var legacy=json.Deserialize<Config>("{}"); legacy.Validate();
            Require(legacy.BallAppearance=="Moon" && legacy.BallGravity=="Earth" && legacy.BallColorMode=="Original colors","Old settings did not retain defaults.");
            float moonDrop=0,earthDrop=0,jupiterDrop=0;
            foreach(var gravity in BallCatalog.Gravities)
            {
                foreach(var appearance in BallCatalog.Appearances)
                {
                    var profile=BallCatalog.Profile(appearance);
                    Require(profile.Diameter>=22 && profile.Diameter<=32,"Ball is not compact.");
                    var body=new BallPhysics(); body.Configure(appearance,gravity); body.Reset(new PointF(200,20),PointF.Empty);
                    var area=new Rectangle(0,0,1000,800); var size=new Size(profile.Diameter,profile.Diameter);
                    for(int frame=0;frame<5000 && !body.Settled;frame++) body.Step(area,size,1f/120,1);
                    Require(body.Settled && body.Position.Y==area.Bottom-size.Height,"Ball never settles: "+appearance+"/"+gravity);
                }
                var drop=new BallPhysics(); drop.Configure("Cricket ball",gravity); drop.Reset(new PointF(200,20),PointF.Empty);
                for(int i=0;i<30;i++) drop.Step(new Rectangle(0,0,1000,5000),new Size(28,28),1f/120,1);
                if(gravity=="Moon") moonDrop=drop.Position.Y;
                if(gravity=="Earth") earthDrop=drop.Position.Y;
                if(gravity=="Jupiter") jupiterDrop=drop.Position.Y;
            }
            Require(moonDrop<earthDrop && earthDrop<jupiterDrop,"Selected gravity does not change acceleration.");
            var cricket=BallCatalog.Profile("Cricket ball");
            foreach(var planet in BallCatalog.Gravities) {
                var profile=BallCatalog.Profile(planet);
                Require(profile.Mass==cricket.Mass && profile.Bounce==cricket.Bounce && profile.Diameter==cricket.Diameter,"Planet skin changed object physics.");
            }
            using(var dock=new FloatingDock())
            using(var atlas=new Bitmap(880,250*((BallCatalog.Appearances.Length+3)/4)))
            using(var g=Graphics.FromImage(atlas))
            using(var font=new Font("Segoe UI",10))
            {
                ((Timer)Field(dock,"hover")).Stop(); dock.RestorePosition(600,0); dock.Show(); dock.Configure(true);
                g.Clear(Color.FromArgb(31,33,38)); int index=0;
                foreach(var appearance in BallCatalog.Appearances)
                {
                    dock.ConfigureBall(appearance,"Earth"); dock.SetExpanded(false,true);
                    int x=index%4*220,y=index/4*250;
                    g.DrawString(appearance,font,Brushes.White,new RectangleF(x+8,y+8,205,36));
                    using(var image=DockImage(dock)) g.DrawImageUnscaled(image,x+85,y+50);
                    float scale; using(var graphics=dock.CreateGraphics()) scale=graphics.DpiX/96;
                    Require(dock.Width<=Math.Ceiling(32*scale),"Collapsed skin is too large.");
                    dock.SetExpanded(true,true);
                    Require(Math.Max(dock.Width,dock.Height)<=Math.Ceiling(BallCatalog.ExpandedDiameter*scale),"Expanded skin is too large.");
                    using(var image=DockImage(dock)) g.DrawImageUnscaled(image,x+10,y+125);
                    index++;
                }
                atlas.Save(Path.Combine(root,"appearances.png"));
                using(var hub=new SettingsForm(new Config(),"","",new NoteStore(Path.Combine(root,"notes"))))
                {
                    hub.Show(); hub.ShowPage("Preferences"); hub.SelectSettingsCategory("Ball"); Application.DoEvents();
                    var appearance=(ComboBox)Field(hub,"ballAppearance"); var gravity=(ComboBox)Field(hub,"ballGravity"); 
                    Require(appearance.Items.Count==19 && gravity.Items.Count==9,"Missing choices.");
                    var sizeSlider=(TrackBar)Field(hub,"ballSize"); Require(sizeSlider.Value==100,"Current size is not the default.");
                    sizeSlider.Value=sizeSlider.Maximum;
                    Require(((BallPreview)Field(hub,"ballPreview")).Height>116,"Maximum-size preview did not grow to fit the ball.");
                    sizeSlider.Value=100;

                    appearance.SelectedItem="Basketball";
                    Require(!((Control)Field(hub,"ballFirst")).Visible && !((Control)Field(hub,"ballSecond")).Visible,"Sports skins expose custom colors.");
                    Snapshot(hub,Path.Combine(root,"preferences-sport.png"));
                    appearance.SelectedItem="Earth";
                    Require(!((Control)Field(hub,"ballFirst")).Visible,"Planet skins expose custom colors.");
                    appearance.SelectedItem="Color";
                    Require(((Control)Field(hub,"ballFirst")).Visible && !((Control)Field(hub,"ballSecond")).Visible && !((Control)Field(hub,"ballAngle")).Visible,"Color appearance has incorrect controls.");
                    Snapshot(hub,Path.Combine(root,"preferences-color.png"));
                    appearance.SelectedItem="Gradient";
                    Require(((Control)Field(hub,"ballFirst")).Visible && ((Control)Field(hub,"ballSecond")).Visible && ((Control)Field(hub,"ballAngle")).Visible,"Gradient controls are missing.");
                    var livePreview=(BallPreview)Field(hub,"ballPreview");
                    foreach(var environment in BallCatalog.Gravities) {
                        gravity.SelectedItem=environment;
                        Require(((Timer)Field(livePreview,"timer")).Enabled,"Selecting gravity does not show a bounce sample.");
                    }
                    gravity.SelectedItem="Moon";
                    hub.GetType().GetField("firstColor",Flags).SetValue(hub,"#E64573"); hub.GetType().GetField("secondColor",Flags).SetValue(hub,"#4050D9");
                    ((NumericUpDown)Field(hub,"ballAngle")).Value=120;
                    hub.GetType().GetMethod("RefreshBallDraft",Flags).Invoke(hub,null);
                    int saves=0;
                    hub.OnSaved=next=> { next.Save(); dock.ConfigureBall(next); saves++; };
                    sizeSlider.Value=150;
                    var expandedSlider=(TrackBar)Field(hub,"ballExpandedSize"); Require(expandedSlider.Value==100,"Current expanded size is not the default.");
                    expandedSlider.Value=150;
                    hub.SavePreferences(); Require(saves==1,"Settings did not apply.");
                    var saved=Config.Load(); Require(saved.BallAppearance=="Gradient" && saved.BallGravity=="Moon" && saved.BallColorMode=="Gradient" && saved.BallColor1=="#E64573" && saved.BallGradientAngle==120 && saved.BallSizePercent==150 && saved.BallExpandedSizePercent==150,"Selection did not survive save/load.");
                    Require(dock.Appearance=="Gradient" && dock.Gravity=="Moon","Saved preferences did not reach the dock.");
                    Require(((BallPhysics)Field(dock,"ball")).GravityMultiplier==BallCatalog.Gravity("Moon"),"Saved gravity did not reach physics.");
                    dock.SetExpanded(false,true); int closedWidth=dock.Width;
                    dock.SetExpanded(true,true);
                    using(var graphics=dock.CreateGraphics()) {
                        float dpiScale=graphics.DpiX/96f;
                        Require(Math.Abs(Math.Max(dock.Width,dock.Height)-BallCatalog.ExpandedDiameter*1.5f*dpiScale)<=1,"Expanded size did not reach the dock.");
                        foreach(var icon in dock.Controls.OfType<DockButton>()) Require(Math.Abs(icon.Width-BallCatalog.TileDiameter*1.5f*dpiScale)<=2,"Icons did not scale with expanded size.");
                    }
                    dock.SetExpanded(false,true); Require(dock.Width==closedWidth,"Expanded size changed the collapsed size.");
                    foreach(var percent in new[]{100,150,200}) {
                        var sizing=saved.Copy(); sizing.BallExpandedSizePercent=percent;
                        dock.ConfigureBall(sizing);
                        var area=Screen.FromControl(dock).WorkingArea;
                        foreach(var point in new[]{new Point(area.Left,area.Top+area.Height/2),new Point(area.Right-1,area.Top+area.Height/2),new Point(area.Left+area.Width/2,area.Top),new Point(area.Left+area.Width/2,area.Bottom-1)}) {
                            dock.RestorePosition(point.X,point.Y); dock.SetExpanded(true,true);
                            Require(area.Contains(dock.Bounds),"Expanded size leaves the screen.");
                        }
                    }
                    dock.ConfigureBall(saved); dock.SetExpanded(false,true);

                    Snapshot(hub,Path.Combine(root,"preferences-ball-top.png"));
                    var panel=(Panel)appearance.Parent; panel.ScrollControlIntoView((Control)Field(hub,"ballPreview"));
                    Snapshot(hub,Path.Combine(root,"preferences-ball-preview.png"));
                    var chosen=saved.Copy(); chosen.BallColor1="#000000"; chosen.BallAppearance="Color"; chosen.BallColorMode="Solid color"; dock.ConfigureBall(chosen);
                    Require(dock.IconColorAt(new PointF(dock.Width/2f,dock.Height/2f))==Color.White,"Dark picked colors make the controls unreadable.");
                    dock.ConfigureBall(saved);
                    using(var finish=BallCatalog.Texture(saved)) finish.Save(Path.Combine(root,"custom-gradient.png"));
                    var solid=saved.Copy(); solid.BallAppearance="Color"; solid.BallColorMode="Solid color"; solid.BallColor1="#167CDF";
                    using(var finish=BallCatalog.Texture(solid)) { var pixel=finish.GetPixel(80,80); Require(pixel.B>pixel.R,"Solid color finish did not use the picked color."); }
                    var invalid=saved.Copy(); invalid.BallColor1="invalid";
                    bool rejected=false; try { invalid.Validate(); } catch(InvalidDataException) { rejected=true; }
                    Require(rejected,"Invalid saved color was accepted.");
                    using(var picker=new BallColorPicker(saved,false))
                    {
                        picker.Show(hub); Application.DoEvents();
                        var range=(BallColorRange)Field(picker,"range"); var pickerPreview=(BallPreview)Field(picker,"preview");
                        var before=((Bitmap)Field(pickerPreview,"skin")).GetPixel(90,90);
                        range.SelectColor(Color.FromArgb(53,181,226));
                        var watch=System.Diagnostics.Stopwatch.StartNew();
                        while(watch.ElapsedMilliseconds<180) { Application.DoEvents(); System.Threading.Thread.Sleep(8); }
                        var after=((Bitmap)Field(pickerPreview,"skin")).GetPixel(90,90);
                        Require(before.ToArgb()!=after.ToArgb() && picker.SelectedHex=="#35B5E2","Color range does not update its live preview.");
                        Require(((Control)pickerPreview).Controls.OfType<DockButton>().Count()==3,"Expanded tray is missing from the picker preview.");
                        foreach(Control control in picker.Controls) control.PerformLayout();
                        Snapshot(picker,Path.Combine(root,"live-color-picker.png")); picker.Close();
                        Require((string)Field(hub,"firstColor")=="#E64573","Unconfirmed picker changes altered preferences.");
                    }

                    string localImage=Path.Combine(root,"local-image.png");
                    using(var image=new Bitmap(160,160)) using(var paint=Graphics.FromImage(image)) {
                        paint.Clear(Color.Navy); paint.FillRectangle(Brushes.Red,100,10,40,50); image.Save(localImage);
                    }
                    byte[] originalBytes=File.ReadAllBytes(localImage);
                    string imported=BallImages.Import(localImage);
                    Require(imported!=localImage && File.Exists(imported) && File.ReadAllBytes(localImage).SequenceEqual(originalBytes),"Local image import changed the original.");
                    hub.GetType().GetField("selectedBallImage",Flags).SetValue(hub,imported);
                    hub.GetType().GetMethod("RefreshBallDraft",Flags).Invoke(hub,null);
                    hub.SavePreferences(); saved=Config.Load();
                    Require(saved.BallImagePath==imported && saved.BallSizePercent==150,"Image or size was not saved.");
                    dock.SetExpanded(false,true);
                    using(var graphics=dock.CreateGraphics()) Require(Math.Abs(dock.Width-BallCatalog.Diameter(saved)*graphics.DpiX/96f)<=1,"Saved size did not reach the dock.");
                    var imageConfig=saved.Copy(); imageConfig.BallAppearance="Color"; imageConfig.BallColor1="#FFFFFF"; imageConfig.BallSizePercent=100;
                    dock.ConfigureBall(imageConfig); dock.SetExpanded(false,true);
                    using(var frame=DockImage(dock)) frame.Save(Path.Combine(root,"custom-image-ball.png"));
                    var rotation=dock.GetType().GetField("rotation",Flags);
                    float redY0,redY90;
                    using(var frame=DockImage(dock)) redY0=RedCenterY(frame);
                    rotation.SetValue(dock,90f);
                    using(var frame=DockImage(dock)) { redY90=RedCenterY(frame); frame.Save(Path.Combine(root,"custom-image-rotated.png")); }
                    Require(redY0<dock.Height/2 && redY90>dock.Height/2,"Custom image did not rotate with the ball.");
                    foreach(var look in BallCatalog.Appearances) {
                        var bounded=new Config { BallAppearance=look,BallSizePercent=BallCatalog.MinSizePercent(look) }; bounded.Validate();
                        Require(BallCatalog.Diameter(bounded)>=18 && BallCatalog.Diameter(bounded)<=20,"Minimum size is outside the approximate half-centimeter range.");
                        bounded.BallSizePercent=BallCatalog.MaxSizePercent(look); bounded.Validate();
                        Require(BallCatalog.Diameter(bounded)>=93 && BallCatalog.Diameter(bounded)<=95,"Maximum size is outside the approximate 2.5-centimeter range.");
                    }
                    ((HubButton)Field(hub,"removeBallImage")).PerformClick();
                    Require((string)Field(hub,"selectedBallImage")=="" && File.Exists(localImage),"Removing image altered the source file.");
                    hub.GetType().GetField("selectedBallImage",Flags).SetValue(hub,imported); hub.GetType().GetMethod("RefreshBallDraft",Flags).Invoke(hub,null);
                    Snapshot(hub,Path.Combine(root,"preferences-size-image.png"));
                    var preview=(BallPreview)Field(hub,"ballPreview"); preview.Drop(); Require(((Timer)Field(preview,"timer")).Enabled,"Drop preview did not start.");
                    var timer=(Timer)Field(preview,"timer"); timer.Stop();
                    hub.Hide();
                    using(var reopened=new SettingsForm(saved,"","",new NoteStore(Path.Combine(root,"notes")))) {
                        Require(((ComboBox)Field(reopened,"ballAppearance")).Text=="Gradient","Reopened settings lost appearance.");
                        Require(((TrackBar)Field(reopened,"ballSize")).Value==150 && ((TrackBar)Field(reopened,"ballExpandedSize")).Value==150 && (string)Field(reopened,"selectedBallImage")==imported,"Reopened settings lost size or image.");
                    }
                }
            }
            Console.Write("ball-preferences-ok|19-appearances|9-gravities|171-physics-combinations|compact|expanded-skins|legacy-config|save-reopen|live-apply|plain-color|plain-gradient|conditional-color-controls|live-color-picker|picker-cancel|automatic-gravity-bounce|drop-preview|percent-size-range|independent-expanded-size|scaled-icons|four-edges|local-image-copy|image-save-reopen|image-removal|image-rotation");
        }
    }
}

