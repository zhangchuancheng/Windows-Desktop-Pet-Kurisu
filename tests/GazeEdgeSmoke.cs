using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Web.Script.Serialization;

namespace MakiseDesktopPet
{
    internal static class GazeEdgeSmoke
    {
        private static void Require(bool condition, string message)
        { if (!condition) throw new Exception(message); }

        // A black fixed rim, white sclera and blue iris make a duplicated rim
        // observable independently of the production artwork and its contours.
        private static void CheckRim()
        {
            using (Bitmap source = new Bitmap(100, 80, PixelFormat.Format32bppPArgb))
            {
                using (Graphics g = Graphics.FromImage(source))
                {
                    g.Clear(Color.White);
                    using (Pen rim = new Pen(Color.Black, 3))
                        g.DrawRectangle(rim, 10, 10, 80, 60);
                    g.FillEllipse(Brushes.SteelBlue, 35, 20, 30, 40);
                }
                float[][][] opening = {
                    new[] { new float[] {10,10}, new float[] {90,10} },
                    new[] { new float[] {10,70}, new float[] {90,70} }
                };
                using (EyeGazeLayer gaze = new EyeGazeLayer(source,
                    new Rectangle(0, 0, 100, 80), source.Size, source.Size,
                    new[] { new float[] {50,40} }, new[] { opening },
                    new float[] {12,6}, 1))
                {
                    using (Bitmap centered = (Bitmap)gaze.Render(0,0).Clone())
                    {
                    for (int angle = 0; angle < 32; angle++)
                    {
                        double phase = angle * Math.PI / 16;
                        Bitmap frame = gaze.Render(Math.Cos(phase), Math.Sin(phase));
                        for (int y = 25; y <= 55; y++)
                            foreach (int x in new[] {18,82})
                            {
                                Color color = frame.GetPixel(x,y);
                                Require(color.R > 240 && color.G > 240 && color.B > 240,
                                    "Fixed dark corner stretched into sclera, angle=" + angle);
                            }
                        for (int x = 20; x <= 80; x++)
                            foreach (int y in new[] {10,70})
                                Require(frame.GetPixel(x,y) == source.GetPixel(x,y),
                                    "Fixed eyelid moved");
                        Require(frame.GetPixel(10,40) == source.GetPixel(10,40),
                            "Fixed corner moved");
                        // Repeated/interleaved calls must not accumulate trails.
                        using (Bitmap first = (Bitmap)frame.Clone())
                        {
                            gaze.Render(-Math.Cos(phase), -Math.Sin(phase));
                            Bitmap repeated = gaze.Render(Math.Cos(phase), Math.Sin(phase));
                            for (int y=0;y<80;y++)
                                for (int x=0;x<100;x++)
                                    Require(first.GetPixel(x,y) == repeated.GetPixel(x,y),
                                        "Gaze accumulated a trail");
                        }
                    }
                    Bitmap neutral = gaze.Render(0,0);
                    for (int y=0;y<80;y++)
                        for (int x=0;x<100;x++)
                            Require(neutral.GetPixel(x,y) == centered.GetPixel(x,y),
                                "Neutral eye did not restore exactly");
                    }
                }
            }
            Console.WriteLine("PASS: fixed black rim does not smear into white sclera in 32 directions; no trails and exact recenter");
        }

        private static void SaveArtReview(string root)
        {
            string output = Path.Combine(root, "test-artifacts", "gaze-edges");
            Directory.CreateDirectory(output);
            using (Bitmap open = new Bitmap(1800, OutfitCatalog.All.Length * 115))
            using (Bitmap half = new Bitmap(open.Width, open.Height))
            using (Graphics openGraphics = Graphics.FromImage(open))
            using (Graphics halfGraphics = Graphics.FromImage(half))
            using (Font font = new Font("Arial", 10))
            {
                openGraphics.Clear(Color.White);
                halfGraphics.Clear(Color.White);
                int row = 0;
                foreach (OutfitCatalog.Outfit outfit in OutfitCatalog.All)
                {
                    string directory = outfit.Id == OutfitCatalog.OriginalId
                        ? Path.Combine(root, "assets", "sprites")
                        : Path.Combine(root, "assets", "concepts", "outfit-actions-2026-10-19", outfit.Id);
                    using (IdleMotionAnimation idle = new IdleMotionAnimation(directory, directory,
                        500, true, outfit.Id != OutfitCatalog.OriginalId))
                    {
                        idle.Enter(0);
                        bool changed;
                        for (int eyes = 0; eyes < 2; eyes++)
                        {
                            Graphics summary = eyes == 0 ? openGraphics : halfGraphics;
                            summary.InterpolationMode = InterpolationMode.NearestNeighbor;
                            summary.PixelOffsetMode = PixelOffsetMode.Half;
                            int column = 0;
                            for (int dy = -1; dy <= 1; dy++)
                                for (int dx = -1; dx <= 1; dx++)
                                {
                                    Bitmap frame = idle.Render(1800, eyes, dx, dy, 0, 0, out changed);
                                    summary.DrawString(outfit.Id + " " + dx + "," + dy,
                                        font, Brushes.Black, column * 200, row * 115);
                                    // Both eyes from the actual 500px renderer, including
                                    // breathing/resampling, enlarged without smoothing.
                                    summary.DrawImage(frame,
                                        new Rectangle(column * 200, row * 115 + 20, 198, 90),
                                        new Rectangle(119, 140, 153, 70), GraphicsUnit.Pixel);
                                    column++;
                                }
                        }
                    }
                    Console.WriteLine("REVIEW: " + outfit.Id + " both eyes, nine directions, open and half blink");
                    row++;
                }
                open.Save(Path.Combine(output, "all-open.png"), ImageFormat.Png);
                half.Save(Path.Combine(output, "all-half.png"), ImageFormat.Png);
            }
        }

        private static void CheckRigidIris()
        {
            using (Bitmap source = new Bitmap(100, 80, PixelFormat.Format32bppPArgb))
            {
                using (Graphics g = Graphics.FromImage(source))
                {
                    g.Clear(Color.White);
                    using (Pen rim = new Pen(Color.Black, 3))
                        g.DrawRectangle(rim, 10, 10, 80, 60);
                    g.FillEllipse(Brushes.SteelBlue, 35, 20, 30, 40);
                    g.FillEllipse(Brushes.White, 43, 29, 5, 7);
                    g.FillEllipse(Brushes.MidnightBlue, 48, 35, 5, 9);
                }
                float[][][] opening = {
                    new[] { new float[] {10,10}, new float[] {90,10} },
                    new[] { new float[] {10,70}, new float[] {90,70} }
                };
                using (EyeGazeLayer gaze = new EyeGazeLayer(source,
                    new Rectangle(0,0,100,80),source.Size,source.Size,
                    new[] { new float[] {50,40} },new[] { opening },new float[] {12,6},1))
                {
                    for (int dy=-1;dy<=1;dy++)
                        for (int dx=-1;dx<=1;dx++)
                        {
                            Bitmap frame = gaze.Render(dx,dy);
                            for (int y=16;y<=64;y++)
                                for (int x=16;x<=84;x++)
                                {
                                    int sx=x-dx*12, sy=y-dy*6;
                                    Color expected=sx>=30 && sx<=70 && sy>=16 && sy<=64
                                        ? source.GetPixel(sx,sy) : Color.White;
                                    Color actual=frame.GetPixel(x,y);
                                    // Sclera now has a reconstructed stationary
                                    // shadow; only the painted iris must translate.
                                    double rx=(sx-50)/15.0, ry=(sy-40)/20.0;
                                    if (expected.R == 255 && expected.G == 255 && expected.B == 255 &&
                                        rx*rx+ry*ry>.8)
                                        continue;
                                    Require(Math.Abs(expected.R-actual.R)<=1 &&
                                        Math.Abs(expected.G-actual.G)<=1 &&
                                        Math.Abs(expected.B-actual.B)<=1,
                                        "Rigid iris/highlight shape changed at "+dx+","+dy+" pixel "+x+","+y);
                                }
                        }
                }
            }
            Console.WriteLine("PASS: iris outline, pupil and highlight translate rigidly in all eight directions");
        }

        private static void CheckArtwork(string root)
        {
            foreach (OutfitCatalog.Outfit outfit in OutfitCatalog.All)
            {
                string directory = outfit.Id == OutfitCatalog.OriginalId
                    ? Path.Combine(root,"assets","sprites")
                    : Path.Combine(root,"assets","concepts","outfit-actions-2026-10-19",outfit.Id);
                var manifest = new JavaScriptSerializer().Deserialize<IdleMotionAnimation.Manifest>(
                    File.ReadAllText(Path.Combine(directory,"idle-motion.json")));
                using (Bitmap loaded = new Bitmap(Path.Combine(directory,"idle.png")))
                using (Bitmap source = new Bitmap(loaded.Width,loaded.Height,PixelFormat.Format32bppPArgb))
                {
                    using (Graphics g = Graphics.FromImage(source))
                    { g.CompositingMode=CompositingMode.SourceCopy; g.DrawImageUnscaled(loaded,0,0); }
                    using (var gaze = new EyeGazeLayer(source,new Rectangle(Point.Empty,source.Size),
                        source.Size,source.Size,manifest.gaze.eyeCenters,manifest.gaze.eyeApertures,
                        manifest.gaze.maxShift,1))
                    {
                        for (int dy=-1;dy<=1;dy++)
                            for (int dx=-1;dx<=1;dx++)
                            {
                                Bitmap frame=gaze.Render(dx,dy);
                                foreach (float[] center in manifest.gaze.eyeCenters)
                                    for (int y=(int)center[1]-2;y<=(int)center[1]+2;y++)
                                        for (int x=(int)center[0]-10;x<=(int)center[0]+10;x++)
                                        {
                                            Color before=source.GetPixel(x,y);
                                            Color after=frame.GetPixel(x+dx*10,y+dy*5);
                                            // The canvas alpha is stationary;
                                            // compare premultiplied paint, not
                                            // GetPixel's unpremultiplied rounding.
                                            Require(Math.Abs(before.R*before.A-after.R*after.A)<=255 &&
                                                Math.Abs(before.G*before.A-after.G*after.A)<=255 &&
                                                Math.Abs(before.B*before.A-after.B*after.A)<=255,
                                                outfit.Id+" iris detail changed at "+x+","+y+" direction "+dx+","+dy+" expected "+before+" actual "+after);
                                        }
                                if (dy!=0 || dx==0) continue;
                                for (int eye=0;eye<2;eye++)
                                {
                                    int y=(int)manifest.gaze.eyeCenters[eye][1]-4;
                                    int first=source.Width,last=-1;
                                    for (int x=(int)manifest.gaze.eyeCenters[eye][0]-50;
                                        x<(int)manifest.gaze.eyeCenters[eye][0]+50;x++)
                                    {
                                        Color c=source.GetPixel(x,y);
                                        if(c.B>50 && c.B-c.R>12 && c.B-c.G>4)
                                        { first=Math.Min(first,x);last=Math.Max(last,x); }
                                    }
                                    Require(last>first,outfit.Id+" missing iris samples");
                                    Color exposed=frame.GetPixel(dx<0?last:first,y);
                                    Require(exposed.R>180 && exposed.G>180 && exposed.B>180,
                                        outfit.Id+" old dark iris rim remains in exposed sclera");
                                }
                            }
                    }
                }
                Console.WriteLine("PASS: "+outfit.Id+" real painted iris details stay rigid; vacated rims become sclera");
            }
        }
        [STAThread]
        private static void Main(string[] args)
        {
            try { CheckRim(); CheckRigidIris(); CheckArtwork(args[0]); SaveArtReview(args[0]); }
            catch (Exception error) { Console.Error.WriteLine("FAIL: "+error); Environment.ExitCode=1; }
        }
    }
}
