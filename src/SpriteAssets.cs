using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace MakiseDesktopPet
{
    internal enum Pose { Idle, Blink, Poke, PokeSkeptical, PokeWink, Drag, Sleep, Wake }

    internal sealed class SpriteAssets : IDisposable
    {
        // The bundled prototype click frames are slightly brighter than idle. Match
        // exact file hashes so replacement artwork is never recolored.
        private const string PrototypePokeSha256 =
            "B1F43D6DF870DA2BB13718C3CF9749F6C250D5BB10A8FFFA0733F4CD39C9B0DD";
        private const string PrototypePokeSkepticalSha256 =
            "67105AEB2AE57CBD46279BE272ECF962D0A3DDC166AEDE8F9307F5AB12245918";
        private const string PrototypePokeWinkSha256 =
            "02C8310AF3EC8C63053A7A6662FEA0C2D1013EC883B2E3E7A327B54016E917FA";
        private readonly Dictionary<Pose, Bitmap> originals = new Dictionary<Pose, Bitmap>();
        private readonly Dictionary<Pose, Bitmap> scaled = new Dictionary<Pose, Bitmap>();
        private Bitmap dragMovingLeft;
        internal int Width { get; private set; }
        internal int Height { get; private set; }

        internal SpriteAssets(string directory, int height)
        {
            foreach (Pose pose in Enum.GetValues(typeof(Pose)))
            {
                string filename = Path.Combine(directory, SpriteFilename(pose));
                if (!File.Exists(filename))
                    throw new FileNotFoundException("Missing sprite: " + filename, filename);
                using (Bitmap image = new Bitmap(filename))
                {
                    Bitmap bitmap = image.Clone(
                        new Rectangle(0, 0, image.Width, image.Height), PixelFormat.Format32bppArgb);
                    int correction = PrototypeClickCorrection(pose, filename);
                    if (correction > 0)
                        CalibratePrototypeClick(bitmap, correction);
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
            if (pose == Pose.PokeSkeptical) return "poke-skeptical.png";
            if (pose == Pose.PokeWink) return "poke-wink.png";
            return pose.ToString().ToLowerInvariant() + ".png";
        }

        private static int PrototypeClickCorrection(Pose pose, string filename)
        {
            string expectedHash;
            int correction;
            switch (pose)
            {
                case Pose.Poke:
                    expectedHash = PrototypePokeSha256;
                    correction = 4;
                    break;
                case Pose.PokeSkeptical:
                    expectedHash = PrototypePokeSkepticalSha256;
                    correction = 2;
                    break;
                case Pose.PokeWink:
                    expectedHash = PrototypePokeWinkSha256;
                    correction = 3;
                    break;
                default:
                    return 0;
            }
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(filename))
            {
                string hash = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "");
                return string.Equals(hash, expectedHash, StringComparison.OrdinalIgnoreCase)
                    ? correction : 0;
            }
        }

        private static void CalibratePrototypeClick(Bitmap bitmap, int maxCorrection)
        {
            Rectangle area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(area, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                byte[] row = new byte[bitmap.Width * 4];
                for (int y = 0; y < bitmap.Height; y++)
                {
                    IntPtr address = IntPtr.Add(data.Scan0, y * data.Stride);
                    Marshal.Copy(address, row, 0, row.Length);
                    for (int x = 0; x < row.Length; x += 4)
                    {
                        if (row[x + 3] == 0) continue;
                        double luminance = 0.2126 * row[x + 2] +
                            0.7152 * row[x + 1] + 0.0722 * row[x];
                        double factor = Math.Max(0, Math.Min(1, (240 - luminance) / 60));
                        int correction = (int)Math.Round(maxCorrection * factor);
                        for (int channel = 0; channel < 3; channel++)
                            row[x + channel] = (byte)Math.Max(0, row[x + channel] - correction);
                    }
                    Marshal.Copy(row, 0, address, row.Length);
                }
            }
            finally { bitmap.UnlockBits(data); }
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
