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
    internal static class MotionSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern int GetGuiResources(IntPtr process, int flags);
        private static object Read(object target, string name) { return target.GetType().GetField(name, Private).GetValue(target); }
        private static void Write(object target, string name, object value) { target.GetType().GetField(name, Private).SetValue(target, value); }
        private static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, Private).Invoke(target, args); }
        private static void Require(bool ok, string text) { if (!ok) throw new Exception(text); }
        private static void Pump(int milliseconds)
        {
            Stopwatch watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(2); }
        }

        [STAThread]
        private static void Main(string[] args)
        {
            try { Run(args); }
            catch (Exception error)
            {
                Console.WriteLine("FAIL: " + error.Message);
                Console.WriteLine(error.StackTrace);
                Environment.ExitCode = 1;
            }
        }

        private static void Run(string[] args)
        {
            NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4));
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (SpriteAssets sprites = new SpriteAssets(Path.Combine(args[0], "assets", "sprites"), 220))
            using (PetMotion motion = new PetMotion())
            {
                foreach (int height in new[] {160, 220, 280, 340})
                {
                    motion.Reset();
                    sprites.Resize(height);
                    foreach (Pose pose in new[] {Pose.Idle, Pose.Blink, Pose.PokeBlock, Pose.PokeFold, Pose.PokeCheek})
                    {
                        Bitmap source = sprites.Get(pose);
                        foreach (double offset in new[] {-0.5, 0.0, 1.5, 5.5})
                        {
                            bool changed;
                            Bitmap frame = motion.Render(source, offset, out changed);
                            Require(frame.PixelFormat == PixelFormat.Format32bppPArgb, "Pixel format changed");
                            for (int y = (int)(height * 0.88); y < height; y++)
                                for (int x = 0; x < frame.Width; x++)
                                    Require(frame.GetPixel(x, y).ToArgb() == source.GetPixel(x, y).ToArgb(), "Shoe pixels moved");
                            Require(frame.GetPixel(0, 0).A == 0 && frame.GetPixel(frame.Width - 1, height - 1).A == 0, "Opaque corner");
                        }
                    }
                }
                motion.StartRecoil(1000);
                double start = motion.Displacement(1050, 220);
                motion.StartRecoil(1050);
                Require(Math.Abs(motion.Displacement(1050, 220) - start) < 0.000001, "Rapid click restarted active pulse");
                Require(Math.Abs(motion.Displacement(1250, 220) - PetMotion.Breath(1250, 220)) < 0.000001, "Recoil did not finish at 250 ms");
                sprites.Resize(340);
                motion.Reset();
                using (Bitmap comparison = new Bitmap(sprites.Width * 3, sprites.Height, PixelFormat.Format32bppArgb))
                using (Graphics graphics = Graphics.FromImage(comparison))
                {
                    graphics.Clear(Color.FromArgb(235, 232, 226));
                    for (int i = 0; i < 3; i++)
                    {
                        bool changed;
                        Bitmap frame = motion.Render(sprites.Get(Pose.Idle), new[] {0.0, 2.0, 5.36}[i], out changed);
                        graphics.DrawImageUnscaled(frame, i * sprites.Width, 0);
                    }
                    comparison.Save(Path.Combine(args[0], "test-artifacts", "screenshots", "motion-step1-comparison.png"));
                }
            }
            Console.WriteLine("PASS: four sizes / five animated poses, fixed shoe pixels, alpha corners, recoil bounds and completion.");

            string config = new SettingsStore().PathOnDisk;
            byte[] saved = File.Exists(config) ? File.ReadAllBytes(config) : null;
            IntPtr foreground = GetForegroundWindow();
            try
            {
                using (PetForm pet = new PetForm(args[0]))
                {
                    pet.Show();
                    try
                    {
                        foreach (int size in new[] {160, 220, 280, 340})
                        {
                            Call(pet, "ChangeSize", size);
                            Point position = pet.Location;
                            Pump(150);
                            Require(pet.Location == position, "Window moved while breathing");
                        }
                        for (int i = 0; i < 4; i++)
                        {
                            Call(pet, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
                            Call(pet, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
                            Require((Pose)Read(pet, "pose") == new[] {Pose.PokeBlock, Pose.PokeFold, Pose.PokeCheek}[i % 3], "Click cycle changed");
                            Pump(270);
                        }
                        Pump(450);
                        Pose restingPose = (Pose)Read(pet, "pose");
                        Require(restingPose == Pose.Idle || restingPose == Pose.Blink, "Click pose did not expire");
                        Call(pet, "SetPose", Pose.Idle);
                        Write(pet, "nextBlink", 0L);
                        Call(pet, "OnTimerTick", null, EventArgs.Empty);
                        Require((Pose)Read(pet, "pose") == Pose.Blink, "Blink did not start");
                        Stopwatch blinkWait = Stopwatch.StartNew();
                        while ((Pose)Read(pet, "pose") == Pose.Blink && blinkWait.ElapsedMilliseconds < 1000) Pump(5);
                        Require((Pose)Read(pet, "pose") == Pose.Idle, "Blink did not finish");
                        Write(pet, "lastInteraction", ((Stopwatch)Read(pet, "clock")).ElapsedMilliseconds - 180010L);
                        Call(pet, "OnTimerTick", null, EventArgs.Empty);
                        Require((Pose)Read(pet, "pose") == Pose.Sleep, "Sleep failed");
                        Call(pet, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
                        Call(pet, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
                        Require((Pose)Read(pet, "pose") == Pose.Wake, "Wake failed");
                        int clicks = (int)Read(pet, "nextClickPose");
                        Write(pet, "pressed", true);
                        Write(pet, "dragging", true);
                        Call(pet, "SetPose", Pose.Drag);
                        Call(pet, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
                        Require((Pose)Read(pet, "pose") == Pose.Idle && (int)Read(pet, "nextClickPose") == clicks, "Drag triggered click");
                        Call(pet, "HideToTray");
                        Require(!pet.Visible, "Hide failed");
                        Call(pet, "RestoreFromTray");
                        Require(pet.Visible, "Restore failed");
                        Require(GetForegroundWindow() == foreground, "Keyboard focus changed");
                        Console.WriteLine("PASS: live layered windows, resizing, click cycle, blink, sleep/wake, drag release, hide/restore, no focus change.");

                        Pump(300);
                        Process process = Process.GetCurrentProcess();
                        int gdi = GetGuiResources(process.Handle, 0);
                        TimeSpan cpu = process.TotalProcessorTime;
                        Point fixedPosition = pet.Location;
                        Stopwatch measured = Stopwatch.StartNew();
                        Pump(8400);
                        double cpuSeconds = (process.TotalProcessorTime - cpu).TotalSeconds;
                        Require(pet.Location == fixedPosition, "Idle window position drifted");
                        int afterGdi = GetGuiResources(process.Handle, 0);
                        Console.WriteLine("GDI: before=" + gdi + ", after=" + afterGdi);
                        Require(afterGdi <= gdi, "GDI handles increased during animation");
                        Console.WriteLine("MEASURE: 340 px, " + measured.ElapsedMilliseconds + " ms idle, CPU " + cpuSeconds.ToString("F3") +
                            " s, GDI stable at " + gdi + ", working set " + (process.WorkingSet64 / 1048576.0).ToString("F1") + " MiB (test process).");
                    }
                    finally { pet.Close(); }
                }
            }
            finally
            {
                if (saved != null) File.WriteAllBytes(config, saved);
                else if (File.Exists(config)) File.Delete(config);
            }
        }
    }
}
