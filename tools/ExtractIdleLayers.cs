// Asset preparation only. Not included in the desktop application's build.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

internal static class ExtractIdleLayers
{
    const int W = 1122, H = 1402;
    static string root;
    static byte[] Read(string file)
    {
        using (Bitmap image = new Bitmap(Path.Combine(root, "sources", file)))
        {
            if (image.Width != W || image.Height != H) throw new Exception("Canvas mismatch: " + file);
            return Bytes(image);
        }
    }
    static byte[] Bytes(Bitmap image)
    {
        byte[] result = new byte[W * H * 4];
        BitmapData data = image.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try { for (int y = 0; y < H; y++) Marshal.Copy(IntPtr.Add(data.Scan0, data.Stride * y), result, y * W * 4, W * 4); }
        finally { image.UnlockBits(data); }
        return result;
    }
    static Bitmap Image(byte[] data)
    {
        Bitmap result = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        BitmapData bits = result.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try { for (int y = 0; y < H; y++) Marshal.Copy(data, y * W * 4, IntPtr.Add(bits.Scan0, bits.Stride * y), W * 4); }
        finally { result.UnlockBits(bits); }
        return result;
    }
    static void Save(string file, byte[] data)
    {
        using (Bitmap image = Image(data)) image.Save(Path.Combine(root, file), ImageFormat.Png);
    }
    static byte[] Mask(GraphicsPath path, bool soft)
    {
        using (Bitmap image = new Bitmap(W, H, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(image))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (soft)
                for (int width = 12; width >= 2; width -= 2)
                    using (Pen p = new Pen(Color.FromArgb(30, 255, 255, 255), width)) g.DrawPath(p, path);
            g.FillPath(Brushes.White, path);
            return Bytes(image);
        }
    }
    static GraphicsPath HeadPath()
    {
        GraphicsPath p = new GraphicsPath();
        p.AddLines(new PointF[] { new PointF(0,0), new PointF(W,0), new PointF(W,300), new PointF(900,300) });
        p.AddBezier(900,300, 900,430, 853,560, 810,620);
        p.AddBezier(810,620, 775,665, 743,691, 709,705);
        p.AddBezier(709,705, 735,670, 746,608, 750,576);
        p.AddBezier(750,576, 704,613, 645,638, 599,647);
        p.AddBezier(599,647, 533,663, 454,652, 400,625);
        p.AddBezier(400,625, 405,655, 423,690, 435,705);
        p.AddBezier(435,705, 371,691, 306,633, 275,560);
        p.AddBezier(275,560,260,590,250,608,242,615);
        p.AddLine(242,615,0,615);
        p.CloseFigure(); return p;
    }
    static GraphicsPath EyePath()
    {
        GraphicsPath p = new GraphicsPath();
        p.AddPolygon(new Point[] { new Point(342,469),new Point(379,442),new Point(437,431),new Point(486,438),new Point(517,463),new Point(523,536),new Point(494,572),new Point(437,581),new Point(390,570),new Point(357,541) });
        p.AddPolygon(new Point[] { new Point(608,407),new Point(641,385),new Point(699,371),new Point(750,374),new Point(777,394),new Point(775,466),new Point(752,512),new Point(704,532),new Point(653,521),new Point(620,491) });
        return p;
    }
    static void Copy(byte[] from, byte[] to, int i) { Buffer.BlockCopy(from, i, to, i, 4); }
    static byte[] Cut(byte[] source, byte[] mask)
    {
        byte[] result = (byte[])source.Clone();
        for (int i=0;i<result.Length;i+=4) result[i+3]=(byte)((result[i+3]*mask[i+3]+127)/255);
        return result;
    }
    static bool Hair(byte[] data,int i)
    {
        int r=data[i+2], g=data[i+1], b=data[i];
        return r>g*1.65 && r>b*1.3 && r>55;
    }
    static bool HairOrOutline(byte[] data,int x,int y)
    {
        int i=(y*W+x)*4;
        if(Hair(data,i)) return true;
        if(data[i+2]>65 || data[i+1]>65 || data[i]>95) return false;
        for(int dy=-7;dy<=7;dy+=2) for(int dx=-7;dx<=7;dx+=2)
        {
            int xx=x+dx, yy=y+dy;
            if(xx>=0 && xx<W && yy>=0 && yy<H && Hair(data,(yy*W+xx)*4)) return true;
        }
        return false;
    }
    static void DrawTurned(Graphics g, Bitmap image, float angle, float pivotX, float pivotY, float dx, float dy)
    {
        GraphicsState saved=g.Save();
        g.TranslateTransform(pivotX+dx,pivotY+dy);
        g.RotateTransform(angle);
        g.TranslateTransform(-pivotX,-pivotY);
        g.DrawImageUnscaled(image,0,0);
        g.Restore(saved);
    }
    static void Main(string[] args)
    {
        root=Path.GetFullPath(args[0]);
        byte[] original=Read("idle-original.png"), blank=Read("face-clean.png"), half=Read("eyes-half-reference.png"), closed=Read("blink-original.png");
        byte[] body=Read("body-fill.png"), backFill=Read("rear-hair-fill.png");
        byte[] headMask, eyeMask;
        using(GraphicsPath p=HeadPath()) headMask=Mask(p,false);
        using(GraphicsPath p=EyePath()) eyeMask=Mask(p,true);
        Save("sources/mask-head.png",headMask); Save("sources/mask-eyes.png",eyeMask);
        byte[] head=Cut(original,headMask);
        for(int i=0;i<head.Length;i+=4)
        {
            int alpha=eyeMask[i+3];
            for(int c=0;c<3;c++) head[i+c]=(byte)((head[i+c]*(255-alpha)+blank[i+c]*alpha+127)/255);
        }
        Save("head-base.png",head);
        Save("eyes-open.png",Cut(original,eyeMask));
        Save("eyes-half.png",Cut(half,eyeMask));
        Save("eyes-closed.png",Cut(closed,eyeMask));

        // Use the generated body as a completed silhouette, then restore source
        // pixels in its visible interior. Generated shoulder/neck overlap remains.
        for(int y=630;y<H;y++) for(int x=0;x<W;x++)
        {
            int i=(y*W+x)*4;
            if(body[i+3]>250 && original[i+3]>250 && headMask[i+3]==0 &&
                (!Hair(original,i) || (x>525 && x<645 && y<900))) Copy(original,body,i);
        }
        Save("body.png",body);

        byte[] left=new byte[original.Length], right=new byte[original.Length];
        // Back hair keeps original visible pixels. Generated fill is used only
        // in opaque regions hidden by the stationary head/body, with root overlap.
        for(int y=300;y<1008;y++) for(int x=0;x<W;x++)
        {
            int i=(y*W+x)*4;
            byte[] target=x<560?left:right;
            bool covered=headMask[i+3]>0 || body[i+3]>0;
            if(!covered)
            {
                if(HairOrOutline(original,x,y)) Copy(original,target,i);
            }
            else if(original[i+3]>250 && backFill[i+3]>0 && y<1008)
            {
                // Taper hidden roots inward. A rectangular crop at the original
                // outer head contour would show corners when the head rotates.
                bool rootInside=y>=600 || (x>420-(y-300)*0.70 && x<700+(y-300)*1.80);
                if(rootInside) Copy(backFill,target,i);
            }
            // Generous center overlap hidden beneath the torso, with no exposed seam.
            if(x>=535 && x<585 && y>=540 && y<1000 && original[i+3]>250 && backFill[i+3]>0)
            {
                Copy(backFill,left,i); Copy(backFill,right,i);
            }
        }
        Save("hair-back-left.png",left); Save("hair-back-right.png",right);
        using(Bitmap l=Image(left)) using(Bitmap r=Image(right)) using(Bitmap b=Image(body)) using(Bitmap h=Image(head))
        {
            string[] names={"open","half","closed"};
            using(Bitmap sheet=new Bitmap(1050,650,PixelFormat.Format32bppArgb))
            using(Graphics g=Graphics.FromImage(sheet))
            using(Font title=new Font("Microsoft YaHei",14,FontStyle.Regular,GraphicsUnit.Pixel))
            {
                g.Clear(Color.FromArgb(243,240,233));
                g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                for(int n=0;n<3;n++)
                using(Bitmap eyes=new Bitmap(Path.Combine(root,"eyes-"+names[n]+".png")))
                using(Bitmap composed=new Bitmap(W,H,PixelFormat.Format32bppArgb))
                using(Graphics cg=Graphics.FromImage(composed))
                {
                    cg.DrawImageUnscaled(l,0,0); cg.DrawImageUnscaled(r,0,0); cg.DrawImageUnscaled(b,0,0); cg.DrawImageUnscaled(h,0,0); cg.DrawImageUnscaled(eyes,0,0);
                    composed.Save(Path.Combine(root,"preview-"+names[n]+".png"));
                    g.DrawImage(composed,new Rectangle(n*350+35,30,280,350));
                    g.DrawString(new[]{"睁眼合成","半闭眼合成","闭眼合成"}[n],title,Brushes.DimGray,n*350+120,392);
                }
                Bitmap[] layers={l,r,b,h};
                string[] labels={"左后发","右后发","身体与颈部","头部底层（眼睛另叠）"};
                for(int n=0;n<4;n++)
                {
                    g.DrawImage(layers[n],new Rectangle(n*255+40,435,152,190));
                    g.DrawString(labels[n],title,Brushes.DimGray,n*255+40,628);
                }
                sheet.Save(Path.Combine(root,"preview-sheet.png"));
            }
            using(Bitmap sheet=new Bitmap(1050,430,PixelFormat.Format32bppArgb))
            using(Graphics g=Graphics.FromImage(sheet))
            using(Font title=new Font("Microsoft YaHei",14,FontStyle.Regular,GraphicsUnit.Pixel))
            using(Bitmap eyes=new Bitmap(Path.Combine(root,"eyes-open.png")))
            {
                g.Clear(Color.FromArgb(230,235,239));
                g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                for(int n=0;n<3;n++)
                using(Bitmap composed=new Bitmap(W,H,PixelFormat.Format32bppArgb))
                using(Graphics cg=Graphics.FromImage(composed))
                {
                    float turn=n-1;
                    cg.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    DrawTurned(cg,l,-turn*1.5f,350,550,0,0);
                    DrawTurned(cg,r,-turn*1.5f,800,540,0,0);
                    cg.DrawImageUnscaled(b,0,0);
                    DrawTurned(cg,h,turn*1.2f,580,645,turn*4,-3);
                    DrawTurned(cg,eyes,turn*1.2f,580,645,turn*4,-3);
                    composed.Save(Path.Combine(root,"check-overlap-"+n+".png"));
                    g.DrawImage(composed,new Rectangle(n*350+35,20,280,350));
                    g.DrawString(new[]{"小幅偏转检查 A","颈部上移检查","小幅偏转检查 B"}[n],title,Brushes.DimGray,n*350+100,385);
                }
                sheet.Save(Path.Combine(root,"check-overlap-sheet.png"));
            }
        }
        Console.WriteLine("Created seven aligned transparent layers, three composites, masks and preview sheet.");
    }
}
