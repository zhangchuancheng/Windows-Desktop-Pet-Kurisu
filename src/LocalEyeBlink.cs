using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace MakiseDesktopPet
{
    internal sealed class LocalEyeBlink
    {
        public sealed class Spec
        {
            public double[][] eyeTransforms { get; set; }
            public int[][][] eyeSkinSamples { get; set; }
        }
        private readonly Bitmap source;
        private readonly Spec layout;
        internal LocalEyeBlink(Bitmap source, Spec layout)
        {
            this.source = source;
            this.layout = layout;
            if (layout == null) throw new InvalidDataException("Missing local eye layout.");
        }
        private static double ContourY(Point[] contour, int x)
        {
            for (int i = 1; i < contour.Length; i++)
                if (x <= contour[i].X)
                {
                    double t = (x - contour[i - 1].X) / (double)(contour[i].X - contour[i - 1].X);
                    return contour[i - 1].Y + t * (contour[i].Y - contour[i - 1].Y);
                }
            return contour[contour.Length - 1].Y;
        }

        internal Bitmap Create(double opening)
        {
            Bitmap overlay = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            // Include the complete lashes and corners of the source drawing.
            Point[][] tops = {
                new[] { new Point(338,449), new Point(361,443), new Point(400,440),
                    new Point(438,441), new Point(466,451), new Point(485,473) },
                new[] { new Point(568,449), new Point(585,430), new Point(619,416),
                    new Point(660,405), new Point(690,401), new Point(714,405) }
            };
            Point[][] bottoms = {
                new[] { new Point(338,451), new Point(361,490), new Point(400,522),
                    new Point(438,522), new Point(466,509), new Point(485,473) },
                new[] { new Point(568,449), new Point(585,483), new Point(619,501),
                    new Point(660,498), new Point(690,473), new Point(714,405) }
            };
            for (int eye = 0; eye < tops.Length; eye++)
            {
                for (int i = 0; i < tops[eye].Length; i++)
                {
                    tops[eye][i] = EyePoint(eye, tops[eye][i].X, tops[eye][i].Y);
                    bottoms[eye][i] = EyePoint(eye, bottoms[eye][i].X, bottoms[eye][i].Y);
                }
                Point[] top = tops[eye], bottom = bottoms[eye];
                Point skinPoint = EyePoint(eye, eye == 0 ? 435 : 645, eye == 0 ? 430 : 397);
                Point cheekPoint = EyePoint(eye, eye == 0 ? 435 : 645, eye == 0 ? 535 : 515);
                if (layout.eyeSkinSamples != null)
                {
                    int[][] samples = layout.eyeSkinSamples[eye];
                    skinPoint = new Point(samples[0][0], samples[0][1]);
                    cheekPoint = new Point(samples[1][0], samples[1][1]);
                }
                Color fallback = source.GetPixel(skinPoint.X, skinPoint.Y);
                Color cheek = source.GetPixel(cheekPoint.X, cheekPoint.Y);
                for (int x = top[0].X; x <= top[top.Length - 1].X; x++)
                {
                    double upper = ContourY(top, x), lower = ContourY(bottom, x);
                    double closedLine = upper + (lower - upper) * 0.78;
                    double newTop = closedLine + (upper - closedLine) * opening;
                    double newBottom = closedLine + (lower - closedLine) * opening;
                    for (int y = (int)upper - 2; y <= (int)Math.Ceiling(lower) + 2; y++)
                    {
                        double blend = Math.Max(0, Math.Min(1, (y - upper) / Math.Max(1, lower - upper)));
                        Color color = Color.FromArgb(
                            (int)(fallback.R + (cheek.R - fallback.R) * blend),
                            (int)(fallback.G + (cheek.G - fallback.G) * blend),
                            (int)(fallback.B + (cheek.B - fallback.B) * blend));
                        if (opening > 0.1 && y >= newTop && y <= newBottom)
                        {
                            int sampleY = (int)Math.Round(closedLine + (y - closedLine) / opening);
                            color = source.GetPixel(x, Math.Max(0, Math.Min(source.Height - 1, sampleY)));
                        }
                        double edge = Math.Min(y - (upper - 3), lower + 3 - y);
                        int alpha = (int)(255 * SmoothStep(0, 2, edge));
                        overlay.SetPixel(x, y, Color.FromArgb(alpha, color.R, color.G, color.B));
                    }
                }
            }
            if (opening <= 0.1)
                using (Graphics g = Graphics.FromImage(overlay))
                using (Pen lashes = new Pen(Color.FromArgb(35, 32, 39), 7))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    lashes.StartCap = lashes.EndCap = LineCap.Round;
                    g.DrawBezier(lashes, EyePoint(0,349,485), EyePoint(0,378,509),
                        EyePoint(0,442,512), EyePoint(0,477,487));
                    g.DrawBezier(lashes, EyePoint(1,577,465), EyePoint(1,614,491),
                        EyePoint(1,666,483), EyePoint(1,706,444));
                }
            return overlay;
        }

        private Point EyePoint(int eye, int x, int y)
        {
            if (layout.eyeTransforms == null) return new Point(x, y);
            double[] transform = layout.eyeTransforms[eye];
            return new Point((int)Math.Round(x * transform[0] + transform[2]),
                (int)Math.Round(y * transform[1] + transform[3]));
        }

        private static double SmoothStep(double low, double high, double value)
        {
            double t = Math.Max(0, Math.Min(1, (value - low) / (high - low)));
            return t * t * (3 - 2 * t);
        }
    }
}