using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MakiseDesktopPet
{
    // Prototype motion for flat artwork. The bottom 12% (including shoes) is
    // copied unchanged; only the area above it is gently compressed vertically.
    internal sealed class PetMotion : IDisposable
    {
        internal const int RecoilDurationMs = 250;
        private readonly Dictionary<Bitmap, byte[]> sources = new Dictionary<Bitmap, byte[]>();
        private Bitmap output;
        private Bitmap previousSource;
        private byte[] pixels;
        private int previousStep = int.MinValue;
        private long recoilStart = -1;

        internal void StartRecoil(long now)
        {
            // Keep an active pulse continuous even when the user clicks rapidly.
            if (recoilStart < 0 || now - recoilStart >= RecoilDurationMs)
                recoilStart = now;
        }

        internal void CancelRecoil() { recoilStart = -1; }

        internal static double Breath(long now, int height)
        {
            double amplitude = Math.Min(2.0, 1.5 * height / 220.0);
            return amplitude * (0.5 - 0.5 * Math.Cos(now * Math.PI * 2 / 4200.0));
        }

        internal double Displacement(long now, int height)
        {
            double recoil = 0;
            if (recoilStart >= 0)
            {
                double elapsed = now - recoilStart;
                if (elapsed >= 0 && elapsed < RecoilDurationMs)
                {
                    // Fast retreat (65 ms), soft rebound, then settle to zero.
                    double[] times = { 0, 65, 175, RecoilDurationMs };
                    double[] values = { 0, 2.4, -0.35, 0 };
                    int segment = elapsed < times[1] ? 0 : elapsed < times[2] ? 1 : 2;
                    double t = (elapsed - times[segment]) / (times[segment + 1] - times[segment]);
                    t = t * t * (3 - 2 * t);
                    recoil = (values[segment] + (values[segment + 1] - values[segment]) * t)
                        * Math.Min(1.4, height / 220.0);
                }
            }
            return Breath(now, height) + recoil;
        }

        internal Bitmap Render(Bitmap source, double displacement, out bool changed)
        {
            int width = source.Width, height = source.Height;
            if (output == null || output.Width != width || output.Height != height)
            {
                Reset();
                output = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppPArgb);
                pixels = new byte[source.Width * source.Height * 4];
            }
            int step = (int)Math.Round(displacement * 16);
            changed = source != previousSource || step != previousStep;
            if (!changed) return output;

            byte[] original;
            int stride = width * 4;
            Rectangle area = new Rectangle(Point.Empty, source.Size);
            if (!sources.TryGetValue(source, out original))
            {
                original = new byte[pixels.Length];
                BitmapData data = source.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                try
                {
                    for (int y = 0; y < source.Height; y++)
                        Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), original, y * stride, stride);
                }
                finally { source.UnlockBits(data); }
                sources.Add(source, original);
            }

            int anchor = (int)Math.Floor(height * 0.88);
            double scale = 1 - step / 16.0 / anchor;
            for (int y = 0; y < anchor; y++)
            {
                double sample = anchor + (y - anchor) / scale;
                int row = (int)Math.Floor(sample);
                int weight = (int)Math.Round((sample - row) * 256);
                int destination = y * stride;
                bool validA = row >= 0 && row < height;
                bool validB = row + 1 >= 0 && row + 1 < height;
                int firstRow = row * stride, secondRow = (row + 1) * stride;
                for (int x = 0; x < stride; x++)
                {
                    int a = validA ? original[firstRow + x] : 0;
                    int b = validB ? original[secondRow + x] : 0;
                    pixels[destination + x] = (byte)((a * (256 - weight) + b * weight + 128) >> 8);
                }
            }
            // Exact copy avoids shoe movement, edge shimmer and color changes here.
            Buffer.BlockCopy(original, anchor * stride, pixels, anchor * stride,
                pixels.Length - anchor * stride);
            BitmapData target = output.LockBits(area, ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                for (int y = 0; y < source.Height; y++)
                    Marshal.Copy(pixels, y * stride, IntPtr.Add(target.Scan0, y * target.Stride), stride);
            }
            finally { output.UnlockBits(target); }
            previousSource = source;
            previousStep = step;
            return output;
        }

        internal void Reset()
        {
            if (output != null) output.Dispose();
            output = null;
            previousSource = null;
            pixels = null;
            previousStep = int.MinValue;
            sources.Clear();
        }

        public void Dispose() { Reset(); }
    }
}
