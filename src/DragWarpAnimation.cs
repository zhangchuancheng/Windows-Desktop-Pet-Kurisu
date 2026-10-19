using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

namespace MakiseDesktopPet
{
    // One complete source pose is deformed continuously. There are no cut-out parts.
    internal sealed class DragWarpAnimation : IDisposable
    {
        public sealed class Layout
        {
            public string image { get; set; }
            public int[] canvas { get; set; }
            public int[][] tearOrigins { get; set; }
            public Placement placement { get; set; }
        }

        public sealed class Placement
        {
            public double widthRatio { get; set; }
            public double heightRatio { get; set; }
            public double offsetXRatio { get; set; }
            public double topRatio { get; set; }
        }

        private const int MotionFrameMs = 16;
        private readonly Layout layout;
        private readonly Bitmap source;
        private Bitmap baseFrame;
        private Bitmap output;
        private byte[] sourcePixels;
        private byte[] pixels;
        private readonly float[][] weights = new float[6][];
        private readonly double[] position = new double[8];
        private readonly double[] velocity = new double[8];
        private static readonly double[] Frequency = { 2.9, 2.5, 2.6, 2.7, 3.4, 3.1, 2.0, 1.8 };
        private Point latestPointer;
        private Point sampledPointer;
        private bool pointerDirty;
        private long previousStep = -1;
        private long previousElapsed = -1;
        private float scaleX;
        private float scaleY;
        private float motionScaleX;
        private float motionScaleY;
        private float drawLeft;
        private float drawTop;

        internal DragWarpAnimation(string directory, int width, int height)
        {
            layout = new JavaScriptSerializer().Deserialize<Layout>(File.ReadAllText(
                Path.Combine(directory, "drag-warp.json")));
            if (layout == null || string.IsNullOrEmpty(layout.image) || layout.canvas == null ||
                layout.canvas.Length != 2 || layout.canvas[0] < 32 || layout.canvas[1] < 32 ||
                layout.tearOrigins == null || layout.tearOrigins.Length != 2 ||
                layout.tearOrigins[0] == null || layout.tearOrigins[1] == null ||
                layout.tearOrigins[0].Length != 2 || layout.tearOrigins[1].Length != 2 ||
                layout.placement == null || layout.placement.widthRatio < 0.5 ||
                layout.placement.widthRatio > 1.5 || layout.placement.heightRatio < 0.5 ||
                layout.placement.heightRatio > 1.5 ||
                Math.Abs(layout.placement.offsetXRatio) > 0.2 ||
                Math.Abs(layout.placement.topRatio) > 0.2)
                throw new InvalidDataException("Invalid drag warp layout.");
            string path = Path.Combine(directory, layout.image);
            using (Bitmap loaded = new Bitmap(path))
                source = loaded.Clone(new Rectangle(Point.Empty, loaded.Size), PixelFormat.Format32bppArgb);
            if (source.Width != layout.canvas[0] || source.Height != layout.canvas[1] ||
                source.Width < 32 || source.Height < 32)
                throw new InvalidDataException("Drag image does not match its declared canvas.");
            Resize(width, height);
        }

        private static double SmoothStep(double low, double high, double value)
        {
            double t = Math.Max(0, Math.Min(1, (value - low) / (high - low)));
            return t * t * (3 - 2 * t);
        }

        private static float Influence(double x, double y, double center, double width,
            double start, double full, double fadeStart, double fadeEnd)
        {
            double horizontal = (x - center) / width;
            return (float)(Math.Exp(-0.5 * horizontal * horizontal) *
                SmoothStep(start, full, y) * (1 - SmoothStep(fadeStart, fadeEnd, y)));
        }

        private static void ReadPixels(Bitmap bitmap, byte[] destination)
        {
            BitmapData data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                int stride = bitmap.Width * 4;
                for (int y = 0; y < bitmap.Height; y++)
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), destination, y * stride, stride);
            }
            finally { bitmap.UnlockBits(data); }
        }

        private void WritePixels()
        {
            BitmapData data = output.LockBits(new Rectangle(Point.Empty, output.Size),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                int stride = output.Width * 4;
                for (int y = 0; y < output.Height; y++)
                    Marshal.Copy(pixels, y * stride, IntPtr.Add(data.Scan0, y * data.Stride), stride);
            }
            finally { output.UnlockBits(data); }
        }

        internal void Resize(int width, int height)
        {
            if (width < 16 || height < 16) throw new ArgumentOutOfRangeException("height");
            if (baseFrame != null) baseFrame.Dispose();
            if (output != null) output.Dispose();
            baseFrame = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            output = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            // Match the visible crown and shoes of the idle/click drawings.
            // The drag source has more empty pixels below the shoes, so fitting
            // its full canvas to the window made the character visibly shorter.
            int drawWidth = Math.Max(1, (int)Math.Round(width * layout.placement.widthRatio));
            int drawHeight = Math.Max(1, (int)Math.Round(height * layout.placement.heightRatio));
            scaleX = drawWidth / (float)source.Width;
            scaleY = drawHeight / (float)source.Height;
            // Spring positions use the original logical 313x418 coordinate
            // space. This keeps movement equally visible with a high-resolution
            // replacement drawing and at every on-screen pet size.
            motionScaleX = drawWidth / 313f;
            motionScaleY = drawHeight / 418f;
            drawLeft = (width - drawWidth) / 2f + (float)(width * layout.placement.offsetXRatio);
            drawTop = (float)(height * layout.placement.topRatio);
            using (Graphics g = Graphics.FromImage(baseFrame))
            {
                g.PageUnit = GraphicsUnit.Pixel;
                g.Clear(Color.Transparent);
                g.CompositingMode = CompositingMode.SourceCopy;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(source, new RectangleF(drawLeft, drawTop, drawWidth, drawHeight),
                    new RectangleF(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
            }
            sourcePixels = new byte[width * height * 4];
            pixels = new byte[sourcePixels.Length];
            ReadPixels(baseFrame, sourcePixels);
            for (int i = 0; i < weights.Length; i++) weights[i] = new float[width * height];
            double sxFactor = source.Width / 313.0, syFactor = source.Height / 418.0;
            for (int y = 0; y < height; y++)
            {
                double sy = (y - drawTop) / scaleY / syFactor;
                for (int x = 0; x < width; x++)
                {
                    double sx = (x - drawLeft) / scaleX / sxFactor;
                    int index = y * width + x;
                    weights[0][index] = Influence(sx, sy, 138, 23, 265, 348, 418, 419);
                    weights[1][index] = Influence(sx, sy, 205, 23, 265, 348, 418, 419);
                    weights[2][index] = Influence(sx, sy, 132, 24, 195, 279, 307, 331);
                    weights[3][index] = Influence(sx, sy, 196, 24, 195, 279, 307, 331);
                    weights[4][index] = Influence(sx, sy, 75, 35, 152, 230, 280, 308);
                    weights[5][index] = Influence(sx, sy, 245, 35, 152, 230, 280, 308);
                }
            }
            ResetMotion(Point.Empty);
        }

        internal void ResetMotion(Point pointer)
        {
            Array.Clear(position, 0, position.Length);
            Array.Clear(velocity, 0, velocity.Length);
            latestPointer = sampledPointer = pointer;
            pointerDirty = false;
            previousElapsed = -1;
            previousStep = -1;
        }

        internal void ObservePointer(Point pointer)
        {
            if (pointer == latestPointer) return;
            latestPointer = pointer;
            pointerDirty = true;
        }

        private void Advance(double[] target, double seconds)
        {
            double total = Math.Min(0.12, Math.Max(0, seconds));
            int steps = Math.Max(1, (int)Math.Ceiling(total / 0.008));
            double dt = total / steps;
            for (int step = 0; step < steps; step++)
                for (int i = 0; i < position.Length; i++)
                {
                    double omega = 2 * Math.PI * Frequency[i];
                    double acceleration = omega * omega * (target[i] - position[i]) -
                        1.64 * omega * velocity[i];
                    velocity[i] += acceleration * dt;
                    position[i] += velocity[i] * dt;
                }
        }

        private static byte RoundByte(double value)
        {
            return (byte)Math.Max(0, Math.Min(255, (int)Math.Round(value)));
        }

        private void Warp()
        {
            bool still = true;
            for (int i = 0; i < position.Length; i++)
                if (Math.Abs(position[i]) >= 0.015) { still = false; break; }
            if (still)
            {
                Buffer.BlockCopy(sourcePixels, 0, pixels, 0, pixels.Length);
                return;
            }
            int width = output.Width, height = output.Height;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int pixel = y * width + x, target = pixel * 4;
                    double dx = position[0] * weights[0][pixel] + position[1] * weights[1][pixel] +
                        position[4] * weights[2][pixel] + position[5] * weights[3][pixel] +
                        position[6] * weights[4][pixel] + position[7] * weights[5][pixel];
                    double dy = position[2] * weights[0][pixel] + position[3] * weights[1][pixel];
                    if (Math.Abs(dx) + Math.Abs(dy) < 0.015)
                    {
                        Buffer.BlockCopy(sourcePixels, target, pixels, target, 4);
                        continue;
                    }
                    double sx = x - dx * motionScaleX, sy = y - dy * motionScaleY;
                    int x0 = (int)Math.Floor(sx), y0 = (int)Math.Floor(sy);
                    int x1 = x0 + 1, y1 = y0 + 1;
                    double tx = sx - x0, ty = sy - y0;
                    x0 = Math.Max(0, Math.Min(width - 1, x0));
                    x1 = Math.Max(0, Math.Min(width - 1, x1));
                    y0 = Math.Max(0, Math.Min(height - 1, y0));
                    y1 = Math.Max(0, Math.Min(height - 1, y1));
                    int a = (y0 * width + x0) * 4, b = (y0 * width + x1) * 4;
                    int c = (y1 * width + x0) * 4, d = (y1 * width + x1) * 4;
                    double wa = (1 - tx) * (1 - ty), wb = tx * (1 - ty);
                    double wc = (1 - tx) * ty, wd = tx * ty;
                    for (int channel = 0; channel < 4; channel++)
                        pixels[target + channel] = RoundByte(sourcePixels[a + channel] * wa +
                            sourcePixels[b + channel] * wb + sourcePixels[c + channel] * wc +
                            sourcePixels[d + channel] * wd);
                }
        }

        internal Bitmap Render(long elapsed, out bool changed)
        {
            elapsed = Math.Max(0, elapsed);
            long step = elapsed / MotionFrameMs;
            changed = step != previousStep || pointerDirty;
            if (!changed) return output;
            double dt = previousElapsed < 0 ? 0 : Math.Max(0, (elapsed - previousElapsed) / 1000.0);
            double sampleTime = Math.Max(0.008, dt);
            double vx = (latestPointer.X - sampledPointer.X) / sampleTime / motionScaleX;
            double vy = (latestPointer.Y - sampledPointer.Y) / sampleTime / motionScaleY;
            double horizontal = Math.Tanh(vx / 220.0), vertical = Math.Tanh(vy / 150.0);
            double[] target = {
                -15 * horizontal - 2.5 * vertical, -10 * horizontal + 2.5 * vertical,
                -4.5 * horizontal, 4.5 * horizontal,
                -7 * horizontal, -8 * horizontal, -5 * horizontal, -6 * horizontal
            };
            Advance(target, dt);
            Warp();
            WritePixels();
            using (Graphics g = Graphics.FromImage(output))
            {
                g.CompositingMode = CompositingMode.SourceOver;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                DrawTear(g, 0, elapsed, 100);
                DrawTear(g, 1, elapsed, 730);
            }
            sampledPointer = latestPointer;
            pointerDirty = false;
            previousElapsed = elapsed;
            previousStep = step;
            return output;
        }

        private void DrawTear(Graphics g, int side, long elapsed, int start)
        {
            const int cycle = 1400, duration = 540;
            if (elapsed < start) return;
            double progress = ((elapsed - start) % cycle) / (double)duration;
            if (progress <= 0 || progress >= 1) return;
            float alpha = (float)(190 * Math.Sin(progress * Math.PI));
            float radius = Math.Max(1.8f, output.Height * 0.008f);
            float x = drawLeft + layout.tearOrigins[side][0] * scaleX +
                (float)(Math.Sin(progress * Math.PI) * (side == 0 ? -1 : 1) * radius);
            float y = drawTop + layout.tearOrigins[side][1] * scaleY +
                (float)(output.Height * 0.075 * progress);
            using (SolidBrush drop = new SolidBrush(Color.FromArgb((int)alpha, 77, 190, 232)))
            using (SolidBrush shine = new SolidBrush(Color.FromArgb((int)(alpha * 0.75f), 245, 253, 255)))
            {
                g.FillEllipse(drop, x - radius, y - radius, radius * 2, radius * 2.5f);
                g.FillEllipse(shine, x - radius * 0.5f, y - radius * 0.55f,
                    radius * 0.55f, radius * 0.8f);
            }
        }

        public void Dispose()
        {
            if (baseFrame != null) baseFrame.Dispose();
            if (output != null) output.Dispose();
            source.Dispose();
        }
    }
}
