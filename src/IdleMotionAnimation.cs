using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

namespace MakiseDesktopPet
{
    // A single complete drawing is deformed gently, so the new flat artwork
    // cannot expose seams between separately cut head, hair, and body pieces.
    internal sealed class IdleMotionAnimation : IDisposable
    {
        public sealed class GazeSpec
        {
            public float[][] eyeCenters { get; set; }
            public float[][][][] eyeApertures { get; set; }
            public float[] maxShift { get; set; }
            public float[][] halfEyeCenters { get; set; }
            public float[][][][] halfEyeApertures { get; set; }
        }

        public sealed class Manifest
        {
            public int[] canvas { get; set; }
            public GazeSpec gaze { get; set; }
        }

        private readonly string directory;
        private readonly bool enableGaze;
        private readonly bool useLocalBlink;
        private readonly Manifest manifest;
        private readonly Bitmap[] frames = new Bitmap[3];
        private readonly Bitmap[] localBlinkOriginals = new Bitmap[2];
        private readonly EyeGazeLayer[] gazeFrames = new EyeGazeLayer[2];
        private readonly Bitmap[] gazeOriginals = new Bitmap[2];
        private readonly Bitmap[] gazeScaled = new Bitmap[2];
        private readonly Rectangle[] gazeCrops = new Rectangle[2];
        private readonly double[] scaledEyeX = { double.NaN, double.NaN };
        private readonly double[] scaledEyeY = { double.NaN, double.NaN };
        private Bitmap output;
        private byte[] sourcePixels, pixels;
        private long activeSince, lastFrame = -1000;
        private int lastEyes = -1;
        private double lastEyeX = double.NaN, lastEyeY = double.NaN;
        private double lastFaceX = double.NaN, lastFaceY = double.NaN;

        internal int Width { get; private set; }
        internal int Height { get; private set; }

        internal IdleMotionAnimation(string directory, int height)
            : this(directory, directory, height, true)
        {
        }

        internal IdleMotionAnimation(string directory, string manifestDirectory,
            int height, bool enableGaze, bool useLocalBlink = false)
        {
            this.directory = directory;
            this.enableGaze = enableGaze;
            this.useLocalBlink = useLocalBlink || !enableGaze;
            manifest = new JavaScriptSerializer().Deserialize<Manifest>(File.ReadAllText(
                Path.Combine(manifestDirectory, "idle-motion.json")));
            if (manifest == null || manifest.canvas == null || manifest.canvas.Length != 2 ||
                manifest.canvas[0] < 32 || manifest.canvas[1] < 32 || manifest.gaze == null ||
                manifest.gaze.eyeCenters == null ||
                manifest.gaze.eyeCenters.Length != 2 ||
                manifest.gaze.eyeApertures == null || manifest.gaze.eyeApertures.Length != 2 ||
                manifest.gaze.maxShift == null || manifest.gaze.maxShift.Length != 2)
                throw new InvalidDataException("Invalid idle motion manifest.");
            for (int i = 0; i < 2; i++)
                if (manifest.gaze.eyeCenters[i] == null || manifest.gaze.eyeCenters[i].Length != 2 ||
                    manifest.gaze.eyeApertures[i] == null ||
                    manifest.gaze.eyeApertures[i].Length != 2 ||
                    manifest.gaze.eyeApertures[i][0] == null ||
                    manifest.gaze.eyeApertures[i][1] == null ||
                    manifest.gaze.eyeApertures[i][0].Length < 2 ||
                    manifest.gaze.eyeApertures[i][1].Length < 2)
                    throw new InvalidDataException("Invalid idle eye region.");
            for (int i = 0; i < 2; i++)
                for (int contour = 0; contour < 2; contour++)
                {
                    float[][] points = manifest.gaze.eyeApertures[i][contour];
                    for (int point = 0; point < points.Length; point++)
                        if (points[point] == null || points[point].Length != 2 ||
                            (point > 0 && points[point][0] <= points[point - 1][0]))
                            throw new InvalidDataException("Invalid idle eye aperture.");
                }
            if (manifest.gaze.maxShift[0] <= 0 || manifest.gaze.maxShift[1] <= 0)
                throw new InvalidDataException("Invalid idle gaze range.");
            if (enableGaze && (this.useLocalBlink ||
                manifest.gaze.halfEyeCenters != null || manifest.gaze.halfEyeApertures != null))
            {
                if (manifest.gaze.halfEyeCenters == null || manifest.gaze.halfEyeCenters.Length != 2 ||
                    manifest.gaze.halfEyeApertures == null || manifest.gaze.halfEyeApertures.Length != 2)
                    throw new InvalidDataException("Missing local half-blink gaze layout.");
                for (int eye = 0; eye < 2; eye++)
                {
                    if (manifest.gaze.halfEyeCenters[eye] == null || manifest.gaze.halfEyeCenters[eye].Length != 2 ||
                        manifest.gaze.halfEyeApertures[eye] == null || manifest.gaze.halfEyeApertures[eye].Length != 2)
                        throw new InvalidDataException("Invalid half-blink eye region.");
                    foreach (float[][] contour in manifest.gaze.halfEyeApertures[eye])
                    {
                        if (contour == null || contour.Length < 2)
                            throw new InvalidDataException("Invalid half-blink contour.");
                        for (int i = 0; i < contour.Length; i++)
                            if (contour[i] == null || contour[i].Length != 2 ||
                                (i > 0 && contour[i][0] <= contour[i - 1][0]))
                                throw new InvalidDataException("Invalid half-blink contour point.");
                    }
                }
            }
            try { Resize(height); }
            catch { Dispose(); throw; }
        }

        private Bitmap LoadScaled(string filename)
        {
            using (Bitmap original = new Bitmap(Path.Combine(directory, filename)))
                return Scale(original);
        }

        private Bitmap Scale(Bitmap original)
        {
            if (original.Width != manifest.canvas[0] || original.Height != manifest.canvas[1])
                throw new InvalidDataException("Idle canvas mismatch in " + directory);
            Bitmap scaled = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(scaled))
            {
                g.PageUnit = GraphicsUnit.Pixel;
                g.CompositingMode = CompositingMode.SourceCopy;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(original, new Rectangle(0, 0, Width, Height),
                    0, 0, original.Width, original.Height, GraphicsUnit.Pixel);
            }
            return scaled;
        }

        private static Bitmap CopyNative(Bitmap original, Rectangle crop)
        {
            Bitmap copy = new Bitmap(crop.Width, crop.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(copy))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImageUnscaled(original, -crop.Left, -crop.Top);
            }
            return copy;
        }

        private Rectangle GazeCrop(float[][][][] apertures)
        {
            float left=manifest.canvas[0], top=manifest.canvas[1], right=0, bottom=0;
            foreach (float[][][] aperture in apertures)
                foreach (float[][] contour in aperture)
                    foreach (float[] point in contour)
                    {
                        left=Math.Min(left,point[0]); top=Math.Min(top,point[1]);
                        right=Math.Max(right,point[0]); bottom=Math.Max(bottom,point[1]);
                    }
            int margin=(int)Math.Ceiling(manifest.canvas[0]*0.05);
            return Rectangle.Intersect(new Rectangle(0,0,manifest.canvas[0],manifest.canvas[1]),
                Rectangle.FromLTRB((int)Math.Floor(left)-margin,(int)Math.Floor(top)-margin,
                    (int)Math.Ceiling(right)+margin,(int)Math.Ceiling(bottom)+margin));
        }

        private Bitmap RenderGaze(int eyes, double x, double y)
        {
            if (x == scaledEyeX[eyes] && y == scaledEyeY[eyes]) return gazeScaled[eyes];
            Bitmap native = gazeFrames[eyes].Render(x, y);
            using (Graphics g = Graphics.FromImage(gazeScaled[eyes]))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImageUnscaled(frames[eyes],0,0);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                Rectangle crop=gazeCrops[eyes];
                float sx=Width/(float)manifest.canvas[0], sy=Height/(float)manifest.canvas[1];
                // Discard crop-border filter pixels. The extra source padding
                // keeps the eye resampling aligned with the complete drawing.
                g.SetClip(new RectangleF((crop.Left+8)*sx,(crop.Top+8)*sy,
                    (crop.Width-16)*sx,(crop.Height-16)*sy));
                g.DrawImage(native, new RectangleF(crop.Left*sx,crop.Top*sy,crop.Width*sx,crop.Height*sy),
                    new RectangleF(0,0,native.Width,native.Height), GraphicsUnit.Pixel);
            }
            scaledEyeX[eyes] = x;
            scaledEyeY[eyes] = y;
            return gazeScaled[eyes];
        }

        private Bitmap LoadLocalBlink(double opening, int index)
        {
            if (localBlinkOriginals[index] != null) return Scale(localBlinkOriginals[index]);
            LocalEyeBlink.Spec spec = new JavaScriptSerializer().Deserialize<LocalEyeBlink.Spec>(
                File.ReadAllText(Path.Combine(directory, "idle-blink.json")));
            using (Bitmap original = new Bitmap(Path.Combine(directory, "idle.png")))
            using (Bitmap overlay = new LocalEyeBlink(original, spec).Create(opening))
            {
                // Touch only eye pixels. Drawing onto the entire PNG through GDI+
                // can round translucent hair-edge colors even outside the overlay.
                for (int y = 0; y < original.Height; y++)
                    for (int x = 0; x < original.Width; x++)
                    {
                        Color eye = overlay.GetPixel(x, y);
                        if (eye.A == 0) continue;
                        Color face = original.GetPixel(x, y);
                        double alpha = eye.A / 255.0;
                        original.SetPixel(x, y, Color.FromArgb(face.A,
                            ClampByte(eye.R * alpha + face.R * (1 - alpha)),
                            ClampByte(eye.G * alpha + face.G * (1 - alpha)),
                            ClampByte(eye.B * alpha + face.B * (1 - alpha))));
                    }
                localBlinkOriginals[index] = original.Clone(
                    new Rectangle(Point.Empty, original.Size), PixelFormat.Format32bppArgb);
            }
            return Scale(localBlinkOriginals[index]);
        }

        internal void Resize(int height)
        {
            ReleaseFrames();
            Height = height;
            Width = Math.Max(1, (int)Math.Round(height * (double)manifest.canvas[0] / manifest.canvas[1]));
            frames[0] = LoadScaled("idle.png");
            frames[1] = useLocalBlink ? LoadLocalBlink(0.48, 0) : LoadScaled("blink-half.png");
            frames[2] = useLocalBlink ? LoadLocalBlink(0.08, 1) : LoadScaled("blink.png");
            Size canvas = new Size(manifest.canvas[0], manifest.canvas[1]);
            if (enableGaze)
                for (int i = 0; i < gazeFrames.Length; i++)
                {
                    float[][] centers=i == 1 && manifest.gaze.halfEyeCenters != null
                        ? manifest.gaze.halfEyeCenters : manifest.gaze.eyeCenters;
                    float[][][][] apertures=i == 1 && manifest.gaze.halfEyeApertures != null
                        ? manifest.gaze.halfEyeApertures : manifest.gaze.eyeApertures;
                    gazeCrops[i]=GazeCrop(apertures);
                    if (i == 1 && useLocalBlink)
                        gazeOriginals[i] = CopyNative(localBlinkOriginals[0],gazeCrops[i]);
                    else
                        using (Bitmap native = new Bitmap(Path.Combine(directory,
                            i == 0 ? "idle.png" : "blink-half.png")))
                            gazeOriginals[i] = CopyNative(native,gazeCrops[i]);
                    gazeScaled[i] = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
                    gazeFrames[i] = new EyeGazeLayer(gazeOriginals[i], gazeCrops[i], canvas, canvas,
                        centers, apertures, manifest.gaze.maxShift, i == 0 ? 1 : 0.5);
                }
            output = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
            sourcePixels = new byte[Width * Height * 4];
            pixels = new byte[sourcePixels.Length];
        }

        internal void Enter(long now) { activeSince = now; lastEyes = -1; }

        internal static int EyeForBlink(long now, long start, long end)
        {
            if (now < start || now >= end) return 0;
            return now - start < 45 || end - now <= 50 ? 1 : 2;
        }

        private static double Smooth(double low, double high, double value)
        {
            double t = Math.Max(0, Math.Min(1, (value - low) / (high - low)));
            return t * t * (3 - 2 * t);
        }

        private static byte ClampByte(double value)
        {
            return (byte)Math.Max(0, Math.Min(255, (int)Math.Round(value)));
        }

        private void ReadPixels(Bitmap image)
        {
            BitmapData data = image.LockBits(new Rectangle(Point.Empty, image.Size),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                int stride = Width * 4;
                for (int y = 0; y < Height; y++)
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), sourcePixels,
                        y * stride, stride);
            }
            finally { image.UnlockBits(data); }
        }

        private void WritePixels()
        {
            BitmapData data = output.LockBits(new Rectangle(Point.Empty, output.Size),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                int stride = Width * 4;
                for (int y = 0; y < Height; y++)
                    Marshal.Copy(pixels, y * stride, IntPtr.Add(data.Scan0, y * data.Stride), stride);
            }
            finally { output.UnlockBits(data); }
        }

        internal Bitmap Render(long now, int eyes, out bool changed)
        {
            return Render(now, eyes, 0, 0, 0, 0, out changed);
        }

        internal Bitmap Render(long now, int eyes, double eyeX, double eyeY,
            double faceX, double faceY, out bool changed)
        {
            if (eyes < 0 || eyes > 2) throw new ArgumentOutOfRangeException("eyes");
            changed = eyes != lastEyes || now - lastFrame >= 16 || now < lastFrame ||
                eyeX != lastEyeX || eyeY != lastEyeY ||
                faceX != lastFaceX || faceY != lastFaceY;
            if (!changed) return output;
            lastFrame = now; lastEyes = eyes;
            lastEyeX = eyeX; lastEyeY = eyeY;
            lastFaceX = faceX; lastFaceY = faceY;

            Bitmap source = enableGaze && eyes < 2
                ? RenderGaze(eyes, eyeX, eyeY) : frames[eyes];
            ReadPixels(source);
            int stride = Width * 4, anchor = (int)Math.Floor(Height * 0.88);
            double breath = PetMotion.Breath(now, Height);
            double gain = Smooth(0, 240, now - activeSince);
            double angle = Math.Sin(now * Math.PI * 2 / 6200.0) * 0.38 * Math.PI / 180.0 * gain;
            double faceDx = faceX * Math.Min(2.8, Height / 120.0);
            double faceDy = faceY * Math.Min(1.9, Height * 0.006);
            double hairLag = Math.Sin((now - 260) * Math.PI * 2 / 6200.0) *
                Math.Min(0.65, Height * 0.002) * gain;

            for (int y = 0; y < Height; y++)
            {
                int row = y * stride;
                if (y >= anchor)
                {
                    Buffer.BlockCopy(sourcePixels, row, pixels, row, stride);
                    continue;
                }
                double yn = y / (double)Height;
                double headWeight = 1 - Smooth(0.31, 0.58, yn);
                double hairWeight = Smooth(0.25, 0.42, yn) * (1 - Smooth(0.66, 0.82, yn));
                double dy = breath * (anchor - y) / anchor + faceDy * headWeight;
                double sy = Math.Max(0, Math.Min(Height - 1.001, y - dy));
                int y0 = (int)sy;
                double ty = sy - y0, ty2 = ty * ty, ty3 = ty2 * ty;
                double wy0 = -0.5 * ty + ty2 - 0.5 * ty3;
                double wy1 = 1 - 2.5 * ty2 + 1.5 * ty3;
                double wy2 = 0.5 * ty + 2 * ty2 - 1.5 * ty3;
                double wy3 = -0.5 * ty2 + 0.5 * ty3;
                int r0 = Math.Max(0, y0 - 1) * stride;
                int r1 = y0 * stride;
                int r2 = Math.Min(Height - 1, y0 + 1) * stride;
                int r3 = Math.Min(Height - 1, y0 + 2) * stride;
                for (int x = 0; x < Width; x++)
                {
                    double xn = x / (double)Width;
                    double sides = Math.Max(0, 1 - Smooth(0.24, 0.42, xn)) +
                        Smooth(0.58, 0.76, xn);
                    double dx = (faceDx + angle * (Height * 0.45 - y)) * headWeight +
                        hairLag * hairWeight * sides;
                    double sx = Math.Max(0, Math.Min(Width - 1.001, x - dx));
                    int x0 = (int)sx;
                    double tx = sx - x0;
                    int target = row + x * 4;
                    if (tx < 0.000001 && ty < 0.000001)
                    {
                        Buffer.BlockCopy(sourcePixels, r1 + x0 * 4, pixels, target, 4);
                        continue;
                    }
                    double tx2 = tx * tx, tx3 = tx2 * tx;
                    double wx0 = -0.5 * tx + tx2 - 0.5 * tx3;
                    double wx1 = 1 - 2.5 * tx2 + 1.5 * tx3;
                    double wx2 = 0.5 * tx + 2 * tx2 - 1.5 * tx3;
                    double wx3 = -0.5 * tx2 + 0.5 * tx3;
                    int c0 = Math.Max(0, x0 - 1) * 4;
                    int c1 = x0 * 4;
                    int c2 = Math.Min(Width - 1, x0 + 1) * 4;
                    int c3 = Math.Min(Width - 1, x0 + 2) * 4;
                    for (int channel = 0; channel < 4; channel++)
                    {
                        double a = sourcePixels[r0 + c0 + channel] * wx0 +
                            sourcePixels[r0 + c1 + channel] * wx1 +
                            sourcePixels[r0 + c2 + channel] * wx2 +
                            sourcePixels[r0 + c3 + channel] * wx3;
                        double b = sourcePixels[r1 + c0 + channel] * wx0 +
                            sourcePixels[r1 + c1 + channel] * wx1 +
                            sourcePixels[r1 + c2 + channel] * wx2 +
                            sourcePixels[r1 + c3 + channel] * wx3;
                        double c = sourcePixels[r2 + c0 + channel] * wx0 +
                            sourcePixels[r2 + c1 + channel] * wx1 +
                            sourcePixels[r2 + c2 + channel] * wx2 +
                            sourcePixels[r2 + c3 + channel] * wx3;
                        double d = sourcePixels[r3 + c0 + channel] * wx0 +
                            sourcePixels[r3 + c1 + channel] * wx1 +
                            sourcePixels[r3 + c2 + channel] * wx2 +
                            sourcePixels[r3 + c3 + channel] * wx3;
                        pixels[target + channel] = ClampByte(a * wy0 + b * wy1 +
                            c * wy2 + d * wy3);
                    }
                    // Catmull-Rom can overshoot at transparent boundaries. Keep
                    // the premultiplied bitmap valid without changing the pose.
                    byte alpha = pixels[target + 3];
                    for (int channel = 0; channel < 3; channel++)
                        if (pixels[target + channel] > alpha) pixels[target + channel] = alpha;
                }
            }
            WritePixels();
            return output;
        }

        private void ReleaseFrames()
        {
            for (int i = 0; i < gazeFrames.Length; i++)
            {
                if (gazeFrames[i] != null) gazeFrames[i].Dispose(); gazeFrames[i] = null;
                if (gazeOriginals[i] != null) gazeOriginals[i].Dispose(); gazeOriginals[i] = null;
                if (gazeScaled[i] != null) gazeScaled[i].Dispose(); gazeScaled[i] = null;
                scaledEyeX[i] = scaledEyeY[i] = double.NaN;
            }
            for (int i = 0; i < frames.Length; i++)
            { if (frames[i] != null) frames[i].Dispose(); frames[i] = null; }
            if (output != null) output.Dispose();
            output = null; sourcePixels = pixels = null;
            lastFrame = -1000; lastEyes = -1;
            lastEyeX = lastEyeY = lastFaceX = lastFaceY = double.NaN;
        }

        public void Dispose()
        {
            ReleaseFrames();
            for (int i = 0; i < localBlinkOriginals.Length; i++)
            {
                if (localBlinkOriginals[i] != null) localBlinkOriginals[i].Dispose();
                localBlinkOriginals[i] = null;
            }
        }
    }
}
