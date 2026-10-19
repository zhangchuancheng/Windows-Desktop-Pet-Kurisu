using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MakiseDesktopPet
{
    internal static class ClickArtSmoke
    {
        private static void Require(bool ok, string message)
        { if (!ok) throw new Exception(message); }

        private static byte[] Pixels(Bitmap image)
        {
            byte[] result = new byte[image.Width * image.Height * 4];
            BitmapData data = image.LockBits(new Rectangle(Point.Empty, image.Size),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < image.Height; y++)
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), result,
                        y * image.Width * 4, image.Width * 4);
            }
            finally { image.UnlockBits(data); }
            return result;
        }

        private static void CheckBlinkSource(string directory, string file)
        {
            using (Bitmap idle = new Bitmap(Path.Combine(directory, "idle.png")))
            using (Bitmap blink = new Bitmap(Path.Combine(directory, file)))
            {
                Require(idle.Size == blink.Size, "Blink canvas changed");
                byte[] a = Pixels(idle), b = Pixels(blink);
                int changed = 0;
                for (int y = 0; y < idle.Height; y++)
                    for (int x = 0; x < idle.Width; x++)
                    {
                        int i = (y * idle.Width + x) * 4;
                        for (int c = 0; c < 4; c++)
                        {
                            if (a[i+c] == b[i+c]) continue;
                            Require(x >= 315 && x < 745 && y >= 400 && y < 550,
                                "Blink altered hair, clothing or silhouette: " + file);
                            if (c == 3) Require(Math.Abs(a[i+c] - b[i+c]) <= 1,
                                "Blink changed face transparency");
                            changed++;
                        }
                    }
                Require(changed > 300, "Blink did not change the eyes");
            }
        }

        private static void CheckClickAlignment(string directory, string file)
        {
            using (Bitmap idle = new Bitmap(Path.Combine(directory, "idle.png")))
            using (Bitmap click = new Bitmap(Path.Combine(directory, file)))
            {
                Require(idle.Size == click.Size, "Click canvas changed: " + file);
                byte[] a = Pixels(idle), b = Pixels(click);
                int overlap = 0, either = 0, footOverlap = 0, footEither = 0;
                for (int y = 0; y < idle.Height; y++)
                    for (int x = 0; x < idle.Width; x++)
                    {
                        int alpha = (y * idle.Width + x) * 4 + 3;
                        bool first = a[alpha] >= 128, second = b[alpha] >= 128;
                        if (first && second) overlap++;
                        if (first || second) either++;
                        if (y < 1050) continue;
                        if (first && second) footOverlap++;
                        if (first || second) footEither++;
                    }
                Require(overlap / (double)either >= 0.96,
                    "Click pose no longer matches idle silhouette: " + file);
                Require(footOverlap / (double)footEither >= 0.92,
                    "Click feet shifted away from idle: " + file);
            }
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
            string directory = Path.Combine(root, "assets", "sprites");
            string output = Path.Combine(root, "test-artifacts", "click-2d");
            Directory.CreateDirectory(output);
            CheckBlinkSource(directory, "blink-half.png");
            CheckBlinkSource(directory, "blink.png");
            foreach (string file in new[] { "poke-block.png", "poke-fold.png", "poke-cheek.png", "rapid.png" })
                CheckClickAlignment(directory, file);
            Pose[] poses = { Pose.Idle, Pose.PokeBlock, Pose.PokeFold, Pose.PokeCheek, Pose.Rapid };
            using (SpriteAssets assets = new SpriteAssets(directory, 220))
            {
                Dictionary<Pose, Bitmap> originals = (Dictionary<Pose, Bitmap>)
                    typeof(SpriteAssets).GetField("originals", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(assets);
                foreach (Pose pose in poses)
                using (Bitmap raw = new Bitmap(Path.Combine(directory,
                    pose == Pose.PokeBlock ? "poke-block.png" :
                    pose == Pose.PokeFold ? "poke-fold.png" :
                    pose == Pose.PokeCheek ? "poke-cheek.png" :
                    pose.ToString().ToLowerInvariant() + ".png")))
                {
                    byte[] a = Pixels(raw), b = Pixels(originals[pose]);
                    Require(a.Length == b.Length, "Sprite canvas mismatch: " + pose);
                    for (int i = 0; i < a.Length; i++)
                        Require(a[i] == b[i], "Unexpected runtime color correction: " + pose);
                }
                foreach (int height in new[] { 100, 220, 340, 500 })
                using (IdleMotionAnimation idle = new IdleMotionAnimation(directory, height))
                {
                    assets.Resize(height);
                    using (Bitmap sheet = new Bitmap((assets.Width + 20) * poses.Length,
                        height + 40, PixelFormat.Format32bppArgb))
                    using (Graphics g = Graphics.FromImage(sheet))
                    {
                        g.Clear(Color.FromArgb(239, 236, 230));
                        bool changed;
                        Bitmap idleFrame = idle.Render(0, 0, out changed);
                        g.DrawImageUnscaled(idleFrame, 10, 20);
                        for (int i = 1; i < poses.Length; i++)
                        {
                            Bitmap frame = assets.Get(poses[i]);
                            for (int x = 0; x < frame.Width; x++)
                                Require(frame.GetPixel(x, 0).A == 0 &&
                                    frame.GetPixel(x, height - 1).A == 0, "Vertical clipping");
                            for (int y = 0; y < height; y++)
                                Require(frame.GetPixel(0, y).A == 0 &&
                                    frame.GetPixel(frame.Width - 1, y).A == 0, "Horizontal clipping");
                            g.DrawImageUnscaled(frame, 10 + i * (assets.Width + 20), 20);
                        }
                        sheet.Save(Path.Combine(output, "compare-" + height + ".png"));
                    }
                    Console.WriteLine("PASS: " + height + " px, new idle/click art, stable colors and transparent edges.");
                }
            }
            Console.WriteLine("PASS: both blink frames modify the eyes only; click silhouettes and feet align with idle.");
        }
    }
}
