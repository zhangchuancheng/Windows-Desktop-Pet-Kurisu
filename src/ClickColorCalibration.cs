using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

namespace MakiseDesktopPet
{
    // Small, continuous RGB corrections fitted to the rendered idle palette.
    // Calibration data belongs to exact artwork, not to arbitrary replacement sprites.
    internal sealed class ClickColorCalibration
    {
        public sealed class Anchor
        {
            public string material { get; set; }
            public double[] source { get; set; }
            public double[] target { get; set; }
        }
        public sealed class Profile
        {
            public string file { get; set; }
            public string sha256 { get; set; }
            public Anchor[] anchors { get; set; }
        }
        public sealed class Manifest
        {
            public int version { get; set; }
            public Dictionary<string, string> references { get; set; }
            public Profile[] profiles { get; set; }
        }
        private readonly Manifest manifest;
        private ClickColorCalibration(Manifest manifest) { this.manifest = manifest; }

        private static bool Matches(string path, string expected)
        {
            if (!File.Exists(path)) return false;
            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return string.Equals(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""), expected, StringComparison.OrdinalIgnoreCase);
        }

        internal static ClickColorCalibration Load(string directory)
        {
            string path = Path.Combine(directory, "click-colors.json");
            if (!File.Exists(path)) return null;
            Manifest data = new JavaScriptSerializer().Deserialize<Manifest>(File.ReadAllText(path));
            if (data == null || data.version != 1 || data.references == null || data.profiles == null)
                throw new InvalidDataException("Invalid click color calibration manifest.");
            foreach (KeyValuePair<string, string> reference in data.references)
                if (!Matches(Path.Combine(directory, reference.Key), reference.Value)) return null;
            foreach (Profile profile in data.profiles)
            {
                if (profile.anchors == null || profile.anchors.Length == 0)
                    throw new InvalidDataException("Missing click palette anchors.");
                foreach (Anchor anchor in profile.anchors)
                {
                    if (anchor.source == null || anchor.target == null || anchor.source.Length != 3 || anchor.target.Length != 3)
                        throw new InvalidDataException("Invalid RGB palette anchor.");
                    for (int c = 0; c < 3; c++)
                        if (double.IsNaN(anchor.source[c]) || double.IsNaN(anchor.target[c]) || anchor.source[c] < 0 || anchor.source[c] > 255 || anchor.target[c] < 0 || anchor.target[c] > 255 || Math.Abs(anchor.source[c] - anchor.target[c]) > 16)
                            throw new InvalidDataException("Click palette adjustment exceeds supported range.");
                }
            }
            return new ClickColorCalibration(data);
        }

        internal Profile Find(string filename)
        {
            foreach (Profile profile in manifest.profiles)
                if (string.Equals(Path.GetFileName(filename), profile.file, StringComparison.OrdinalIgnoreCase) && Matches(filename, profile.sha256)) return profile;
            return null;
        }

        internal static int MapColor(int rgb, Profile profile)
        {
            int r = (rgb >> 16) & 255, g = (rgb >> 8) & 255, b = rgb & 255;
            double total = 0, red = 0, green = 0, blue = 0;
            foreach (Anchor anchor in profile.anchors)
            {
                double dr = r - anchor.source[0], dg = g - anchor.source[1], db = b - anchor.source[2];
                double distance = 16 + dr * dr + dg * dg + db * db;
                double weight = 1 / (distance * distance);
                total += weight;
                red += weight * (anchor.target[0] - anchor.source[0]);
                green += weight * (anchor.target[1] - anchor.source[1]);
                blue += weight * (anchor.target[2] - anchor.source[2]);
            }
            return (Clamp(r + red / total) << 16) | (Clamp(g + green / total) << 8) | Clamp(b + blue / total);
        }

        private static int Clamp(double value) { return Math.Max(0, Math.Min(255, (int)Math.Round(value))); }

        internal bool Apply(Bitmap bitmap, string filename)
        {
            Profile profile = Find(filename);
            if (profile == null) return false;
            Dictionary<int, int> cache = new Dictionary<int, int>();
            BitmapData data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
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
                        int rgb = (row[x + 2] << 16) | (row[x + 1] << 8) | row[x], mapped;
                        if (!cache.TryGetValue(rgb, out mapped)) cache[rgb] = mapped = MapColor(rgb, profile);
                        row[x] = (byte)(mapped & 255);
                        row[x + 1] = (byte)((mapped >> 8) & 255);
                        row[x + 2] = (byte)((mapped >> 16) & 255);
                    }
                    Marshal.Copy(row, 0, address, row.Length);
                }
            }
            finally { bitmap.UnlockBits(data); }
            return true;
        }
    }
}
