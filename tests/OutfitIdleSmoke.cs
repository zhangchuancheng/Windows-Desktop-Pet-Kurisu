using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace MakiseDesktopPet
{
    internal static class OutfitIdleSmoke
    {
        private static void Require(bool value, string message)
        {
            if (!value) throw new Exception(message);
        }

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                string root = args[0];
                string output = Path.Combine(root, "test-artifacts", "outfit-idle");
                Directory.CreateDirectory(output);
                foreach (OutfitCatalog.Outfit outfit in OutfitCatalog.All)
                {
                    if (outfit.Id == OutfitCatalog.OriginalId) continue;
                    string directory = Path.Combine(root, "assets", "concepts", "outfit-actions-2026-10-19", outfit.Id);
                    using (Bitmap sheet = new Bitmap(780, 395))
                    using (Graphics display = Graphics.FromImage(sheet))
                    using (Font label = new Font("Arial", 10))
                    {
                        display.Clear(Color.FromArgb(239, 237, 231));
                        using (Bitmap original = new Bitmap(Path.Combine(directory, "idle.png")))
                            foreach (string name in new[] { "blink-half.png", "blink.png" })
                                using (Bitmap old = new Bitmap(Path.Combine(directory, name)))
                                {
                                    int changed = 0, visible = 0;
                                    for (int y = original.Height / 2; y < original.Height; y++)
                                        for (int x = 0; x < original.Width; x++)
                                        {
                                            Color a = original.GetPixel(x,y), b = old.GetPixel(x,y);
                                            if (a.A < 128 && b.A < 128) continue;
                                            visible++;
                                            if (Math.Abs(a.A-b.A) > 16 || Math.Abs(a.R-b.R) > 16 ||
                                                Math.Abs(a.G-b.G) > 16 || Math.Abs(a.B-b.B) > 16) changed++;
                                        }
                                    Console.WriteLine("OLD " + outfit.Id + " " + name +
                                        " body difference: " + (100.0 * changed / visible).ToString("F1") + "%");
                                }
                        foreach (int size in new[] { 100, 355, 500 })
                        using (IdleMotionAnimation idle = new IdleMotionAnimation(directory,
                            directory, size, true, true))
                        {
                            idle.Enter(0);
                            bool changed;
                            foreach (int time in new[] { 0, 100, 1800, 4200 })
                            using (Bitmap baseline = (Bitmap)idle.Render(time, 0, out changed).Clone())
                            {
                                for (int eyes = 0; eyes <= 2; eyes++)
                                {
                                    Bitmap frame = idle.Render(time, eyes, out changed);
                                    int differences = 0;
                                    for (int y = 0; y < size; y++)
                                        for (int x = 0; x < frame.Width; x++)
                                            if (baseline.GetPixel(x,y) != frame.GetPixel(x,y))
                                            {
                                                differences++;
                                                Require(y > size * 0.24 && y < size * 0.43 &&
                                                    x > frame.Width * 0.26 && x < frame.Width * 0.78,
                                                    outfit.Id + " blink altered hair/body at " + size + "px: " + x + "," + y + " time=" + time + " old=" + baseline.GetPixel(x,y) + " new=" + frame.GetPixel(x,y));
                                            }
                                    Require(eyes == 0 ? differences == 0 : differences > 0,
                                        outfit.Id + " missing local blink");
                                    if (size == 355 && time == 1800)
                                    {
                                        display.DrawImageUnscaled(frame, eyes * 260, 30);
                                        display.DrawString(outfit.Id + " / eyes " + eyes,
                                            label, Brushes.Black, eyes * 260 + 5, 5);
                                    }
                                }
                                Bitmap restored = idle.Render(time, 0, out changed);
                                for (int y = 0; y < size; y++)
                                    for (int x = 0; x < restored.Width; x++)
                                        Require(restored.GetPixel(x,y) == baseline.GetPixel(x,y),
                                            outfit.Id + " blink failed to restore baseline");
                            }
                        }
                        sheet.Save(Path.Combine(output, outfit.Id + ".png"), ImageFormat.Png);
                    }
                    Console.WriteLine("PASS: " + outfit.Id + " zero blink-induced body changes at 100/355/500px");
                }
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("FAIL: " + error);
                Environment.ExitCode = 1;
            }
        }
    }
}
