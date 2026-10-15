using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MakiseDesktopPet
{
    internal static class SleepMotionSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Read(object target, string name) { return target.GetType().GetField(name, Private).GetValue(target); }
        private static void Write(object target, string name, object value) { target.GetType().GetField(name, Private).SetValue(target, value); }
        private static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, Private).Invoke(target, args); }
        private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        private static byte[] Pixels(Bitmap image)
        {
            byte[] result = new byte[image.Width * image.Height * 4];
            BitmapData data = image.LockBits(new Rectangle(Point.Empty, image.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try { for (int y=0;y<image.Height;y++) Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),result,y*image.Width*4,image.Width*4); }
            finally { image.UnlockBits(data); }
            return result;
        }
        private static bool Same(byte[] a, byte[] b)
        {
            if(a.Length!=b.Length) return false;
            for(int i=0;i<a.Length;i++) if(a[i]!=b[i]) return false;
            return true;
        }
        private static byte[] Frame(PetForm pet)
        {
            return Pixels((Bitmap)typeof(LayeredForm).GetField("currentFrame",Private).GetValue(pet));
        }
        [STAThread]
        private static void Main(string[] args)
        {
            try { Run(args[0]); }
            catch(Exception error) { Console.WriteLine("FAIL: "+error); Environment.ExitCode=1; }
        }
        private static void Run(string root)
        {
            NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4));
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string output=Path.Combine(root,"test-artifacts","sleep-zzz");
            Directory.CreateDirectory(output);
            using(SpriteAssets sprites=new SpriteAssets(Path.Combine(root,"assets","sprites"),220))
            using(SleepAnimation motion=new SleepAnimation())
            using(PetMotion bodyOnly=new PetMotion())
            using(Bitmap sheet=new Bitmap(1010,410))
            using(Graphics g=Graphics.FromImage(sheet))
            using(Font font=new Font("Segoe UI",13,GraphicsUnit.Pixel))
            {
                g.Clear(Color.FromArgb(239,236,230));
                int column=12;
                foreach(int height in new[]{160,220,280,340})
                {
                    motion.Reset();
                    bodyOnly.Reset();
                    sprites.Resize(height);
                    Bitmap source=sprites.Get(Pose.Sleep);
                    byte[] original=Pixels(source);
                    bool upperMoved=false;
                    for(int t=0;t<=11200;t+=200)
                    {
                        double offset=PetMotion.SleepBreath(t,height);
                        Require(offset>=0&&offset<=1.5,"Sleep breathing exceeded bounds");
                        bool changed;
                        Bitmap rendered=motion.Render(source,t,out changed);
                        byte[] frame=Pixels(rendered);
                        int stride=rendered.Width*4;
                        for(int y=0;y<height;y++) for(int x=0;x<rendered.Width;x++)
                        {
                            int index=y*stride+x*4;
                            // Source generation can leave alpha=1 dust, invisible after scaling.
                            if(x==0||y==0||x==rendered.Width-1||y==height-1)
                                Require(frame[index+3]<=1,"Visible clipping at canvas edge");
                            for(int c=0;c<4;c++)
                            {
                                if(c<3) Require(frame[index+c]<=frame[index+3],"Invalid premultiplied alpha");
                                if(y>=(int)(height*0.88)) Require(frame[index+c]==original[index+c],"Sleep shoes moved");
                                else if(frame[index+c]!=original[index+c]) upperMoved=true;
                            }
                        }
                        if(t%5600==0) Require(Same(frame,original),"Sleep cycle did not return to its source frame");
                    }
                    Require(upperMoved,"Sleep animation stayed static");
                    bool updated;
                    byte[] active=Pixels(motion.Render(source,2400,out updated));
                    byte[] plain=Pixels(bodyOnly.Render(source,PetMotion.SleepBreath(2400,height),out updated));
                    Require(!Same(active,plain),"Independent Z symbols missing");
                    byte[] faded=Pixels(motion.Render(source,5200,out updated));
                    plain=Pixels(bodyOnly.Render(source,PetMotion.SleepBreath(5200,height),out updated));
                    Require(Same(faded,plain),"Z symbols did not fully fade");
                    motion.Render(source,2800,out updated);
                    motion.Render(source,2800,out updated);
                    Require(!updated,"Identical breathing phase bypassed render cache");
                    Bitmap display=motion.Render(source,2400,out updated);
                    g.DrawImage(display,new Rectangle(column,370-height,source.Width,height),0,0,source.Width,height,GraphicsUnit.Pixel);
                    g.DrawString(height+" px",font,Brushes.Black,column,380);
                    column+=source.Width+35;
                    Console.WriteLine("PASS: "+height+" px, two 5.6 s sleep cycles, stable feet, alpha, animated/fading Z symbols and cache.");
                }
                sheet.Save(Path.Combine(output,"four-sizes.png"));
            }
            // Exercise the production state/render paths without showing windows or capturing input.
            using(PetForm pet=new PetForm(root))
            {
                ((NotifyIcon)Read(pet,"tray")).Visible=false;
                SettingsStore store=(SettingsStore)Read(pet,"settingsStore");
                Write(store,"PathOnDisk",Path.Combine(output,"isolated-settings.json"));
                try
                {
                    Write(pet,"lastInteraction",-180001L);
                    Call(pet,"OnTimerTick",null,EventArgs.Empty);
                    Require((bool)Read(pet,"sleeping")&&(Pose)Read(pet,"pose")==Pose.Sleep,"Sleep timer did not enter sleeping pose");
                    long start=(long)Read(pet,"sleepStarted");
                    Call(pet,"RenderMotion",start,true);
                    byte[] relaxed=Frame(pet);
                    Call(pet,"RenderMotion",start+2800,true);
                    Require(!Same(Frame(pet),relaxed),"PetForm did not animate sleeping pose");
                    Call(pet,"RenderMotion",start+5600,true);
                    Require(Same(Frame(pet),relaxed),"PetForm sleep animation loop drifted");
                    foreach(int size in new[]{160,220,280,340})
                    {
                        Call(pet,"ChangeSize",size);
                        Call(pet,"RenderMotion",start+2800,true);
                        Require(pet.Height==size&&(Pose)Read(pet,"pose")==Pose.Sleep,"Resizing lost sleep state");
                    }
                    Write(pet,"sleeping",false);
                    Call(pet,"SetPose",Pose.Wake);
                    SpriteAssets assets=(SpriteAssets)Read(pet,"sprites");
                    Require(Same(Frame(pet),Pixels(assets.Get(Pose.Wake))),"Sleep frame/ZZZ remained on wake");
                    Write(pet,"poseUntil",((Stopwatch)Read(pet,"clock")).ElapsedMilliseconds-1);
                    Call(pet,"OnTimerTick",null,EventArgs.Empty);
                    Require((Pose)Read(pet,"pose")==Pose.Idle||(Pose)Read(pet,"pose")==Pose.Blink,"Wake did not restore idle");
                    for(int i=0;i<6;i++)
                    {
                        ((System.Collections.Generic.Queue<long>)Read(pet,"recentClicks")).Clear();
                        Write(pet,"pressed",true);
                        Call(pet,"OnMouseUp",new MouseEventArgs(MouseButtons.Left,1,0,0,0));
                        Require((Pose)Read(pet,"pose")==new[]{Pose.PokeBlock,Pose.PokeFold,Pose.PokeCheek}[i%3],"Click cycle regressed");
                    }
                    Call(pet,"SetPose",Pose.Drag);
                    Require(Same(Frame(pet),Pixels(assets.GetDrag(false))),"Sleep frame remained on drag");
                    Require(!pet.Visible&&!((BubbleForm)Read(pet,"bubble")).Visible,"Background test showed a window");
                    Console.WriteLine("PASS: sleep entry/render loop, four sleep sizes, wake/idle/click/drag transitions; no shown windows.");
                }
                finally { Call(pet,"OnFormClosed",new FormClosedEventArgs(CloseReason.ApplicationExitCall)); }
            }
        }
    }
}
