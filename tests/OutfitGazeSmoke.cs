using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace MakiseDesktopPet
{
    internal static class OutfitGazeSmoke
    {
        private static void Require(bool ok, string message)
        { if (!ok) throw new Exception(message); }

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                string output = Path.Combine(args[0], "test-artifacts", "outfit-gaze");
                Directory.CreateDirectory(output);
                foreach (OutfitCatalog.Outfit outfit in OutfitCatalog.All)
                {
                    if (outfit.Id == OutfitCatalog.OriginalId) continue;
                    string directory = Path.Combine(args[0], "assets", "concepts",
                        "outfit-actions-2026-10-19", outfit.Id);
                    using (Bitmap sheet = new Bitmap(780, 790))
                    using (Graphics display = Graphics.FromImage(sheet))
                    using (Font label = new Font("Arial", 10))
                    {
                        display.Clear(Color.FromArgb(239,237,231));
                        foreach (int size in new[] { 100, 355, 500 })
                        using (IdleMotionAnimation idle = new IdleMotionAnimation(directory, directory, size, true, true))
                        {
                            idle.Enter(0);
                            bool changed;
                            for (int eyes = 0; eyes <= 2; eyes++)
                            using (Bitmap neutral = (Bitmap)idle.Render(1800, eyes, out changed).Clone())
                            {
                                for (int dx = -1; dx <= 1; dx++)
                                    for (int dy = -1; dy <= 1; dy++)
                                    {
                                        Bitmap frame = idle.Render(1800, eyes, dx, dy, 0, 0, out changed);
                                        int differences = 0;
                                        for (int y = 0; y < size; y++)
                                            for (int x = 0; x < frame.Width; x++)
                                            {
                                                Color a=neutral.GetPixel(x,y), b=frame.GetPixel(x,y);
                                                Require(a.A == b.A, outfit.Id + " gaze changed silhouette");
                                                if (a == b) continue;
                                                differences++;
                                                Require(y > size*.24 && y < size*.43 &&
                                                    x > frame.Width*.26 && x < frame.Width*.78,
                                                    outfit.Id + " gaze changed pixels outside eyes");
                                            }
                                        Require(eyes == 2 || (dx == 0 && dy == 0) ? differences == 0 : differences > 0,
                                            outfit.Id + " eye tracking inactive or closed eye moved");
                                        if (size == 355 && eyes < 2 && dy == 0)
                                        {
                                            int left=(dx+1)*260, top=eyes*395;
                                            display.DrawString(outfit.Id + " eyes=" + eyes + " gaze=" + dx,
                                                label, Brushes.Black, left+5,top+5);
                                            display.DrawImageUnscaled(frame,left,top+30);
                                        }
                                    }
                                Bitmap restored=idle.Render(1800, eyes, 0,0,0,0,out changed);
                                for (int y=0;y<size;y++)
                                    for(int x=0;x<restored.Width;x++)
                                        Require(neutral.GetPixel(x,y)==restored.GetPixel(x,y),outfit.Id+" gaze did not recenter exactly");
                            }
                        }
                        sheet.Save(Path.Combine(output,outfit.Id+".png"),ImageFormat.Png);
                    }
                    Console.WriteLine("PASS: "+outfit.Id+" nine gaze directions, open/half/closed eyes, 100/355/500px and exact recenter");
                }
            }
            catch(Exception error) { Console.Error.WriteLine("FAIL: "+error); Environment.ExitCode=1; }
        }
    }
}
