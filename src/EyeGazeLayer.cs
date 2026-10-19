using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MakiseDesktopPet
{
    // Turn the painted eye surface beneath a fixed eyelid. The opening is
    // described by the art manifest so adjacent hair and brows stay intact.
    internal sealed class EyeGazeLayer : IDisposable
    {
        internal sealed class Region
        {
            internal readonly double X, Y, Left, Right, Top, Bottom;
            private readonly double[][] upper, lower;

            internal Region(float[] center, float[][][] aperture, double scaleX,
                double scaleY, Rectangle crop)
            {
                X = center[0] * scaleX - crop.Left;
                Y = center[1] * scaleY - crop.Top;
                upper = Scale(aperture[0], scaleX, scaleY, crop);
                lower = Scale(aperture[1], scaleX, scaleY, crop);
                Left = upper[0][0];
                Right = upper[upper.Length - 1][0];
                Top = double.MaxValue;
                Bottom = double.MinValue;
                foreach (double[] point in upper) Top = Math.Min(Top, point[1]);
                foreach (double[] point in lower) Bottom = Math.Max(Bottom, point[1]);
            }

            private static double[][] Scale(float[][] points, double sx,
                double sy, Rectangle crop)
            {
                double[][] result = new double[points.Length][];
                for (int i = 0; i < points.Length; i++)
                    result[i] = new[] { points[i][0] * sx - crop.Left,
                        points[i][1] * sy - crop.Top };
                return result;
            }

            private static double HeightAt(double[][] points, double x)
            {
                for (int i = 1; i < points.Length; i++)
                    if (x <= points[i][0])
                    {
                        double fraction = (x - points[i - 1][0]) /
                            (points[i][0] - points[i - 1][0]);
                        return points[i - 1][1] * (1 - fraction) + points[i][1] * fraction;
                    }
                return points[points.Length - 1][1];
            }

            internal bool Contains(double x, double y)
            {
                return x >= Left && x <= Right &&
                    y >= HeightAt(upper, x) && y <= HeightAt(lower, x);
            }

            internal double EdgeDistance(double x, double y)
            {
                return Math.Min(Math.Min(x - Left, Right - x),
                    Math.Min(y - HeightAt(upper, x), HeightAt(lower, x) - y));
            }

            internal void ClampSample(ref double x, ref double y)
            {
                // The artwork at the eye rim supplies the exposed sclera.
                // Reusing that paint avoids a flat, synthetic white gap.
                x = Math.Max(Left + 0.75, Math.Min(Right - 0.75, x));
                double top = HeightAt(upper, x) + 0.75;
                double bottom = HeightAt(lower, x) - 0.75;
                if (bottom < top) y = (top + bottom) * 0.5;
                else y = Math.Max(top, Math.Min(bottom, y));
            }
        }

        private readonly Bitmap source, output;
        private readonly byte[] original, pixels;
        private readonly Region[] regions;
        private readonly double maxX, maxY;
        private double previousX = double.NaN, previousY = double.NaN;

        internal EyeGazeLayer(Bitmap source, Rectangle crop, Size canvas, Size size,
            float[][] centers, float[][][][] apertures, float[] maxShift, double strength)
        {
            this.source = source;
            output = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppPArgb);
            original = Read(source);
            pixels = new byte[original.Length];
            double sx = size.Width / (double)canvas.Width;
            double sy = size.Height / (double)canvas.Height;
            maxX = maxShift[0] * sx * strength;
            maxY = maxShift[1] * sy * strength;
            regions = new Region[centers.Length];
            for (int i = 0; i < regions.Length; i++)
                regions[i] = new Region(centers[i], apertures[i], sx, sy, crop);
        }

        private static byte[] Read(Bitmap image)
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

        private static double Clamp(double value, double low, double high)
        {
            return Math.Max(low, Math.Min(high, value));
        }

        private static double Smooth(double low, double high, double value)
        {
            double t = Clamp((value - low) / (high - low), 0, 1);
            return t * t * (3 - 2 * t);
        }

        internal Bitmap Render(double gazeX, double gazeY)
        {
            double dx = Math.Round(Clamp(gazeX, -1, 1) * maxX * 16) / 16;
            double dy = Math.Round(Clamp(gazeY, -1, 1) * maxY * 16) / 16;
            if (dx == 0 && dy == 0) return source;
            if (dx == previousX && dy == previousY) return output;
            previousX = dx;
            previousY = dy;
            Buffer.BlockCopy(original, 0, pixels, 0, original.Length);

            int width = source.Width, height = source.Height;
            foreach (Region eye in regions)
            {
                int left = Math.Max(1, (int)Math.Floor(eye.Left));
                int right = Math.Min(width - 2, (int)Math.Ceiling(eye.Right));
                int top = Math.Max(1, (int)Math.Floor(eye.Top));
                int bottom = Math.Min(height - 2, (int)Math.Ceiling(eye.Bottom));
                double turnX = dx;
                double turnY = dy;
                double xScale = 1 - 0.04 * Math.Abs(dx / maxX);
                double yScale = 1 - 0.02 * Math.Abs(dy / maxY);
                for (int y = top; y <= bottom; y++)
                    for (int x = left; x <= right; x++)
                    {
                        if (!eye.Contains(x, y)) continue;
                        int target = (y * width + x) * 4;
                        double fade = Smooth(0, Math.Min(1.5, maxX * 0.4),
                            eye.EdgeDistance(x, y));
                        if (fade <= 0) continue;
                        double sourceX = eye.X + (x - eye.X - turnX) / xScale;
                        double sourceY = eye.Y + (y - eye.Y - turnY) / yScale;
                        eye.ClampSample(ref sourceX, ref sourceY);
                        int x0 = (int)Math.Floor(sourceX), y0 = (int)Math.Floor(sourceY);
                        x0 = Math.Max(0, Math.Min(width - 2, x0));
                        y0 = Math.Max(0, Math.Min(height - 2, y0));
                        double fx = Clamp(sourceX - x0, 0, 1);
                        double fy = Clamp(sourceY - y0, 0, 1);
                        int a = (y0 * width + x0) * 4, b = a + 4;
                        int c = a + width * 4, d = c + 4;
                        for (int channel = 0; channel < 3; channel++)
                        {
                            double upper = original[a + channel] * (1 - fx) +
                                original[b + channel] * fx;
                            double lower = original[c + channel] * (1 - fx) +
                                original[d + channel] * fx;
                            double value = upper * (1 - fy) + lower * fy;
                            pixels[target + channel] = (byte)Math.Round(
                                original[target + channel] * (1 - fade) + value * fade);
                        }
                    }
            }

            BitmapData data = output.LockBits(new Rectangle(Point.Empty, output.Size),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                for (int y = 0; y < height; y++)
                    Marshal.Copy(pixels, y * width * 4, IntPtr.Add(data.Scan0, y * data.Stride),
                        width * 4);
            }
            finally { output.UnlockBits(data); }
            return output;
        }

        public void Dispose() { output.Dispose(); }
    }
}
