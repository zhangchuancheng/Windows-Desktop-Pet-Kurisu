using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MakiseDesktopPet
{
    // Translate the painted iris beneath a fixed eyelid. The manifest bounds
    // the eye search so adjacent hair and brows stay intact.
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

            internal bool SearchContains(double x, double y)
            {
                double width = Right - Left;
                double sampleX = Math.Max(Left, Math.Min(Right, x));
                return x >= Left - width * 0.1 && x <= Right + width * 0.1 &&
                    y >= HeightAt(upper, sampleX) - width * 0.04 &&
                    y <= HeightAt(lower, sampleX) + width * 0.16;
            }

            internal double UpperAt(double x)
            { return HeightAt(upper, Math.Max(Left, Math.Min(Right, x))); }

        }

        private readonly Bitmap source, output;
        private readonly byte[] original, pixels;
        private readonly Region[] regions;
        private readonly IrisSurface[] irises;
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
            irises = new IrisSurface[centers.Length];
            for (int i = 0; i < regions.Length; i++)
            {
                regions[i] = new Region(centers[i], apertures[i], sx, sy, crop);
                irises[i] = new IrisSurface(original, source.Width, source.Height, regions[i], maxX, maxY);
            }
        }

        internal static byte[] Read(Bitmap image)
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

        internal Bitmap Render(double gazeX, double gazeY)
        {
            double dx = Math.Round(Clamp(gazeX, -1, 1) * maxX * 16) / 16;
            double dy = Math.Round(Clamp(gazeY, -1, 1) * maxY * 16) / 16;
            // Center uses the same composite as moving frames; switching back
            // to the unsplit source here would cause a visible pop at zero.
            if (dx == previousX && dy == previousY) return output;
            previousX = dx;
            previousY = dy;
            Buffer.BlockCopy(original, 0, pixels, 0, original.Length);

            int width = source.Width, height = source.Height;
            for (int eyeIndex = 0; eyeIndex < regions.Length; eyeIndex++)
            {
                IrisSurface iris = irises[eyeIndex];
                int left = Math.Max(1, iris.Bounds.Left);
                int right = Math.Min(width - 2, iris.Bounds.Right - 1);
                int top = Math.Max(1, iris.Bounds.Top);
                int bottom = Math.Min(height - 2, iris.Bounds.Bottom - 1);
                for (int y = top; y <= bottom; y++)
                    for (int x = left; x <= right; x++)
                    {
                        double coverage = iris.Coverage(x, y);
                        if (coverage <= 0) continue;
                        int target = (y * width + x) * 4;
                        double backing = iris.Background(x, y) *
                            (1 - iris.Sample(x - dx, y - dy, 3));
                        for (int channel = 0; channel < 3; channel++)
                        {
                            double sample = iris.Sample(x - dx, y - dy, channel) + backing;
                            pixels[target + channel] = (byte)Math.Round(Clamp(
                                original[target + channel] * (1 - coverage) + coverage * sample, 0, original[target + 3]));
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
