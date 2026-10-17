using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MakiseDesktopPet
{
    // A small inverse texture warp moves each iris inside its original eye.
    // The eyelid and eye outline remain anchored because displacement fades to
    // zero at the edge. Every frame samples the same source, so no ghost trail
    // or cumulative color change can build up.
    internal sealed class EyeGazeLayer : IDisposable
    {
        internal sealed class Region
        {
            internal readonly double X, Y, RadiusX, RadiusY;
            internal Region(float[] center, float[] radii, double scaleX, double scaleY, Rectangle crop)
            {
                X = center[0] * scaleX - crop.Left;
                Y = center[1] * scaleY - crop.Top;
                RadiusX = radii[0] * scaleX;
                RadiusY = radii[1] * scaleY;
            }
        }

        private readonly Bitmap source;
        private readonly Bitmap output;
        private readonly byte[] original;
        private readonly byte[] pixels;
        private readonly Region[] regions;
        private readonly double maxX, maxY;
        private double previousX = double.NaN, previousY = double.NaN;

        internal EyeGazeLayer(Bitmap source, Rectangle crop, Size canvas, Size size,
            float[][] centers, float[][] radii, float[] maxShift, double strength)
        {
            this.source = source;
            output = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppPArgb);
            original = Read(source);
            pixels = new byte[original.Length];
            double scaleX = size.Width / (double)canvas.Width;
            double scaleY = size.Height / (double)canvas.Height;
            maxX = maxShift[0] * scaleX * strength;
            maxY = maxShift[1] * scaleY * strength;
            regions = new Region[centers.Length];
            for (int i = 0; i < regions.Length; i++)
                regions[i] = new Region(centers[i], radii[i], scaleX, scaleY, crop);
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

        internal Bitmap Render(double gazeX, double gazeY)
        {
            double dx = Math.Round(Math.Max(-1, Math.Min(1, gazeX)) * maxX * 16) / 16;
            double dy = Math.Round(Math.Max(-1, Math.Min(1, gazeY)) * maxY * 16) / 16;
            if (dx == 0 && dy == 0) return source;
            if (dx == previousX && dy == previousY) return output;
            previousX = dx;
            previousY = dy;
            Buffer.BlockCopy(original, 0, pixels, 0, original.Length);

            int width = source.Width, height = source.Height, stride = width * 4;
            foreach (Region eye in regions)
            {
                int left = Math.Max(1, (int)Math.Floor(eye.X - eye.RadiusX));
                int right = Math.Min(width - 2, (int)Math.Ceiling(eye.X + eye.RadiusX));
                int top = Math.Max(1, (int)Math.Floor(eye.Y - eye.RadiusY));
                int bottom = Math.Min(height - 2, (int)Math.Ceiling(eye.Y + eye.RadiusY));
                for (int y = top; y <= bottom; y++)
                    for (int x = left; x <= right; x++)
                    {
                        double ux = (x - eye.X) / eye.RadiusX;
                        double uy = (y - eye.Y) / eye.RadiusY;
                        double radiusSquared = ux * ux + uy * uy;
                        if (radiusSquared >= 1) continue;
                        double fade = Math.Max(0, Math.Min(1, (1 - radiusSquared) / 0.75));
                        fade = fade * fade * (3 - 2 * fade);
                        double sampleX = x - dx * fade;
                        double sampleY = y - dy * fade;
                        int x0 = (int)Math.Floor(sampleX), y0 = (int)Math.Floor(sampleY);
                        x0 = Math.Max(0, Math.Min(width - 2, x0));
                        y0 = Math.Max(0, Math.Min(height - 2, y0));
                        double fx = Math.Max(0, Math.Min(1, sampleX - x0));
                        double fy = Math.Max(0, Math.Min(1, sampleY - y0));
                        int a = y0 * stride + x0 * 4;
                        int b = a + 4;
                        int c = a + stride;
                        int d = c + 4;
                        int target = y * stride + x * 4;
                        for (int channel = 0; channel < 4; channel++)
                        {
                            double upper = original[a + channel] * (1 - fx) + original[b + channel] * fx;
                            double lower = original[c + channel] * (1 - fx) + original[d + channel] * fx;
                            pixels[target + channel] = (byte)Math.Round(upper * (1 - fy) + lower * fy);
                        }
                    }
            }

            BitmapData data = output.LockBits(new Rectangle(Point.Empty, output.Size),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                for (int y = 0; y < height; y++)
                    Marshal.Copy(pixels, y * stride, IntPtr.Add(data.Scan0, y * data.Stride), stride);
            }
            finally { output.UnlockBits(data); }
            return output;
        }

        public void Dispose() { output.Dispose(); }
    }
}
