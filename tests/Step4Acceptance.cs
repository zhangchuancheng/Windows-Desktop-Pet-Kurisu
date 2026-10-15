using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MakiseDesktopPet
{
    // Background acceptance: no Show, mouse capture, input injection or screen capture.
    internal static class Step4Acceptance
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        [DllImport("user32.dll")] private static extern int GetGuiResources(IntPtr process, int flags);
        private static PetForm pet;
        private static Stopwatch timeline;
        private static string output;
        private static StreamWriter manifest;
        private static int frame;
        private static long nextCapture;
        private static string stage;
        private static object Read(object target, string field) { return target.GetType().GetField(field, Flags).GetValue(target); }
        private static void Write(object target, string field, object value) { target.GetType().GetField(field, Flags).SetValue(target, value); }
        private static object Call(object target, string method, params object[] args) { return target.GetType().GetMethod(method, Flags).Invoke(target, args); }
        private static void Check(bool ok, string description) { if (!ok) throw new Exception(description); Console.WriteLine("PASS: " + description); }
        private static Bitmap Frame(LayeredForm form) { return (Bitmap)typeof(LayeredForm).GetField("currentFrame", Flags).GetValue(form); }
        private static long Now { get { return ((Stopwatch)Read(pet, "clock")).ElapsedMilliseconds; } }
        private static void Click()
        {
            // Exercise the real release handler without acquiring the user's mouse.
            Write(pet, "pressed", true);
            Call(pet, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
        }
        private static void Pump(int duration, int captureInterval)
        {
            long end = timeline.ElapsedMilliseconds + duration;
            while (timeline.ElapsedMilliseconds < end)
            {
                Application.DoEvents();
                Call(pet, "RenderMotion", Now, false);
                if (captureInterval > 0 && timeline.ElapsedMilliseconds >= nextCapture)
                {
                    Capture();
                    nextCapture = timeline.ElapsedMilliseconds + captureInterval;
                }
                Thread.Sleep(2);
            }
        }
        private static void Capture()
        {
            using (Bitmap canvas = new Bitmap(650, 440, PixelFormat.Format32bppRgb))
            using (Graphics g = Graphics.FromImage(canvas))
            using (Font font = new Font("Microsoft YaHei UI", 12, GraphicsUnit.Pixel))
            using (SolidBrush ink = new SolidBrush(Color.FromArgb(65, 62, 60)))
            {
                g.Clear(Color.FromArgb(239, 236, 230));
                using (SolidBrush tile = new SolidBrush(Color.FromArgb(225, 223, 218)))
                    for (int y=48;y<410;y+=16) for(int x=0;x<650;x+=16)
                        if ((x/16+y/16)%2==0) g.FillRectangle(tile,x,y,16,16);
                g.DrawString("程序渲染帧 · 后台事件测试（非真人鼠标录屏）",font,ink,12,8);
                g.DrawString(stage + "   " + (timeline.ElapsedMilliseconds/1000.0).ToString("F1") + " 秒",font,ink,12,27);
                Bitmap character = Frame(pet);
                int xPet = 100, yPet = 398-character.Height;
                if ((Pose)Read(pet,"pose") == Pose.Drag) xPet += (bool)Read(pet,"dragMovingLeft") ? -24 : 24;
                g.DrawImage(character,new Rectangle(xPet,yPet,character.Width,character.Height),0,0,character.Width,character.Height,GraphicsUnit.Pixel);
                BubbleForm bubble = (BubbleForm)Read(pet,"bubble");
                if ((long)Read(pet,"bubbleUntil")>Now && Frame(bubble)!=null)
                {
                    Bitmap speech=Frame(bubble);
                    g.DrawImage(speech,new Rectangle(340,80,speech.Width,speech.Height),0,0,speech.Width,speech.Height,GraphicsUnit.Pixel);
                }
                g.DrawString("实际渲染尺寸："+pet.Height+" px；中间等待段每秒采样，其余约 20 fps",font,ink,12,417);
                string name = frame.ToString("D5")+".png";
                canvas.Save(Path.Combine(output,"frames",name),ImageFormat.Png);
                manifest.WriteLine(name+","+timeline.ElapsedMilliseconds+","+stage+","+Read(pet,"pose"));
                manifest.Flush();
                frame++;
            }
        }
        private static void Place(int x,int y)
        {
            Write(pet,"baseX",x); Write(pet,"baseY",y);
            Call(pet,"OnDisplaySettingsChanged",null,EventArgs.Empty);
            // The hidden form has no handle; explicitly exercise the same clamp method.
            Call(pet,"ClampToVisibleScreen");
            pet.Location=new Point((int)Read(pet,"baseX"),(int)Read(pet,"baseY"));
        }
        [STAThread]
        private static void Main(string[] args)
        {
            try { Run(args[0]); }
            catch(Exception error) { Console.WriteLine("FAIL: "+error); Environment.ExitCode=1; }
            finally
            {
                if(manifest!=null) manifest.Dispose();
                if(pet!=null)
                {
                    Call(pet,"OnFormClosed",new FormClosedEventArgs(CloseReason.ApplicationExitCall));
                    pet.Dispose();
                }
            }
        }
        private static void Run(string root)
        {
            NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4));
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            output=Path.Combine(root,"test-artifacts","step4");
            Directory.CreateDirectory(Path.Combine(output,"frames"));
            pet=new PetForm(root);
            ((NotifyIcon)Read(pet,"tray")).Visible=false;
            SettingsStore store=(SettingsStore)Read(pet,"settingsStore");
            Write(store,"PathOnDisk",Path.Combine(output,"isolated-settings.json"));
            Check(!pet.Visible,"background form never shown; test settings isolated");
            timeline=Stopwatch.StartNew();
            manifest=new StreamWriter(Path.Combine(output,"frames.csv"),false,System.Text.Encoding.UTF8);
            manifest.WriteLine("file,time_ms,stage,pose");
            using(Bitmap sheet=new Bitmap(1010,410))
            using(Graphics g=Graphics.FromImage(sheet))
            using(Font font=new Font("Segoe UI",13,GraphicsUnit.Pixel))
            {
                g.Clear(Color.FromArgb(239,236,230));
                int x=12;
                foreach(int size in new[]{160,220,280,340})
                {
                    Call(pet,"ChangeSize",size);
                    Call(pet,"RenderMotion",Now,true);
                    Bitmap current=Frame(pet);
                    g.DrawImage(current,new Rectangle(x,370-size,current.Width,size),0,0,current.Width,size,GraphicsUnit.Pixel);
                    g.DrawString(size+" px",font,Brushes.Black,x,380);
                    x+=current.Width+35;
                    Rectangle work=Screen.PrimaryScreen.WorkingArea;
                    foreach(Point corner in new[]{new Point(work.Left,work.Top),new Point(work.Right,work.Top),new Point(work.Left,work.Bottom),new Point(work.Right,work.Bottom),new Point(5000,5000)})
                    {
                        Place(corner.X,corner.Y);
                        Call(pet,"ShowMessage","click");
                        BubbleForm bubble=(BubbleForm)Read(pet,"bubble");
                        Check(work.Contains(pet.Bounds)&&work.Contains(bubble.Bounds),size+" px pet and bubble in work area at "+corner);
                    }
                    Call(pet,"SaveSettings");
                    PetSettings loaded=store.Load();
                    Check(loaded.Height==size&&loaded.X==pet.Left&&loaded.Y==pet.Top,"size/position save and reload "+size);
                }
                sheet.Save(Path.Combine(output,"four-sizes.png"));
            }
            foreach(bool top in new[]{false,true})
            {
                Call(pet,"ChangeLayer",top);
                Check(store.Load().TopMost==top,"layer preference saves "+top+" (hidden window; OS z-order not tested)");
            }
            Dialogues lines=(Dialogues)Read(pet,"dialogues");
            foreach(string category in new[]{"click","rapid","wake","drag"})
            {
                string previous=null;
                for(int i=0;i<100;i++) { string next=lines.Pick(category); if(next==previous) throw new Exception("repeated dialogue"); previous=next; }
            }
            Check(true,"400 dialogue selections without consecutive duplicate");
            Call(pet,"ChangeSize",220);
            Place(100,180);
            Write(pet,"bubbleUntil",0L);
            stage="待机与自然眨眼"; nextCapture=0; Pump(7500,50);
            for(int i=0;i<3;i++)
            {
                stage="点击表情 "+(i+1); Click();
                Check((Pose)Read(pet,"pose")==new[]{Pose.Poke,Pose.PokeSkeptical,Pose.PokeWink}[i],"click expression "+(i+1));
                Pump(1400,50);
            }
            int clickIndex=(int)Read(pet,"nextClickPose");
            Write(pet,"pressed",true); Write(pet,"dragging",true); Write(pet,"dragDirectionAnchorX",100);
            Write(pet,"bubbleUntil",0L); Call(pet,"SetPose",Pose.Drag);
            int[] positions={90,70,72,74,80,80,77,76};
            bool[] expected={true,true,true,false,false,false,false,true};
            for(int i=0;i<positions.Length;i++)
            {
                Call(pet,"UpdateDragDirection",positions[i]);
                Check((bool)Read(pet,"dragMovingLeft")==expected[i],"drag direction / reversal / jitter "+i);
            }
            stage="向左拖动姿态"; Pump(1000,50);
            Call(pet,"UpdateDragDirection",110); stage="向右拖动姿态"; Pump(1000,50);
            stage="松开恢复待机"; Call(pet,"OnMouseUp",new MouseEventArgs(MouseButtons.Left,1,0,0,0));
            Check((int)Read(pet,"nextClickPose")==clickIndex && (Pose)Read(pet,"pose")==Pose.Idle,"drag release does not advance click cycle");
            long waitStart=(long)Read(pet,"lastInteraction");
            stage="真实等待入睡";
            Process self=Process.GetCurrentProcess();
            int gdiStart=GetGuiResources(self.Handle,0); TimeSpan cpuStart=self.TotalProcessorTime;
            long measuredStart=timeline.ElapsedMilliseconds;
            int blinkCount=0; bool wasBlink=false; long lastReport=0;
            while(!(bool)Read(pet,"sleeping") && Now-waitStart<183000)
            {
                long elapsed=Now-waitStart;
                bool blink=(Pose)Read(pet,"pose")==Pose.Blink;
                if(blink&&!wasBlink) blinkCount++; wasBlink=blink;
                if(elapsed<179900 && (bool)Read(pet,"sleeping")) throw new Exception("slept early");
                Pump(20,elapsed<5000||elapsed>177000 ? 50 : 1000);
                if(elapsed-lastReport>=30000) { Console.WriteLine("WAIT: "+elapsed+" ms, natural blinks="+blinkCount); lastReport=elapsed; }
            }
            long delay=Now-waitStart;
            Check((bool)Read(pet,"sleeping")&&delay>=180000&&delay<183000,"natural sleep after "+delay+" ms; blink count="+blinkCount);
            self.Refresh();
            Console.WriteLine("MEASURE: hidden renderer + PNG capture, wall_ms="+(timeline.ElapsedMilliseconds-measuredStart)+", cpu_s="+(self.TotalProcessorTime-cpuStart).TotalSeconds.ToString("F3")+", gdi="+gdiStart+"->"+GetGuiResources(self.Handle,0)+", working_MiB="+(self.WorkingSet64/1048576.0).ToString("F1"));
            stage="三分钟后入睡"; nextCapture=0; Pump(1600,50);
            // Wake state entry equivalent to mouse-down, without Capture=true.
            Call(pet,"Interact"); Write(pet,"sleeping",false); Call(pet,"SetPose",Pose.Wake);
            Write(pet,"poseUntil",Now+850); Call(pet,"ScheduleBlink",Now);
            stage="唤醒与恢复"; Click();
            Check((Pose)Read(pet,"pose")==Pose.Wake && (int)Read(pet,"nextClickPose")==clickIndex,"wake release uses wake response, does not advance clicks");
            Pump(5700,50);
            Check((long)Read(pet,"bubbleUntil")==0,"bubble expires after five seconds");
            Check((Pose)Read(pet,"pose")==Pose.Idle || (Pose)Read(pet,"pose")==Pose.Blink,"wake returns to layered idle");
            long previousCooldown=(long)Read(pet,"rapidCooldownUntil");
            for(int i=0;i<4;i++) { Click(); Pump(80,0); }
            long cooldown=(long)Read(pet,"rapidCooldownUntil");
            Check(cooldown>previousCooldown,"rapid click response starts cooldown");
            for(int i=0;i<4;i++) { Click(); Pump(80,0); }
            Check((long)Read(pet,"rapidCooldownUntil")==cooldown,"cooldown suppresses repeated special response");
            Check(!pet.Visible&&!((BubbleForm)Read(pet,"bubble")).Visible,"test produced no visible pet or speech windows");
            Console.WriteLine("PASS: background acceptance completed, frames="+frame);
        }
    }
}
