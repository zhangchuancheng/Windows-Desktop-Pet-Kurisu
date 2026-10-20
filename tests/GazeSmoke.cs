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

        private static void RequireUnchanged(byte[] baseline, byte[] sample, int width,
            Rectangle region, string message)
        {
            for (int y = region.Top; y < region.Bottom; y++)
                for (int x = region.Left; x < region.Right; x++)
                {
                    int offset = (y * width + x) * 4;
                    for (int channel = 0; channel < 4; channel++)
                        Require(baseline[offset + channel] == sample[offset + channel], message);
                }
        }

        private static void SavePointerPreview(IdleMotionAnimation animation, string root)
        {
            animation.Resize(355);
            animation.Enter(0);
            Rectangle pet = new Rectangle(500, 500, animation.Width, animation.Height);
            int faceX = (int)Math.Round(pet.Left + pet.Width * 0.51);
            int faceY = (int)Math.Round(pet.Top + pet.Height * 0.33);
            using (Bitmap sheet = new Bitmap(animation.Width * 3, animation.Height * 3,
                PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(sheet))
            {
                graphics.Clear(Color.FromArgb(242, 238, 232));
                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 3; col++)
                    {
                        Point pointer = new Point(faceX + (col - 1) * 200,
                            faceY + (row - 1) * 160);
                        GazeMotion tracking = new GazeMotion();
                        for (int time = 0; time <= 640; time += 16)
                            tracking.Update(time, pointer, pet, true);
                        bool changed;
                        Bitmap frame = animation.Render(1800, 0, tracking.EyeX,
                            tracking.EyeY, tracking.HeadX, tracking.HeadY, out changed);
                        graphics.DrawImageUnscaled(frame,
                            col * animation.Width, row * animation.Height);
                    }
                sheet.Save(Path.Combine(root, "test-artifacts", "screenshots",
                    "gaze-pointer-preview-355.png"), ImageFormat.Png);
            }

            using (Bitmap halfBlink = new Bitmap(animation.Width * 3, animation.Height,
                PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(halfBlink))
            {
                graphics.Clear(Color.FromArgb(242, 238, 232));
                for (int col = 0; col < 3; col++)
                {
                    bool changed;
                    Bitmap frame = animation.Render(1800, 1, col - 1, 0,
                        0, 0, out changed);
                    graphics.DrawImageUnscaled(frame, col * animation.Width, 0);
                }
                halfBlink.Save(Path.Combine(root, "test-artifacts", "screenshots",
                    "gaze-half-blink-355.png"), ImageFormat.Png);
            }

            string frames = Path.Combine(root, "test-artifacts", "gaze-motion-frames");
            Directory.CreateDirectory(frames);
            GazeMotion moving = new GazeMotion();
            for (int index = 0; index < 60; index++)
            {
                int time = index * 40;
                double phase = time * Math.PI * 2 / 2400.0;
                Point pointer = new Point(faceX + (int)Math.Round(200 * Math.Sin(phase)),
                    faceY + (int)Math.Round(120 * Math.Sin(phase * 1.5)));
                moving.Update(time, pointer, pet, true);
                bool changed;
                Bitmap frame = animation.Render(1800 + time, 0, moving.EyeX,
                    moving.EyeY, moving.HeadX, moving.HeadY, out changed);
                frame.Save(Path.Combine(frames, string.Format("frame-{0:D3}.png", index)),
                    ImageFormat.Png);
            }
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

                GazeMotion nearby = new GazeMotion();
                int faceX = (int)Math.Round(pet.Left + pet.Width * 0.51);
                int faceY = (int)Math.Round(pet.Top + pet.Height * 0.33);
                Point nearbyPointer = new Point(faceX + 200, faceY);
                for (int t = 0; t <= 400; t += 16)
                    nearby.Update(t, nearbyPointer, pet, true);
                Require(nearby.EyeX > 0.6 && nearby.EyeX < 0.9,
                    "A pointer 200 px from the face should produce a clear eye turn");
                Require(nearby.HeadX > 0 && nearby.HeadX < nearby.EyeX,
                    "Head should follow the nearby pointer more gently than the eyes");

                string directory = Path.Combine(args[0], "assets", "sprites");
                using (IdleMotionAnimation animation = new IdleMotionAnimation(directory, 220))
                {
                    foreach (int height in new[] { 100, 160, 220, 280, 340, 500 })
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
                        if (height == 340)
                            RequireUnchanged(neutral, eyesOnly, animation.Width,
                                new Rectangle(185, 105, 10, 30), "Eye tracking warped the right side lock");
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
                        Console.WriteLine("PASS: " + height + " px gaze, stable feet, transparent edges, centered composite restored.");
                    }

                    animation.Resize(340);
                    animation.Enter(0);
                    bool sweepChanged;
                    byte[] sweepNeutral = Pixels(animation.Render(1800, 0, out sweepChanged));
                    byte[] halfNeutral = Pixels(animation.Render(1800, 1, out sweepChanged));
                    Rectangle[] protectedArtwork = {
                        new Rectangle(140, 88, 35, 10), // right eyebrow
                        new Rectangle(185, 105, 22, 30), // right side lock
                        new Rectangle(84, 91, 29, 11), // left eyebrow
                        new Rectangle(56, 110, 25, 30) // left side lock
                    };
                    using (Bitmap angles = new Bitmap(animation.Width * 3, animation.Height * 3, PixelFormat.Format32bppArgb))
                    using (Graphics grid = Graphics.FromImage(angles))
                    {
                        grid.Clear(Color.FromArgb(242, 238, 232));
                        for (int row = 0; row < 3; row++)
                            for (int col = 0; col < 3; col++)
                            {
                                double directionX = col - 1, directionY = row - 1;
                                byte[] eyeFrame = Pixels(animation.Render(1800, 0,
                                    directionX, directionY, 0, 0, out sweepChanged));
                                foreach (Rectangle region in protectedArtwork)
                                    RequireUnchanged(sweepNeutral, eyeFrame, animation.Width, region,
                                        "Eye tracking warped the eyebrow or side hair at " + col + "/" + row);
                                Bitmap full = animation.Render(1800, 0, directionX, directionY,
                                    directionX * 0.8, directionY * 0.8, out sweepChanged);
                                byte[] fullPixels = Pixels(full);
                                for (int x = 0; x < animation.Width; x++)
                                {
                                    Require(fullPixels[x * 4 + 3] == 0, "Clipped top edge in gaze sweep");
                                    int bottom = ((animation.Height - 1) * animation.Width + x) * 4;
                                    Require(fullPixels[bottom + 3] == 0, "Clipped bottom edge in gaze sweep");
                                }
                                grid.DrawImageUnscaled(full, col * animation.Width, row * animation.Height);
                            }
                        string anglePath = Path.Combine(args[0], "test-artifacts", "screenshots", "gaze-angle-grid.png");
                        angles.Save(anglePath, ImageFormat.Png);
                    }
                    for (int step = 0; step < 16; step++)
                    {
                        double angle = step * Math.PI / 8;
                        byte[] eyeFrame = Pixels(animation.Render(1800, 0,
                            Math.Cos(angle), Math.Sin(angle), 0, 0, out sweepChanged));
                        byte[] halfFrame = Pixels(animation.Render(1800, 1,
                            Math.Cos(angle), Math.Sin(angle), 0, 0, out sweepChanged));
                        foreach (Rectangle region in protectedArtwork)
                        {
                            RequireUnchanged(sweepNeutral, eyeFrame, animation.Width, region,
                                "Eye tracking warped protected artwork at direction " + step);
                            RequireUnchanged(halfNeutral, halfFrame, animation.Width, region,
                                "Half blink warped protected artwork at direction " + step);
                        }
                    }
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
                    SavePointerPreview(animation, args[0]);
                }
                Console.WriteLine("PASS: visible nearby pointer response, smooth eye/head motion, 8 extreme and 16 intermediate directions, protected brows/hair, inactive return to center.");
            }
            catch (Exception error)
            {
                Console.WriteLine("FAIL: " + error);
                Environment.ExitCode = 1;
            }
        }
    }
}
