using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

namespace MakiseDesktopPet
{
    internal sealed class DragAnimation : IDisposable
    {
        public sealed class Layout
        {
            public string sheet { get; set; }
            public int columns { get; set; }
            public int rows { get; set; }
            public int frames { get; set; }
            public int frameMs { get; set; }
            public int[] colorKey { get; set; }
        }
        private readonly Bitmap source;
        private readonly Layout layout;
        private Bitmap[] frames;
        private int previous = -1;
        internal int FrameCount { get { return layout.frames; } }
        internal int CycleMs { get { return layout.frameMs * layout.frames; } }

        internal DragAnimation(string directory, int width, int height)
        {
            layout = new JavaScriptSerializer().Deserialize<Layout>(File.ReadAllText(Path.Combine(directory, "drag-animation.json")));
            if (layout == null || layout.columns < 1 || layout.rows < 1 || layout.frames < 2 ||
                layout.frames > layout.columns * layout.rows || layout.frameMs < 40 || layout.frameMs > 1000)
                throw new InvalidDataException("Invalid drag animation layout.");
            string path = Path.Combine(directory, layout.sheet);
            using (Bitmap loaded = new Bitmap(path))
                source = loaded.Clone(new Rectangle(Point.Empty, loaded.Size), PixelFormat.Format32bppArgb);
            if (source.Width < layout.columns * 8 || source.Height < layout.rows * 8)
                throw new InvalidDataException("Drag sheet cells are too small.");
            if (layout.colorKey != null)
            {
                if (layout.colorKey.Length != 3 || layout.colorKey[1] < 200)
                    throw new InvalidDataException("Drag color key must be a bright green RGB color.");
                RemoveColorKey(source, layout.colorKey);
            }
            ClickColorCalibration colors = ClickColorCalibration.Load(directory, "drag-colors.json");
            if (colors != null) colors.Apply(source, path);
            Resize(width, height);
        }

        // Decode the generated chroma-key sheet once. Rendering uses alpha only.
        private static void RemoveColorKey(Bitmap bitmap, int[] key)
        {
            BitmapData data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                byte[] row = new byte[bitmap.Width * 4];
                double keyExcess = key[1] - Math.Max(key[0], key[2]);
                for (int y = 0; y < bitmap.Height; y++)
                {
                    IntPtr address = IntPtr.Add(data.Scan0, y * data.Stride);
                    Marshal.Copy(address, row, 0, row.Length);
                    for (int p = 0; p < row.Length; p += 4)
                    {
                        int excess = row[p + 1] - Math.Max(row[p], row[p + 2]);
                        if (excess <= 8) continue;
                        double alpha = Math.Max(0, 1 - excess / keyExcess);
                        if (alpha < 0.08) { row[p] = row[p + 1] = row[p + 2] = row[p + 3] = 0; continue; }
                        row[p + 3] = (byte)Math.Round(row[p + 3] * alpha);
                        for (int c = 0; c < 3; c++)
                            row[p + c] = (byte)Math.Max(0, Math.Min(255, Math.Round((row[p + c] - key[2 - c] * (1 - alpha)) / alpha)));
                        row[p + 1] = (byte)Math.Min(row[p + 1], Math.Max(row[p], row[p + 2]));
                    }
                    Marshal.Copy(row, 0, address, row.Length);
                }
            }
            finally { bitmap.UnlockBits(data); }
        }

        internal void Resize(int width, int height)
        {
            if (frames != null) foreach (Bitmap frame in frames) frame.Dispose();
            frames = new Bitmap[layout.frames];
            int cellWidth = source.Width / layout.columns, cellHeight = source.Height / layout.rows;
            double scale = Math.Min((width - 4.0) / cellWidth, (height - 4.0) / cellHeight);
            int drawWidth = (int)Math.Round(cellWidth * scale), drawHeight = (int)Math.Round(cellHeight * scale);
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(frames[i]))
                {
                    g.PageUnit = GraphicsUnit.Pixel;
                    g.Clear(Color.Transparent);
                    g.CompositingMode = CompositingMode.SourceCopy;
                    // Native-alpha artwork retains sharper linework with bicubic sampling.
                    // Keep bilinear for legacy keyed sheets to avoid green edge ringing.
                    g.InterpolationMode = layout.colorKey == null
                        ? InterpolationMode.HighQualityBicubic : InterpolationMode.HighQualityBilinear;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    // Round grid origins separately for generated sheets with fractional cell widths.
                    // This matches the approved preview and avoids cumulative horizontal drift.
                    int sourceX = (int)Math.Round((i % layout.columns) * source.Width / (double)layout.columns);
                    int sourceY = (int)Math.Round((i / layout.columns) * source.Height / (double)layout.rows);
                    g.DrawImage(source, new Rectangle((width - drawWidth) / 2, 2, drawWidth, drawHeight),
                        sourceX, sourceY, cellWidth, cellHeight, GraphicsUnit.Pixel);
                }
            }
            previous = -1;
        }

        internal Bitmap Render(long elapsed, out bool changed)
        {
            int index = (int)(Math.Max(0, elapsed) / layout.frameMs % frames.Length);
            changed = index != previous;
            previous = index;
            return frames[index];
        }

        public void Dispose()
        {
            if (frames != null) foreach (Bitmap frame in frames) frame.Dispose();
            frames = null;
            source.Dispose();
        }
    }
}
