using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace MakiseDesktopPet
{
    internal sealed class SleepAnimation : IDisposable
    {
        private const int CycleMs = 5600;
        private const int FrameMs = 40;
        private readonly PetMotion breathing = new PetMotion();
        private readonly GraphicsPath letter = new GraphicsPath();
        private readonly float letterHeight;
        private Bitmap output;
        private Bitmap previousSource;
        private long previousFrame = -1;

        internal SleepAnimation()
        {
            using (FontFamily font = new FontFamily("Segoe UI"))
                letter.AddString("Z", font, (int)(FontStyle.Bold | FontStyle.Italic),
                    100, Point.Empty, StringFormat.GenericTypographic);
            RectangleF bounds = letter.GetBounds();
            letterHeight = bounds.Height;
            using (Matrix normalize = new Matrix())
            {
                normalize.Translate(-bounds.X, -bounds.Y);
                letter.Transform(normalize);
            }
        }

        private static double Smooth(double value)
        {
            value = Math.Max(0, Math.Min(1, value));
            return value * value * (3 - 2 * value);
        }

        internal Bitmap Render(Bitmap source, long elapsed, out bool changed)
        {
            long frame = Math.Max(0, elapsed) / FrameMs;
            if (output == null || output.Size != source.Size)
            {
                Reset();
                output = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppPArgb);
            }
            changed = previousSource != source || previousFrame != frame;
            if (!changed) return output;

            int time = (int)((frame % (CycleMs / FrameMs)) * FrameMs);
            bool bodyChanged;
            Bitmap body = breathing.Render(source, PetMotion.SleepBreath(time, source.Height), out bodyChanged);
            using (Graphics graphics = Graphics.FromImage(output))
            {
                graphics.PageUnit = GraphicsUnit.Pixel;
                graphics.PageScale = 1;
                graphics.Clear(Color.Transparent);
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.DrawImage(body, new Rectangle(Point.Empty, body.Size),
                    0, 0, body.Width, body.Height, GraphicsUnit.Pixel);
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                for (int i = 0; i < 3; i++)
                {
                    double age = time - i * 1000;
                    if (age < 0 || age >= 3200) continue;
                    double progress = age / 3200;
                    double opacity = Smooth(age / 400) * Smooth((3200 - age) / 850);
                    int alpha = (int)(255 * opacity);
                    if (alpha == 0) continue;
                    float size = (float)(source.Height * (0.032 + 0.043 * progress));
                    float x = (float)(source.Width * (0.82 + 0.13 * progress));
                    float y = (float)(source.Height * (0.26 - 0.255 * progress));
                    using (GraphicsPath glyph = (GraphicsPath)letter.Clone())
                    using (Matrix transform = new Matrix())
                    using (Pen outline = new Pen(Color.FromArgb(alpha, 255, 250, 239),
                        Math.Max(0.8f, 1.6f * source.Height / 340)))
                    using (SolidBrush ink = new SolidBrush(Color.FromArgb(alpha, 77, 76, 117)))
                    {
                        transform.Translate(x - size * 0.4f, y - size * 0.5f);
                        transform.Scale(size / letterHeight, size / letterHeight);
                        glyph.Transform(transform);
                        outline.LineJoin = LineJoin.Round;

                        // The preview had outer margins. Keep the fading glyphs inside
                        // the real transparent window without enlarging its hit area.
                        RectangleF bounds = glyph.GetBounds(null, outline);
                        float dx = Math.Max(0, 2 - bounds.Left) - Math.Max(0, bounds.Right - (source.Width - 2));
                        float dy = Math.Max(0, 2 - bounds.Top) - Math.Max(0, bounds.Bottom - (source.Height - 2));
                        if (dx != 0 || dy != 0)
                        {
                            using (Matrix shift = new Matrix())
                            {
                                shift.Translate(dx, dy);
                                glyph.Transform(shift);
                            }
                        }
                        graphics.DrawPath(outline, glyph);
                        graphics.FillPath(ink, glyph);
                    }
                }
            }
            previousSource = source;
            previousFrame = frame;
            return output;
        }

        internal void Reset()
        {
            breathing.Reset();
            if (output != null) output.Dispose();
            output = null;
            previousSource = null;
            previousFrame = -1;
        }

        public void Dispose()
        {
            Reset();
            letter.Dispose();
            breathing.Dispose();
        }
    }
}
