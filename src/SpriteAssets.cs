using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace MakiseDesktopPet
{
    internal enum Pose { Idle, Blink, PokeBlock, PokeFold, PokeCheek, Rapid, Drag, Sleep, Wake }

    internal sealed class SpriteAssets : IDisposable
    {
        private readonly Dictionary<Pose, Bitmap> originals = new Dictionary<Pose, Bitmap>();
        private readonly Dictionary<Pose, Bitmap> scaled = new Dictionary<Pose, Bitmap>();
        private Bitmap dragMovingLeft;
        internal int Width { get; private set; }
        internal int Height { get; private set; }

        internal SpriteAssets(string directory, int height)
        {
            ClickColorCalibration calibration = ClickColorCalibration.Load(directory);
            foreach (Pose pose in Enum.GetValues(typeof(Pose)))
            {
                string filename = Path.Combine(directory, SpriteFilename(pose));
                if (!File.Exists(filename))
                    throw new FileNotFoundException("Missing sprite: " + filename, filename);
                using (Bitmap image = new Bitmap(filename))
                {
                    Bitmap bitmap = image.Clone(
                        new Rectangle(0, 0, image.Width, image.Height), PixelFormat.Format32bppArgb);
                    if (calibration != null) calibration.Apply(bitmap, filename);
                    originals.Add(pose, bitmap);
                }
            }

            Size first = originals[Pose.Idle].Size;
            foreach (Bitmap bitmap in originals.Values)
                if (bitmap.Size != first)
                    throw new InvalidDataException("All sprite images must use the same canvas size.");
            Resize(height);
        }

        private static string SpriteFilename(Pose pose)
        {
            if (pose == Pose.PokeBlock) return "poke-block.png";
            if (pose == Pose.PokeFold) return "poke-fold.png";
            if (pose == Pose.PokeCheek) return "poke-cheek.png";
            return pose.ToString().ToLowerInvariant() + ".png";
        }

        internal void Resize(int height)
        {
            if (dragMovingLeft != null) dragMovingLeft.Dispose();
            dragMovingLeft = null;
            foreach (Bitmap bitmap in scaled.Values)
                bitmap.Dispose();
            scaled.Clear();

            Height = height;
            Size source = originals[Pose.Idle].Size;
            Width = Math.Max(1, (int)Math.Round(source.Width * (double)height / source.Height));
            foreach (KeyValuePair<Pose, Bitmap> item in originals)
            {
                Bitmap output = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
                using (Graphics graphics = Graphics.FromImage(output))
                {
                    graphics.PageUnit = GraphicsUnit.Pixel;
                    graphics.PageScale = 1f;
                    graphics.Clear(Color.Transparent);
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.DrawImage(item.Value, new Rectangle(0, 0, Width, Height),
                        0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
                }
                scaled.Add(item.Key, output);
            }
            // The source drag pose has its legs trailing left (moving right).
            dragMovingLeft = scaled[Pose.Drag].Clone(
                new Rectangle(0, 0, Width, Height), PixelFormat.Format32bppPArgb);
            dragMovingLeft.RotateFlip(RotateFlipType.RotateNoneFlipX);
        }

        internal Bitmap Get(Pose pose) { return scaled[pose]; }
        internal Bitmap GetDrag(bool movingLeft)
        {
            return movingLeft ? dragMovingLeft : scaled[Pose.Drag];
        }

        public void Dispose()
        {
            if (dragMovingLeft != null) dragMovingLeft.Dispose();
            dragMovingLeft = null;
            foreach (Bitmap bitmap in scaled.Values) bitmap.Dispose();
            foreach (Bitmap bitmap in originals.Values) bitmap.Dispose();
            scaled.Clear();
            originals.Clear();
        }
    }
}
