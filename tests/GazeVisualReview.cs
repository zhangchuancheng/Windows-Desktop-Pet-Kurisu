using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace MakiseDesktopPet
{
    // Diagnostic exports from the production renderer, not screen recordings.
    internal static class GazeVisualReview
    {
        private static Rectangle EyeCrop(int height)
        {
            return new Rectangle((int)(310*height/1402.0), (int)(390*height/1402.0),
                (int)(430*height/1402.0), (int)(150*height/1402.0));
        }

        [STAThread]
        private static void Main(string[] args)
        {
            string root=args[0];
            int height=args.Length>1 ? int.Parse(args[1]) : 355;
            string output=Path.Combine(root,"test-artifacts",args.Length>2 ? args[2] : "gaze-rework");
            Directory.CreateDirectory(output);
            string frames=Path.Combine(output,"motion-"+height);
            Directory.CreateDirectory(frames);
            const int count=64;
            Bitmap[] motion=new Bitmap[count];
            for(int i=0;i<count;i++) motion[i]=new Bitmap(660,560);
            try
            {
                using(Bitmap sheet=new Bitmap(1650,1120))
                using(Bitmap half=new Bitmap(1650,1120))
                using(Graphics g=Graphics.FromImage(sheet))
                using(Graphics h=Graphics.FromImage(half))
                using(Font font=new Font("Arial",10))
                {
                    g.Clear(Color.White); h.Clear(Color.White);
                    g.InterpolationMode=h.InterpolationMode=InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode=h.PixelOffsetMode=PixelOffsetMode.Half;
                    int row=0;
                    foreach(var outfit in OutfitCatalog.All)
                    {
                        string directory=outfit.Id=="original" ? Path.Combine(root,"assets","sprites") :
                            Path.Combine(root,"assets","concepts","outfit-actions-2026-10-19",outfit.Id);
                        using(var idle=new IdleMotionAnimation(directory,directory,height,true,outfit.Id!="original"))
                        {
                            bool changed;
                            idle.Enter(0);
                            double[,] directions={{-1,0},{0,-1},{0,0},{0,1},{1,0}};
                            for(int col=0;col<5;col++)
                                for(int eyes=0;eyes<2;eyes++)
                                {
                                    Graphics canvas=eyes==0 ? g : h;
                                    canvas.DrawString(outfit.Id+" "+directions[col,0]+","+directions[col,1],
                                        font,Brushes.Black,col*330,row*140);
                                    Bitmap frame=idle.Render(1800,eyes,directions[col,0],directions[col,1],0,0,out changed);
                                    canvas.DrawImage(frame,new Rectangle(col*330,row*140+20,327,114),
                                        EyeCrop(height),GraphicsUnit.Pixel);
                                }
                            Stopwatch watch=new Stopwatch();
                            for(int i=0;i<count;i++)
                            {
                                double phase=2*Math.PI*i/(count-1);
                                double radius=Math.Min(1,Math.Min(i,count-1-i)/8.0);
                                int eyes=i==49 ? 2 : i==48 || i==50 ? 1 : 0;
                                watch.Start();
                                Bitmap frame=idle.Render(1800,eyes,radius*Math.Cos(phase),radius*Math.Sin(phase),0,0,out changed);
                                watch.Stop();
                                using(Graphics canvas=Graphics.FromImage(motion[i]))
                                {
                                    int x=row%2*330,y=row/2*140;
                                    canvas.FillRectangle(Brushes.White,x,y,330,140);
                                    canvas.DrawString(outfit.Id,font,Brushes.Black,x,y);
                                    canvas.InterpolationMode=InterpolationMode.NearestNeighbor;
                                    canvas.PixelOffsetMode=PixelOffsetMode.Half;
                                    canvas.DrawImage(frame,new Rectangle(x,y+20,327,114),EyeCrop(height),GraphicsUnit.Pixel);
                                }
                            }
                            Console.WriteLine(outfit.Id+" "+height+"px moving-gaze renderer: "+
                                (watch.Elapsed.TotalMilliseconds/count).ToString("F2")+" ms/frame");
                        }
                        row++;
                    }
                    sheet.Save(Path.Combine(output,"open-"+height+".png"));
                    half.Save(Path.Combine(output,"half-"+height+".png"));
                }
                for(int i=0;i<count;i++) motion[i].Save(Path.Combine(frames,i.ToString("D3")+".png"));
            }
            finally { foreach(Bitmap frame in motion) if(frame!=null) frame.Dispose(); }
        }
    }
}
