using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace MakiseDesktopPet
{
    internal static class LayeredIdleSmoke
    {
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static byte[] Bytes(Bitmap bitmap)
        {
            int stride = bitmap.Width * 4;
            byte[] pixels = new byte[stride * bitmap.Height];
            BitmapData data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try { for (int y = 0; y < bitmap.Height; y++) Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * stride, stride); }
            finally { bitmap.UnlockBits(data); }
            return pixels;
        }
        private static void Main(string[] args)
        {
            try
            {
                NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4));
                string directory = Path.Combine(args[0], "assets", "layers", "idle-v1");
                using (LayeredIdleAnimation animation = new LayeredIdleAnimation(directory, 220))
                {
                    foreach (int height in new[] { 160, 220, 280, 340 })
                    {
                        animation.Resize(height);
                        animation.Enter(0);
                        bool changed;
                        byte[] baseline = Bytes(animation.Render(0, 0, out changed));
                        bool moved = false;
                        for (int t = 0; t <= 12600; t += 210)
                        {
                            Bitmap frame = animation.Render(t, t / 210 % 3, out changed);
                            byte[] pixels = Bytes(frame);
                            int stride = frame.Width * 4;
                            Require(frame.PixelFormat == PixelFormat.Format32bppPArgb, "Wrong format");
                            for (int y = 0; y < height; y++)
                                for (int x = 0; x < frame.Width; x++)
                                {
                                    int i = y * stride + x * 4;
                                    if (y == 0 || y == height - 1 || x == 0 || x == frame.Width - 1)
                                        Require(pixels[i + 3] == 0, "Clipped at canvas edge");
                                    for (int c = 0; c < 4; c++)
                                    {
                                        Require(c == 3 || pixels[i+c] <= pixels[i+3], "Invalid premultiplied alpha");
                                        if (y >= (int)(height * 0.88)) Require(pixels[i+c] == baseline[i+c], "Feet moved during layered animation");
                                        else if (pixels[i+c] != baseline[i+c]) moved = true;
                                    }
                                }
                        }
                        Require(moved, "Layered animation remained static");
                        Console.WriteLine("PASS: " + height + " px, 61 samples / three eye frames, stable feet, valid alpha, transparent canvas edges.");
                    }
                    animation.Resize(340);
                    animation.Enter(0);
                    using (Bitmap sheet = new Bitmap(animation.Width * 3, 370, PixelFormat.Format32bppArgb))
                    using (Graphics g = Graphics.FromImage(sheet))
                    {
                        g.Clear(Color.FromArgb(238, 235, 228));
                        for (int i=0;i<3;i++)
                        {
                            bool changed;
                            g.DrawImageUnscaled(animation.Render(1800, i, out changed), i * animation.Width, 0);
                        }
                        sheet.Save(Path.Combine(args[0], "test-artifacts", "screenshots", "layered-step3-runtime.png"));
                    }
                }
                long start=1000, end=1240;
                Require(LayeredIdleAnimation.EyeForBlink(1000,start,end)==1, "Missing closing half frame");
                Require(LayeredIdleAnimation.EyeForBlink(1045,start,end)==2, "Missing closed frame");
                Require(LayeredIdleAnimation.EyeForBlink(1190,start,end)==1, "Missing opening half frame");
                Require(LayeredIdleAnimation.EyeForBlink(1240,start,end)==0, "Eyes did not reopen");
                Console.WriteLine("PASS: half / closed / half / open blink sequence.");
            }
            catch(Exception error) { Console.WriteLine("FAIL: " + error); Environment.ExitCode=1; }
        }
    }
}
