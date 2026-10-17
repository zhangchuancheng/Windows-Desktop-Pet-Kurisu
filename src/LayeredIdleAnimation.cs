using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

namespace MakiseDesktopPet
{
    internal sealed class LayeredIdleAnimation : IDisposable
    {
        private sealed class Part : IDisposable
        {
            internal readonly Bitmap Image;
            internal readonly Rectangle Bounds;
            internal Part(Bitmap full)
            {
                try
                {
                    int width = full.Width, height = full.Height;
                    int left = width, top = height, right = -1, bottom = -1;
                    BitmapData data = full.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                    try
                    {
                        byte[] row = new byte[width * 4];
                        for (int y = 0; y < height; y++)
                        {
                            Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                            for (int x = 0; x < width; x++)
                                if (row[x*4+3] != 0)
                                {
                                    left = Math.Min(left, x); right = Math.Max(right, x);
                                    top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                                }
                        }
                    }
                    finally { full.UnlockBits(data); }
                    if (right < left) throw new InvalidDataException("Empty idle animation layer.");
                    Bounds = Rectangle.FromLTRB(Math.Max(0,left-2),Math.Max(0,top-2),Math.Min(width,right+3),Math.Min(height,bottom+3));
                    Image = full.Clone(Bounds, PixelFormat.Format32bppPArgb);
                }
                finally { full.Dispose(); }
            }
            public void Dispose() { Image.Dispose(); }
        }
        public sealed class CanvasSpec { public int width { get; set; } public int height { get; set; } }
        public sealed class GazeSpec
        {
            public float[][] eyeCenters { get; set; }
            public float[][] eyeRadii { get; set; }
            public float[] maxShift { get; set; }
        }
        public sealed class LayerSpec
        {
            public string id { get; set; }
            public string file { get; set; }
            public float[] pivot { get; set; }
            public Dictionary<string, string> variants { get; set; }
        }
        public sealed class Manifest
        {
            public CanvasSpec canvas { get; set; }
            public GazeSpec gaze { get; set; }
            public LayerSpec[] layers { get; set; }
        }

        private readonly string directory;
        private readonly Manifest manifest;
        private readonly Dictionary<string, LayerSpec> definitions = new Dictionary<string, LayerSpec>();
        private readonly PetMotion bodyMotion = new PetMotion();
        private Bitmap body, output;
        private Part leftHair, rightHair;
        private readonly Part[] heads = new Part[3];
        private readonly EyeGazeLayer[] gazeHeads = new EyeGazeLayer[2];
        private long activeSince, lastFrame = -1000;
        private int lastEyes = -1;
        private double lastEyeX = double.NaN, lastEyeY = double.NaN;
        private double lastHeadX = double.NaN, lastHeadY = double.NaN;
        internal int Width { get; private set; }
        internal int Height { get; private set; }

        internal LayeredIdleAnimation(string directory, int height)
        {
            this.directory = directory;
            manifest = new JavaScriptSerializer().Deserialize<Manifest>(File.ReadAllText(Path.Combine(directory, "layers.json")));
            if (manifest == null || manifest.canvas == null || manifest.canvas.width <= 0 || manifest.canvas.height <= 0 || manifest.layers == null)
                throw new InvalidDataException("Invalid idle layer manifest.");
            if (manifest.gaze == null || manifest.gaze.eyeCenters == null || manifest.gaze.eyeRadii == null ||
                manifest.gaze.eyeCenters.Length != 2 || manifest.gaze.eyeRadii.Length != 2 ||
                manifest.gaze.maxShift == null || manifest.gaze.maxShift.Length != 2 ||
                manifest.gaze.maxShift[0] <= 0 || manifest.gaze.maxShift[1] <= 0)
                throw new InvalidDataException("Missing gaze geometry in idle layer manifest.");
            for (int i = 0; i < 2; i++)
                if (manifest.gaze.eyeCenters[i] == null || manifest.gaze.eyeCenters[i].Length != 2 ||
                    manifest.gaze.eyeRadii[i] == null || manifest.gaze.eyeRadii[i].Length != 2 ||
                    manifest.gaze.eyeRadii[i][0] <= 0 || manifest.gaze.eyeRadii[i][1] <= 0)
                    throw new InvalidDataException("Invalid gaze eye region.");
            foreach (LayerSpec definition in manifest.layers)
            {
                if (definition.pivot == null || definition.pivot.Length != 2)
                    throw new InvalidDataException("Missing layer pivot: " + definition.id);
                definitions.Add(definition.id, definition);
            }
            try { Resize(height); }
            catch { Dispose(); throw; }
        }

        private Bitmap Load(string filename)
        {
            Bitmap image = new Bitmap(Path.Combine(directory, filename));
            if (image.Width == manifest.canvas.width && image.Height == manifest.canvas.height) return image;
            image.Dispose();
            throw new InvalidDataException("Idle layer canvas mismatch: " + filename);
        }

        private Bitmap Scale(Bitmap original)
        {
            Bitmap result = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.PageUnit = GraphicsUnit.Pixel;
                g.PageScale = 1f;
                g.CompositingMode = CompositingMode.SourceCopy;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(original, new Rectangle(0, 0, Width, Height), 0, 0,
                    original.Width, original.Height, GraphicsUnit.Pixel);
            }
            return result;
        }

        private Bitmap LoadScaled(string id)
        {
            using (Bitmap image = Load(definitions[id].file)) return Scale(image);
        }

        internal void Resize(int height)
        {
            ReleaseFrames();
            Height = height;
            Width = (int)Math.Round(height * (double)manifest.canvas.width / manifest.canvas.height);
            body = LoadScaled("body");
            leftHair = new Part(LoadScaled("hair-back-left"));
            rightHair = new Part(LoadScaled("hair-back-right"));
            string[] variants = { "open", "half", "closed" };
            using (Bitmap head = Load(definitions["head-base"].file))
                for (int i = 0; i < heads.Length; i++)
                    using (Bitmap eyes = Load(definitions["eyes"].variants[variants[i]]))
                    using (Bitmap combined = new Bitmap(head.Width, head.Height, PixelFormat.Format32bppPArgb))
                    {
                        // Composite eyes before resampling so their skin margin and
                        // the face share exactly the same transform and filtering.
                        using (Graphics g = Graphics.FromImage(combined))
                        {
                            g.PageUnit = GraphicsUnit.Pixel;
                            g.PageScale = 1f;
                            Rectangle canvas = new Rectangle(0, 0, head.Width, head.Height);
                            g.DrawImage(head, canvas, 0, 0, head.Width, head.Height, GraphicsUnit.Pixel);
                            g.DrawImage(eyes, canvas, 0, 0, eyes.Width, eyes.Height, GraphicsUnit.Pixel);
                        }
                        heads[i] = new Part(Scale(combined));
                    }
            Size sourceCanvas = new Size(manifest.canvas.width, manifest.canvas.height);
            Size size = new Size(Width, Height);
            for (int i = 0; i < gazeHeads.Length; i++)
                gazeHeads[i] = new EyeGazeLayer(heads[i].Image, heads[i].Bounds, sourceCanvas, size,
                    manifest.gaze.eyeCenters, manifest.gaze.eyeRadii, manifest.gaze.maxShift,
                    i == 0 ? 1 : 0.55);
            output = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
        }

        internal void Enter(long now) { activeSince = now; lastEyes = -1; }

        internal static int EyeForBlink(long now, long start, long end)
        {
            if (now < start || now >= end) return 0;
            return now - start < 45 || end - now <= 50 ? 1 : 2;
        }

        private static double HeadAngle(double now)
        {
            return 0.38 * Math.Sin(now * Math.PI * 2 / 6200.0);
        }

        private PointF Pivot(string id)
        {
            float[] p = definitions[id].pivot;
            return new PointF(p[0] * Width / manifest.canvas.width, p[1] * Height / manifest.canvas.height);
        }

        private void DrawTurned(Graphics g, Part image, string id, double angle, double dx, double dy)
        {
            DrawTurned(g, image.Image, image.Bounds, id, angle, dx, dy);
        }

        private void DrawTurned(Graphics g, Bitmap image, Rectangle bounds, string id,
            double angle, double dx, double dy)
        {
            PointF pivot = Pivot(id);
            GraphicsState saved = g.Save();
            g.TranslateTransform(pivot.X + (float)dx, pivot.Y + (float)dy);
            g.RotateTransform((float)angle);
            g.TranslateTransform(-pivot.X, -pivot.Y);
            g.DrawImage(image, bounds, 0, 0, bounds.Width, bounds.Height, GraphicsUnit.Pixel);
            g.Restore(saved);
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
                eyeX != lastEyeX || eyeY != lastEyeY || faceX != lastHeadX || faceY != lastHeadY;
            if (!changed) return output;
            lastFrame = now;
            lastEyes = eyes;
            lastEyeX = eyeX; lastEyeY = eyeY;
            lastHeadX = faceX; lastHeadY = faceY;
            double gain = Math.Max(0, Math.Min(1, (now - activeSince) / 240.0));
            gain = gain * gain * (3 - 2 * gain);
            double breath = PetMotion.Breath(now, Height);
            double anchor = Math.Floor(Height * 0.88);
            double neckOffset = breath * (anchor - Pivot("head-base").Y) / anchor;
            double headAngle = HeadAngle(now) * gain + faceX * 1.1;
            double headDx = faceX * Math.Min(2.8, Height / 120.0);
            double headDy = neckOffset + faceY * Math.Min(1.9, Height * 0.006);
            double leftAngle = (HeadAngle(now - 220) + 0.17 * Math.Sin((now - 220) * Math.PI * 2 / 4700.0)) * gain + faceX * 0.5;
            double rightAngle = (HeadAngle(now - 340) + 0.14 * Math.Sin((now - 340) * Math.PI * 2 / 5300.0)) * gain + faceX * 0.5;
            bool bodyChanged;
            Bitmap breathingBody = bodyMotion.Render(body, breath, out bodyChanged);
            using (Graphics g = Graphics.FromImage(output))
            {
                g.PageUnit = GraphicsUnit.Pixel;
                g.PageScale = 1f;
                g.Clear(Color.Transparent);
                g.CompositingMode = CompositingMode.SourceOver;
                // Artwork is already downsampled with bicubic filtering at resize.
                // Bilinear subpixel transforms avoid expensive repeated resampling.
                g.InterpolationMode = InterpolationMode.Bilinear;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                DrawTurned(g, leftHair, "hair-back-left", leftAngle, headDx * 0.5, neckOffset + faceY * 0.45);
                DrawTurned(g, rightHair, "hair-back-right", rightAngle, headDx * 0.5, neckOffset + faceY * 0.45);
                g.DrawImage(breathingBody, new Rectangle(0, 0, Width, Height), 0, 0, Width, Height, GraphicsUnit.Pixel);
                Bitmap face = eyes < gazeHeads.Length ? gazeHeads[eyes].Render(eyeX, eyeY) : heads[eyes].Image;
                DrawTurned(g, face, heads[eyes].Bounds, "head-base", headAngle, headDx, headDy);
            }
            return output;
        }

        private void ReleaseFrames()
        {
            bodyMotion.Reset();
            if (body != null) body.Dispose();
            if (leftHair != null) leftHair.Dispose();
            if (rightHair != null) rightHair.Dispose();
            if (output != null) output.Dispose();
            body = output = null;
            leftHair = rightHair = null;
            for (int i = 0; i < gazeHeads.Length; i++) { if (gazeHeads[i] != null) gazeHeads[i].Dispose(); gazeHeads[i] = null; }
            for (int i = 0; i < heads.Length; i++) { if (heads[i] != null) heads[i].Dispose(); heads[i] = null; }
            lastFrame = -1000;
            lastEyes = -1;
            lastEyeX = lastEyeY = lastHeadX = lastHeadY = double.NaN;
        }

        public void Dispose() { ReleaseFrames(); bodyMotion.Dispose(); }
    }
}
