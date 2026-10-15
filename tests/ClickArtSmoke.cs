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
        private static byte[] Pixels(Bitmap image)
        {
            byte[] bytes = new byte[image.Width * image.Height * 4];
            BitmapData data = image.LockBits(new Rectangle(Point.Empty, image.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try { for (int y = 0; y < image.Height; y++) Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), bytes, y * image.Width * 4, image.Width * 4); }
            finally { image.UnlockBits(data); }
            return bytes;
        }

        private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4));
                string root = args[0], output = Path.Combine(root, "test-artifacts", "click-refresh");
                Directory.CreateDirectory(output);
                Pose[] poses = { Pose.PokeBlock, Pose.PokeFold, Pose.PokeCheek, Pose.Rapid };
                string[] names = { "poke-block", "poke-fold", "poke-cheek", "rapid" };
                using (SpriteAssets assets = new SpriteAssets(Path.Combine(root, "assets", "sprites"), 220))
                {
                    Dictionary<Pose, Bitmap> originals = (Dictionary<Pose, Bitmap>)typeof(SpriteAssets).GetField("originals", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(assets);
                    for (int i = 0; i < poses.Length; i++)
                    using (Bitmap raw = new Bitmap(Path.Combine(root, "assets", "sprites", names[i] + ".png")))
                    {
                        Bitmap corrected = originals[poses[i]];
                        byte[] before = Pixels(raw), after = Pixels(corrected);
                        int changed = 0;
                        for (int p = 0; p < before.Length; p += 4)
                        {
                            Require(before[p + 3] == after[p + 3], "Calibration changed alpha");
                            for (int c = 0; c < 3; c++)
                            {
                                Require(Math.Abs(after[p + c] - before[p + c]) <= 16, "Excessive color correction");
                                if (after[p + c] != before[p + c]) changed++;
                            }
                        }
                        Require(changed > 0, "Calibration fingerprint did not match");
                        corrected.Save(Path.Combine(output, names[i] + ".png"), ImageFormat.Png);
                        string replacements = Path.Combine(output, "replacements");
                        Directory.CreateDirectory(replacements);
                        string replacement = Path.Combine(replacements, names[i] + ".png");
                        raw.Save(replacement, ImageFormat.Png);
                        ClickColorCalibration calibration = ClickColorCalibration.Load(Path.Combine(root, "assets", "sprites"));
                        Require(calibration != null, "Reference palette fingerprint did not match");
                        Require(calibration.Find(replacement) == null, "Calibration affected replacement artwork");
                        Console.WriteLine("PASS: " + names[i] + " calibration applied, alpha unchanged, replacement artwork bypasses calibration.");
                    }
                    foreach (int height in new[] { 160, 220, 280, 340 })
                    using (LayeredIdleAnimation idle = new LayeredIdleAnimation(Path.Combine(root, "assets", "layers", "idle-v1"), height))
                    {
                        assets.Resize(height);
                        using (Bitmap sheet = new Bitmap((assets.Width + 20) * (poses.Length + 1), height + 40))
                        using (Graphics g = Graphics.FromImage(sheet))
                        {
                            g.Clear(Color.FromArgb(239, 236, 230));
                            bool changed;
                            Bitmap idleFrame = idle.Render(0, 0, out changed);
                            idleFrame.Save(Path.Combine(output, "idle-" + height + ".png"), ImageFormat.Png);
                            g.DrawImage(idleFrame, new Rectangle(10, 20, idleFrame.Width, height), 0, 0, idleFrame.Width, height, GraphicsUnit.Pixel);
                            for (int i = 0; i < poses.Length; i++)
                            {
                                Bitmap frame = assets.Get(poses[i]);
                                frame.Save(Path.Combine(output, names[i] + "-" + height + ".png"), ImageFormat.Png);
                                for (int x = 0; x < frame.Width; x++) Require(frame.GetPixel(x, 0).A <= 1 && frame.GetPixel(x, height - 1).A <= 1, "Vertical clipping");
                                for (int y = 0; y < height; y++) Require(frame.GetPixel(0, y).A <= 1 && frame.GetPixel(frame.Width - 1, y).A <= 1, "Horizontal clipping");
                                g.DrawImage(frame, new Rectangle(10 + (i + 1) * (assets.Width + 20), 20, frame.Width, height), 0, 0, frame.Width, height, GraphicsUnit.Pixel);
                            }
                            sheet.Save(Path.Combine(output, "compare-" + height + ".png"), ImageFormat.Png);
                        }
                        Console.WriteLine("PASS: " + height + " px, normal/rapid click poses load and have transparent canvas edges.");
                    }
                }
            }
            catch (Exception error) { Console.WriteLine("FAIL: " + error); Environment.ExitCode = 1; }
        }
    }
}
