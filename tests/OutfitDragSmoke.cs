using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

namespace MakiseDesktopPet
{
    internal static class OutfitDragSmoke
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
                string output = Path.Combine(root, "test-artifacts", "outfit-drag");
                Directory.CreateDirectory(output);
                foreach (OutfitCatalog.Outfit outfit in OutfitCatalog.All)
                {
                    if (outfit.Id == OutfitCatalog.OriginalId) continue;
                    string directory = Path.Combine(root, "assets", "concepts",
                        "outfit-actions-2026-10-19", outfit.Id);
                    using (Bitmap sheet = new Bitmap(1040, 395))
                    using (Graphics display = Graphics.FromImage(sheet))
                    using (Font label = new Font("Arial", 10))
                    {
                        display.Clear(Color.FromArgb(239, 237, 231));
                        foreach (int size in new[] { 100, 355, 500 })
                        using (SpriteAssets sprites = new SpriteAssets(directory, size))
                        using (DragWarpAnimation drag = new DragWarpAnimation(
                            directory, sprites.Width, size, false))
                        {
                            bool changed;
                            using (Bitmap baseline = (Bitmap)drag.Render(0, out changed).Clone())
                            {
                                for (int y = 0; y < size; y++)
                                    for (int x = 0; x < sprites.Width; x++)
                                        Require(baseline.GetPixel(x,y) == sprites.Get(Pose.Drag).GetPixel(x,y),
                                            outfit.Id + " changed its resting drag pose");
                                int column = 0;
                                foreach (int time in new[] { 0, 460, 1320, 1440, 1460 })
                                {
                                    drag.ObservePointer(new Point(time, -time));
                                    Bitmap frame = drag.Render(time, out changed);
                                    int differences = 0;
                                    for (int y = 0; y < size; y++)
                                        for (int x = 0; x < sprites.Width; x++)
                                            if (frame.GetPixel(x,y) != baseline.GetPixel(x,y))
                                            {
                                                differences++;
                                                Require(y > size * 0.23 && y < size * 0.53 &&
                                                    x > sprites.Width * 0.30 && x < sprites.Width * 0.75,
                                                    outfit.Id + " moved outside eyes and tear paths");
                                            }
                                    Require((time == 0 || time == 1460) ? differences == 0 : differences > 0,
                                        outfit.Id + " missing animation or failed to restore eyes");
                                    if (size == 355 && column < 4)
                                    {
                                        display.DrawImageUnscaled(frame, column * 260, 30);
                                        display.DrawString(outfit.Id + " / " + time + " ms", label,
                                            Brushes.Black, column * 260 + 5, 5);
                                    }
                                    column++;
                                }
                            }
                            drag.Resize(Math.Max(16, sprites.Width / 2), Math.Max(16, size / 2));
                            drag.Render(1320, out changed);
                            drag.ResetMotion(Point.Empty);
                            Require(DragWarpAnimation.DragBlinkEye(0) == 0, "Reset starts with closed eyes");
                        }
                        sheet.Save(Path.Combine(output, outfit.Id + ".png"), ImageFormat.Png);
                    }
                    Console.WriteLine("PASS: " + outfit.Id + " tears, half/closed eyes, exact reopen, static body and resizing");
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
