using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace MakiseDesktopPet
{
    // Native-resolution eye layers: opaque sclera, a rigid iris, and a fixed
    // eyelid opening. Never add a shifted colour difference to the old iris.
    internal sealed class IrisSurface
    {
        private readonly Rectangle bounds;
        private readonly double[] texture;
        private readonly byte[] opening;
        private readonly double[] tops;
        internal Rectangle Bounds { get { return bounds; } }
        internal double Coverage(int x, int y)
        { return opening[((y-bounds.Top)*bounds.Width+x-bounds.Left)*4+3]/255.0; }
        internal double Background(int x,int y)
        { return 250-55*Math.Exp(-Math.Max(0,y-tops[x-bounds.Left])/7.0); }

        internal IrisSurface(byte[] source, int width, int height,
            EyeGazeLayer.Region eye, double paddingX, double paddingY)
        {
            double ew=eye.Right-eye.Left;
            int margin=(int)Math.Ceiling(Math.Max(paddingX,paddingY)+ew*.2)+3;
            bounds=Rectangle.Intersect(new Rectangle(0,0,width,height),Rectangle.FromLTRB(
                (int)eye.Left-margin,(int)eye.Top-margin,(int)eye.Right+margin+1,(int)eye.Bottom+margin+1));
            texture=new double[bounds.Width*bounds.Height*4];
            tops=new double[bounds.Width];
            List<Point> visible=new List<Point>(),blue=new List<Point>();
            for(int y=bounds.Top;y<bounds.Bottom;y++)
                for(int x=bounds.Left;x<bounds.Right;x++)
                {
                    if(!eye.SearchContains(x,y)) continue;
                    int p=(y*width+x)*4;
                    int b=source[p],g=source[p+1],r=source[p+2];
                    bool isBlue=b>50 && b-r>12 && b-g>4;
                    bool white=r>120 && Math.Abs(b-r)<18 && Math.Abs(g-r)<18;
                    if(source[p+3]<240) continue;
                    if(isBlue) blue.Add(new Point(x-bounds.Left,y-bounds.Top));
                    if(isBlue||white) visible.Add(new Point(x-bounds.Left,y-bounds.Top));
                }
            if (blue.Count < 3)
            {
                opening = new byte[bounds.Width * bounds.Height * 4];
                return;
            }
            opening=Mask(visible,Math.Max(1,ew*.045));
            byte[] iris=Mask(blue,Math.Max(1,ew*.08));
            int irisLeft=bounds.Right,irisRight=bounds.Left,irisBottom=bounds.Top;
            foreach(Point point in blue)
            {
                irisLeft=Math.Min(irisLeft,point.X+bounds.Left);
                irisRight=Math.Max(irisRight,point.X+bounds.Left);
                irisBottom=Math.Max(irisBottom,point.Y+bounds.Top);
            }
            // Preserve the lashes, including their antialiasing, outside the iris.
            // A near-neutral dark edge there belongs to the fixed eyelid.
            for(int y=bounds.Top;y<bounds.Bottom;y++)
                for(int x=bounds.Left;x<bounds.Right;x++)
                {
                    int q=((y-bounds.Top)*bounds.Width+x-bounds.Left)*4,p=(y*width+x)*4;
                    if((x<irisLeft-ew*.065 || x>irisRight+ew*.065 || y>irisBottom+ew*.065)
                        && source[p+2]<115) opening[q+3]=0;
                    if(y<eye.UpperAt(x)-ew*.035 && source[p]<95) opening[q+3]=0;
                    if(y<=eye.UpperAt(x)+ew*.02 && source[p]<45) opening[q+3]=0;
                }
            for(int x=bounds.Left;x<bounds.Right;x++)
            {
                int first=bounds.Top,last=bounds.Bottom-1;
                while(first<=last && Coverage(x,first)<.95) first++;
                while(last>=first && Coverage(x,last)<.95) last--;
                tops[x-bounds.Left]=first;
                if(first>last) continue;
                // Use a coherent upper boundary, not individual colour pixels.
                // Extend the iris behind the lid, never the eyelash itself.
                int start=Math.Min(last,first+Math.Max(1,(int)(ew*.025)));
                while(start<last)
                {
                    int p=(start*width+x)*4;
                    if(source[p+2]-source[p]<=8) break;
                    start++;
                }
                for(int y=bounds.Top;y<bounds.Bottom;y++)
                {
                    if(y>last) continue;
                    int sampleY=Math.Max(start,y);
                    int sample=((sampleY-bounds.Top)*bounds.Width+x-bounds.Left)*4;
                    int q=((y-bounds.Top)*bounds.Width+x-bounds.Left)*4;
                    int p=(sampleY*width+x)*4;
                    double a=iris[sample+3]/255.0;
                    if(source[p+2]-source[p]>8 &&
                        (a<.95 || sampleY>irisBottom || x<irisLeft || x>irisRight)) a=0;
                    texture[q+3]=a;
                    for(int c=0;c<3;c++) texture[q+c]=source[p+c]*a;
                }
            }
        }
        private byte[] Mask(List<Point> points,double stroke)
        {
            using(Bitmap mask=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppPArgb))
            {
                using(Graphics g=Graphics.FromImage(mask))
                using(Pen edge=new Pen(Color.White,(float)stroke))
                {
                    g.SmoothingMode=SmoothingMode.AntiAlias;
                    Point[] hull=ConvexHull(points);
                    if(hull.Length>=3) {g.FillPolygon(Brushes.White,hull);g.DrawPolygon(edge,hull);}
                }
                return EyeGazeLayer.Read(mask);
            }
        }
        private static long Cross(Point a,Point b,Point c)
        { return (long)(b.X-a.X)*(c.Y-a.Y)-(long)(b.Y-a.Y)*(c.X-a.X); }
        private static Point[] ConvexHull(List<Point> points)
        {
            points.Sort(delegate(Point a,Point b) { return a.X==b.X?a.Y.CompareTo(b.Y):a.X.CompareTo(b.X); });
            List<Point> hull=new List<Point>();
            foreach(Point p in points)
            {
                while(hull.Count>=2 && Cross(hull[hull.Count-2],hull[hull.Count-1],p)<=0) hull.RemoveAt(hull.Count-1);
                hull.Add(p);
            }
            int lower=hull.Count;
            for(int i=points.Count-2;i>=0;i--)
            {
                Point p=points[i];
                while(hull.Count>lower && Cross(hull[hull.Count-2],hull[hull.Count-1],p)<=0) hull.RemoveAt(hull.Count-1);
                hull.Add(p);
            }
            if(hull.Count>0) hull.RemoveAt(hull.Count-1);
            return hull.ToArray();
        }
        internal double Sample(double x,double y,int channel)
        {
            x-=bounds.Left; y-=bounds.Top;
            int ix=(int)Math.Floor(x),iy=(int)Math.Floor(y);
            if(ix<0 || iy<0 || ix>=bounds.Width-1 || iy>=bounds.Height-1) return 0;
            double fx=x-ix,fy=y-iy;
            int a=(iy*bounds.Width+ix)*4+channel,b=a+bounds.Width*4;
            return (texture[a]*(1-fx)+texture[a+4]*fx)*(1-fy)+(texture[b]*(1-fx)+texture[b+4]*fx)*fy;
        }
    }
}
