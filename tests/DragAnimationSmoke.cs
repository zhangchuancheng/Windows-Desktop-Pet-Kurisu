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

        private static void CheckBitmap(Bitmap bitmap, int size)
        {
            int transparent = 0, opaque = 0;
            for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < bitmap.Width; x++)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    if (pixel.A == 0) transparent++;
                    if (pixel.A > 240) opaque++;
                    if (x == 0 || y == 0 || x == bitmap.Width - 1 || y == bitmap.Height - 1)
                        Check(pixel.A == 0, "Clipped drag sprite at " + size + "px");
                    Check(!(pixel.A > 20 && pixel.G > Math.Max(pixel.R, pixel.B) + 20),
                        "Visible green spill at " + size + "px");
                }
            Check(transparent > bitmap.Width * bitmap.Height / 2 &&
                opaque > bitmap.Width * bitmap.Height / 10, "Missing figure or opaque backdrop");
        }

        private static Rectangle VisibleBounds(Bitmap bitmap)
        {
            int left = bitmap.Width, top = bitmap.Height, right = 0, bottom = 0;
            for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < bitmap.Width; x++)
                    if (bitmap.GetPixel(x, y).A >= 128)
                    {
                        left = Math.Min(left, x); top = Math.Min(top, y);
                        right = Math.Max(right, x + 1); bottom = Math.Max(bottom, y + 1);
                    }
            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        private static void CheckAlignment(SpriteAssets assets, Bitmap drag, int size)
        {
            Rectangle actual = VisibleBounds(drag);
            int edgeTolerance = Math.Max(2, (int)Math.Ceiling(size * 0.008));
            int widthTolerance = Math.Max(3, (int)Math.Ceiling(size * 0.012));
            foreach (Pose pose in new[] { Pose.Idle, Pose.PokeBlock, Pose.PokeFold, Pose.PokeCheek })
            {
                Rectangle reference = VisibleBounds(assets.Get(pose));
                Check(Math.Abs(actual.Top - reference.Top) <= edgeTolerance &&
                    Math.Abs(actual.Bottom - reference.Bottom) <= edgeTolerance &&
                    Math.Abs(actual.Left - reference.Left) <= edgeTolerance &&
                    Math.Abs(actual.Right - reference.Right) <= edgeTolerance &&
                    Math.Abs(actual.Width - reference.Width) <= widthTolerance,
                    "Drag visible bounds differ from " + pose + " at " + size + "px: " +
                    actual + " vs " + reference);
            }
        }

        private static void Run(string root)
        {
            NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4));
            string output = Path.Combine(root, "test-artifacts", "drag-warp-runtime");
            Directory.CreateDirectory(output);
            foreach (int size in new[] { 100, 160, 220, 280, 340, 355, 500 })
            using (SpriteAssets assets = new SpriteAssets(Path.Combine(root, "assets", "sprites"), size))
            using (DragWarpAnimation drag = new DragWarpAnimation(Path.Combine(root, "assets", "sprites"), assets.Width, size))
            {
                if (size == 340) ((Bitmap)Read(drag, "source")).Save(
                    Path.Combine(output, "calibrated-base.png"), ImageFormat.Png);
                drag.ResetMotion(Point.Empty);
                bool changed;
                Bitmap rest = drag.Render(0, out changed);
                string original = Hash((Bitmap)Read(drag, "baseFrame"));
                Check(changed && Hash(rest) == original,
                    "Resting drag image differs from its single source pose");
                CheckBitmap(rest, size);
                CheckAlignment(assets, rest, size);
                if (size == 340) rest.Save(Path.Combine(output, "rest-340.png"), ImageFormat.Png);
                if (size == 355)
                {
                    rest.Save(Path.Combine(output, "rest-355.png"), ImageFormat.Png);
                    assets.Get(Pose.PokeFold).Save(Path.Combine(output, "click-355.png"), ImageFormat.Png);
                    using (IdleMotionAnimation idle = new IdleMotionAnimation(
                        Path.Combine(root, "assets", "sprites"), size))
                    {
                        idle.Enter(0);
                        bool idleChanged;
                        idle.Render(0, 0, out idleChanged).Save(
                            Path.Combine(output, "idle-355.png"), ImageFormat.Png);
                    }
                }
                drag.Render(1, out changed);
                Check(!changed, "Unnecessary upload within one motion tick");

                drag.ObservePointer(new Point(24, 0));
                drag.Render(16, out changed);
                Check(changed, "Pointer motion did not render");
                drag.ObservePointer(new Point(48, 0));
                Bitmap right = drag.Render(32, out changed);
                Check(Hash(right) != original, "Rightward drag remained static");
                Check(((double[])Read(drag, "position"))[0] < -0.5,
                    "Legs did not lag behind rightward drag");
                CheckBitmap(right, size);
                if (size == 340) right.Save(Path.Combine(output, "right-340.png"), ImageFormat.Png);

                for (int i = 1; i <= 12; i++)
                {
                    drag.ObservePointer(new Point(48 - i * 24, 0));
                    drag.Render(32 + i * 16, out changed);
                }
                Check(((double[])Read(drag, "position"))[0] > 0.5,
                    "Legs did not reverse with leftward drag");
                CheckBitmap(drag.Render(225, out changed), size);
                if (size == 340) drag.Render(224, out changed).Save(Path.Combine(output, "left-340.png"), ImageFormat.Png);

                for (int i = 1; i <= 70; i++) drag.Render(224 + i * 16, out changed);
                Check(Math.Abs(((double[])Read(drag, "position"))[0]) < 0.15,
                    "Motion did not settle when pointer stopped");
                if (size == 340) drag.Render(1360, out changed).Save(Path.Combine(output, "settled-340.png"), ImageFormat.Png);
                if (size == 340)
                {
                    string preview = Path.Combine(root, "test-artifacts", "drag-2d-preview-frames");
                    Directory.CreateDirectory(preview);
                    drag.ResetMotion(Point.Empty);
                    for (int i = 0; i < 80; i++)
                    {
                        double angle = i * Math.PI * 2 / 32;
                        Point pointer = new Point((int)Math.Round(130 * Math.Sin(angle)),
                            (int)Math.Round(25 * Math.Sin(i * Math.PI * 2 / 48)));
                        drag.ObservePointer(pointer);
                        drag.Render(i * 32, out changed).Save(
                            Path.Combine(preview, i.ToString("D3") + ".png"), ImageFormat.Png);
                    }
                }
                if (size == 500)
                {
                    drag.ResetMotion(Point.Empty);
                    Stopwatch watch = Stopwatch.StartNew();
                    for (int i = 0; i < 100; i++)
                    {
                        drag.ObservePointer(new Point(i * 4, i % 3));
                        drag.Render(i * 16, out changed);
                    }
                    watch.Stop();
                    Console.WriteLine("500 px continuous warp average: " +
                        (watch.Elapsed.TotalMilliseconds / 100).ToString("F2") + " ms/frame");
                }
                Console.WriteLine("PASS: " + size + "px, exact rest, both drag directions, settling, transparent boundary.");
            }

            using (PetForm pet = new PetForm(root))
            {
                ((NotifyIcon)Read(pet, "tray")).Visible = false;
                typeof(SettingsStore).GetField("PathOnDisk", Flags | BindingFlags.Public).SetValue(
                    Read(pet, "settingsStore"), Path.Combine(output, "isolated-settings.json"));
                try
                {
                    Point pointer = (Point)typeof(PetForm).GetMethod("PhysicalMousePosition",
                        BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    Write(pet, "pressed", true);
                    Write(pet, "pressBase", pet.Location);
                    Write(pet, "pressPoint", new Point(pointer.X - 2, pointer.Y));
                    Call(pet, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
                    Check(!(bool)Read(pet, "dragging"), "Click threshold regressed");
                    Write(pet, "pressPoint", new Point(pointer.X - 20, pointer.Y));
                    Call(pet, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
                    Check((Pose)Read(pet, "pose") == Pose.Drag, "Drag did not start");
                    int clickIndex = (int)Read(pet, "nextClickPose");
                    long start = (long)Read(pet, "dragStarted");
                    foreach (Point delta in new[] { new Point(-40, 0), new Point(40, 0),
                        new Point(0, -40), new Point(0, 40) })
                    {
                        Write(pet, "pressPoint", new Point(pointer.X - delta.X, pointer.Y - delta.Y));
                        Call(pet, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
                        Check((long)Read(pet, "dragStarted") == start, "Direction restarted drag");
                    }
                    Call(pet, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
                    Check((Pose)Read(pet, "pose") == Pose.Idle &&
                        (int)Read(pet, "nextClickPose") == clickIndex,
                        "Release triggered a click or failed to restore idle");
                    Check(!pet.Visible, "Hidden integration test showed a window");
                    Call(pet, "ApplySize", 500);
                    IntPtr hiddenHandle = pet.Handle;
                    Write(pet, "pressed", true);
                    Call(pet, "SetPose", Pose.Drag);
                    long uploadStart = (long)Read(pet, "dragStarted");
                    DragWarpAnimation engine = (DragWarpAnimation)Read(pet, "dragAnimation");
                    Stopwatch uploadWatch = Stopwatch.StartNew();
                    for (int i = 0; i < 80; i++)
                    {
                        engine.ObservePointer(new Point(pointer.X + i * 4, pointer.Y + i % 3));
                        Call(pet, "RenderMotion", uploadStart + i * 16L, true);
                    }
                    uploadWatch.Stop();
                    Console.WriteLine("500 px warp plus hidden window upload average: " +
                        (uploadWatch.Elapsed.TotalMilliseconds / 80).ToString("F2") + " ms/frame");
                    Console.WriteLine("PASS: hidden form threshold, four directions, release and click separation.");
                }
                finally { Call(pet, "OnFormClosed", new FormClosedEventArgs(CloseReason.ApplicationExitCall)); }
            }
        }
    }
}
