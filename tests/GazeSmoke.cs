using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace MakiseDesktopPet
{
    internal static class GazeSmoke
    {
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static byte[] Pixels(Bitmap image)
        {
            byte[] result = new byte[image.Width * image.Height * 4];
            BitmapData data = image.LockBits(new Rectangle(Point.Empty, image.Size),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                for (int y = 0; y < image.Height; y++)
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), result,
                        y * image.Width * 4, image.Width * 4);
            }
            finally { image.UnlockBits(data); }
            return result;
        }

        private static void Main(string[] args)
        {
            try
            {
                GazeMotion gaze = new GazeMotion();
                Rectangle pet = new Rectangle(100, 100, 176, 220);
                Point right = new Point(1400, 170);
                gaze.Update(0, right, pet, true);
                gaze.Update(16, right, pet, true);
                Require(gaze.EyeX > gaze.HeadX && gaze.HeadX > 0, "Eyes should lead the head");
                double first = gaze.EyeX;
                gaze.Update(32, new Point(-1100, 170), pet, true);
                Require(gaze.EyeX < first && gaze.EyeX > -0.5, "Direction reversal snapped");
                for (int t = 48; t <= 1024; t += 16)
                    gaze.Update(t, new Point(-1100, 170), pet, true);
                Require(gaze.EyeX < -0.8 && gaze.HeadX < -0.7, "Gaze did not turn left");
                for (int t = 1040; t <= 2640; t += 16)
                    gaze.Update(t, Point.Empty, pet, false);
                Require(Math.Abs(gaze.EyeX) < 0.001 && Math.Abs(gaze.HeadX) < 0.02,
                    "Gaze did not relax while inactive");

                string directory = Path.Combine(args[0], "assets", "layers", "idle-v1");
                using (LayeredIdleAnimation animation = new LayeredIdleAnimation(directory, 220))
                {
                    foreach (int height in new[] { 160, 220, 280, 340 })
                    {
                        animation.Resize(height);
                        animation.Enter(0);
                        bool changed;
                        byte[] neutral = Pixels(animation.Render(1800, 0, out changed));
                        byte[] eyesOnly = Pixels(animation.Render(1800, 0, 0.95, -0.75,
                            0, 0, out changed));
                        byte[] tracked = Pixels(animation.Render(1800, 0, 0.95, -0.75,
                            0.8, -0.6, out changed));
                        int stride = animation.Width * 4;
                        int different = 0, eyeDifferent = 0;
                        for (int y = 0; y < height; y++)
                            for (int x = 0; x < animation.Width; x++)
                            {
                                int offset = y * stride + x * 4;
                                Require(neutral[offset + 3] == eyesOnly[offset + 3],
                                    "Eye movement changed the character silhouette");
                                for (int c = 0; c < 4; c++)
                                {
                                    Require(c == 3 || tracked[offset + c] <= tracked[offset + 3],
                                        "Invalid premultiplied color");
                                    if (y >= (int)(height * 0.88))
                                        Require(neutral[offset + c] == tracked[offset + c], "Feet shifted");
                                }
                                if (y == 0 || y == height - 1 || x == 0 || x == animation.Width - 1)
                                    Require(tracked[offset + 3] == 0, "Gaze was clipped");
                                if (neutral[offset] != tracked[offset] || neutral[offset + 1] != tracked[offset + 1] ||
                                    neutral[offset + 2] != tracked[offset + 2] || neutral[offset + 3] != tracked[offset + 3])
                                    different++;
                                if (neutral[offset] != eyesOnly[offset] || neutral[offset + 1] != eyesOnly[offset + 1] ||
                                    neutral[offset + 2] != eyesOnly[offset + 2])
                                    eyeDifferent++;
                            }
                        Require(different > 20, "Gaze did not visibly change the frame");
                        Require(eyeDifferent > 15, "Iris movement was not visible without head motion");
                        byte[] returned = Pixels(animation.Render(1800, 0, out changed));
                        for (int i = 0; i < neutral.Length; i++)
                            Require(neutral[i] == returned[i], "Neutral frame accumulated color changes");
                        Console.WriteLine("PASS: " + height + " px gaze, stable feet, transparent edges, original colors restored.");
                    }

                    animation.Resize(340);
                    animation.Enter(0);
                    using (Bitmap sheet = new Bitmap(animation.Width * 3, 350, PixelFormat.Format32bppArgb))
                    using (Graphics g = Graphics.FromImage(sheet))
                    {
                        g.Clear(Color.FromArgb(242, 238, 232));
                        bool changed;
                        g.DrawImageUnscaled(animation.Render(1800, 0, -0.95, -0.6,
                            -0.75, -0.55, out changed), 0, 0);
                        g.DrawImageUnscaled(animation.Render(1800, 0, out changed), animation.Width, 0);
                        g.DrawImageUnscaled(animation.Render(1800, 0, 0.95, 0.65,
                            0.75, 0.55, out changed), animation.Width * 2, 0);
                        Directory.CreateDirectory(Path.Combine(args[0], "test-artifacts", "screenshots"));
                        sheet.Save(Path.Combine(args[0], "test-artifacts", "screenshots", "gaze-preview.png"));
                    }
                }
                Console.WriteLine("PASS: smooth eye/head response and inactive return to center.");
            }
            catch (Exception error)
            {
                Console.WriteLine("FAIL: " + error);
                Environment.ExitCode = 1;
            }
        }
    }
}
