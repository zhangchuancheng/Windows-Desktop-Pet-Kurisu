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
            public LayerSpec[] layers { get; set; }
        }

        private readonly string directory;
        private readonly Manifest manifest;
        private readonly Dictionary<string, LayerSpec> definitions = new Dictionary<string, LayerSpec>();
        private readonly PetMotion bodyMotion = new PetMotion();
        private Bitmap body, output;
        private Part leftHair, rightHair;
        private readonly Part[] heads = new Part[3];
        private long activeSince, lastFrame = -1000;
        private int lastEyes = -1;
        internal int Width { get; private set; }
        internal int Height { get; private set; }

        internal LayeredIdleAnimation(string directory, int height)
        {
            this.directory = directory;
            manifest = new JavaScriptSerializer().Deserialize<Manifest>(File.ReadAllText(Path.Combine(directory, "layers.json")));
            if (manifest == null || manifest.canvas == null || manifest.canvas.width <= 0 || manifest.canvas.height <= 0 || manifest.layers == null)
                throw new InvalidDataException("Invalid idle layer manifest.");
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

        private void DrawTurned(Graphics g, Part image, string id, double angle, double dy)
        {
            PointF pivot = Pivot(id);
            GraphicsState saved = g.Save();
            g.TranslateTransform(pivot.X, pivot.Y + (float)dy);
            g.RotateTransform((float)angle);
            g.TranslateTransform(-pivot.X, -pivot.Y);
            g.DrawImage(image.Image, image.Bounds, 0, 0, image.Bounds.Width, image.Bounds.Height, GraphicsUnit.Pixel);
            g.Restore(saved);
        }

        internal Bitmap Render(long now, int eyes, out bool changed)
        {
            if (eyes < 0 || eyes > 2) throw new ArgumentOutOfRangeException("eyes");
            changed = eyes != lastEyes || now - lastFrame >= 16 || now < lastFrame;
            if (!changed) return output;
            lastFrame = now;
            lastEyes = eyes;
            double gain = Math.Max(0, Math.Min(1, (now - activeSince) / 240.0));
            gain = gain * gain * (3 - 2 * gain);
            double breath = PetMotion.Breath(now, Height);
            double anchor = Math.Floor(Height * 0.88);
            double neckOffset = breath * (anchor - Pivot("head-base").Y) / anchor;
            double headAngle = HeadAngle(now) * gain;
            double leftAngle = (HeadAngle(now - 220) + 0.17 * Math.Sin((now - 220) * Math.PI * 2 / 4700.0)) * gain;
            double rightAngle = (HeadAngle(now - 340) + 0.14 * Math.Sin((now - 340) * Math.PI * 2 / 5300.0)) * gain;
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
                DrawTurned(g, leftHair, "hair-back-left", leftAngle, neckOffset);
                DrawTurned(g, rightHair, "hair-back-right", rightAngle, neckOffset);
                g.DrawImage(breathingBody, new Rectangle(0, 0, Width, Height), 0, 0, Width, Height, GraphicsUnit.Pixel);
                DrawTurned(g, heads[eyes], "head-base", headAngle, neckOffset);
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
            for (int i = 0; i < heads.Length; i++) { if (heads[i] != null) heads[i].Dispose(); heads[i] = null; }
            lastFrame = -1000;
            lastEyes = -1;
        }

        public void Dispose() { ReleaseFrames(); bodyMotion.Dispose(); }
    }
}
