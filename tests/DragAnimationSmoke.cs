using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace MakiseDesktopPet
{
    internal static class DragAnimationSmoke
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Read(object target, string name) { return target.GetType().GetField(name, Flags).GetValue(target); }
        private static void Write(object target, string name, object value) { target.GetType().GetField(name, Flags).SetValue(target, value); }
        private static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, Flags).Invoke(target, args); }
        private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        private static Bitmap Frame(LayeredForm form) { return (Bitmap)typeof(LayeredForm).GetField("currentFrame", Flags).GetValue(form); }
        private static string Hash(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            using (SHA256 hash = SHA256.Create())
            { bitmap.Save(memory, ImageFormat.Png); return Convert.ToBase64String(hash.ComputeHash(memory.ToArray())); }
        }

        [STAThread]
        private static void Main(string[] args)
        {
            try { Run(args[0]); }
            catch (Exception error) { Console.WriteLine("FAIL: " + error); Environment.ExitCode = 1; }
        }

        private static void Run(string root)
        {
            NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4));
            string output = Path.Combine(root, "test-artifacts", "drag-animation");
            Directory.CreateDirectory(output);
            foreach (int size in new[] { 160, 220, 280, 340 })
            using (SpriteAssets assets = new SpriteAssets(Path.Combine(root, "assets", "sprites"), size))
            using (DragAnimation drag = new DragAnimation(Path.Combine(root, "assets", "sprites"), assets.Width, size))
            using (LayeredIdleAnimation idle = new LayeredIdleAnimation(Path.Combine(root, "assets", "layers", "idle-v1"), size))
            using (Bitmap comparison = new Bitmap((assets.Width + 12) * 4, (size + 16) * 4))
            using (Graphics g = Graphics.FromImage(comparison))
            {
                Check(drag.FrameCount == 12 && drag.CycleMs == 960, "Approved timing changed");
                if (size == 340) ((Bitmap)Read(drag, "source")).Save(Path.Combine(output, "decoded-sheet.png"), ImageFormat.Png);
                bool changed;
                g.Clear(Color.FromArgb(239, 236, 230));
                Bitmap idleFrame = idle.Render(0, 0, out changed);
                g.DrawImage(idleFrame, new Rectangle(6, 8, assets.Width, size), 0, 0, assets.Width, size, GraphicsUnit.Pixel);
                string first = null, previous = null;
                for (int i = 0; i < 12; i++)
                {
                    Bitmap frame = drag.Render(i * 80, out changed);
                    Check(changed, "Frame did not advance");
                    string current = Hash(frame);
                    Check(current != previous, "Repeated artwork instead of limb animation");
                    if (i == 0) first = current;
                    previous = current;
                    int clear = 0, solid = 0;
                    for (int y = 0; y < frame.Height; y++) for (int x = 0; x < frame.Width; x++)
                    {
                        Color c = frame.GetPixel(x, y);
                        if (c.A == 0) clear++;
                        if (c.A > 240) solid++;
                        Check(!(c.A > 20 && c.G > Math.Max(c.R, c.B) + 20), "Visible green spill at " + size + "/" + i + "/" + x + "/" + y + ": " + c);
                        if (x == 0 || y == 0 || x == frame.Width - 1 || y == frame.Height - 1)
                            Check(c.A == 0, "Clipped canvas edge");
                    }
                    Check(clear > frame.Width * frame.Height / 2 && solid > frame.Width * frame.Height / 10, "Backdrop or missing figure");
                    drag.Render(i * 80 + 50, out changed);
                    Check(!changed, "Unnecessary frame upload");
                    frame.Save(Path.Combine(output, "frame-" + size + "-" + i + ".png"), ImageFormat.Png);
                    int slot = i + 1;
                    g.DrawImage(frame, new Rectangle(slot % 4 * (assets.Width + 12) + 6, slot / 4 * (size + 16) + 8, assets.Width, size),
                        0, 0, assets.Width, size, GraphicsUnit.Pixel);
                }
                Check(Hash(drag.Render(960, out changed)) == first, "Loop does not return to first frame");
                comparison.Save(Path.Combine(output, "compare-" + size + ".png"), ImageFormat.Png);
                if (size == 340) ((Bitmap)Read(drag, "source")).Save(Path.Combine(output, "decoded-sheet.png"), ImageFormat.Png);
                Console.WriteLine("PASS: " + size + " px, twelve distinct adjacent frames, 960ms loop, transparent edges/background, no green spill, cached uploads.");
            }

            using (PetForm pet = new PetForm(root))
            {
                ((NotifyIcon)Read(pet, "tray")).Visible = false;
                typeof(SettingsStore).GetField("PathOnDisk", Flags | BindingFlags.Public).SetValue(Read(pet, "settingsStore"), Path.Combine(output, "isolated-settings.json"));
                try
                {
                    Point pointer = (Point)typeof(PetForm).GetMethod("PhysicalMousePosition", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    Write(pet, "pressed", true);
                    Write(pet, "pressBase", pet.Location);
                    Write(pet, "pressPoint", new Point(pointer.X - 2, pointer.Y));
                    Call(pet, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
                    Check(!(bool)Read(pet, "dragging"), "Click threshold regressed");
                    Write(pet, "pressPoint", new Point(pointer.X - 20, pointer.Y));
                    Call(pet, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
                    Check((Pose)Read(pet, "pose") == Pose.Drag, "Drag did not start");
                    long start = (long)Read(pet, "dragStarted");
                    int clickIndex = (int)Read(pet, "nextClickPose");
                    Call(pet, "RenderMotion", start + 260, true);
                    string phase = Hash(Frame(pet));
                    foreach (Point delta in new[] { new Point(-40, 0), new Point(40, 0), new Point(0, -40), new Point(0, 40) })
                    {
                        Write(pet, "pressPoint", new Point(pointer.X - delta.X, pointer.Y - delta.Y));
                        Call(pet, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
                        Check((long)Read(pet, "dragStarted") == start, "Direction reset animation");
                        Call(pet, "RenderMotion", start + 260, true);
                        Check(Hash(Frame(pet)) == phase, "Direction mirrored animation");
                    }
                    Call(pet, "RenderMotion", start + 390, false);
                    Check(Hash(Frame(pet)) != phase, "Held stationary drag did not animate");
                    Call(pet, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
                    Check((Pose)Read(pet, "pose") == Pose.Idle && (int)Read(pet, "nextClickPose") == clickIndex, "Release triggered click or failed idle");
                    Call(pet, "EnterSleep");
                    Check((Pose)Read(pet, "pose") == Pose.Sleep, "Sleep transition failed");
                    Check(!pet.Visible, "Test showed window");
                    Console.WriteLine("PASS: actual hidden drag handler, threshold, four directions without flips/restarts, stationary animation, release to idle without click, sleep transition; no input injected.");
                }
                finally { Call(pet, "OnFormClosed", new FormClosedEventArgs(CloseReason.ApplicationExitCall)); }
            }
        }
    }
}
