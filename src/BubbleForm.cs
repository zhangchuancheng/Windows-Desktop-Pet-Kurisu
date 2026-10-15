using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace MakiseDesktopPet
{
    internal enum BubblePlacement { Left, Right, Above, Below }

    internal sealed class BubbleForm : LayeredForm
    {
        private readonly Font font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Regular, GraphicsUnit.Point);
        private Bitmap frame;

        internal BubbleForm() : base(true) { }

        internal void Render(string text, BubblePlacement placement)
        {
            const int bodyWidth = 270;
            int bodyHeight;
            using (Bitmap measureBitmap = new Bitmap(1, 1))
            using (Graphics measure = Graphics.FromImage(measureBitmap))
                bodyHeight = Math.Max(64, (int)Math.Ceiling(measure.MeasureString(text, font, 232).Height) + 28);

            bool side = placement == BubblePlacement.Left || placement == BubblePlacement.Right;
            int width = side ? 296 : 282;
            int height = side ? bodyHeight + 12 : bodyHeight + 26;
            Bitmap next = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            using (Graphics graphics = Graphics.FromImage(next))
            {
                graphics.PageUnit = GraphicsUnit.Pixel;
                graphics.PageScale = 1f;
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                Rectangle body;
                if (placement == BubblePlacement.Left) body = new Rectangle(5, 5, bodyWidth, bodyHeight);
                else if (placement == BubblePlacement.Right) body = new Rectangle(19, 5, bodyWidth, bodyHeight);
                else if (placement == BubblePlacement.Above) body = new Rectangle(5, 5, bodyWidth, bodyHeight);
                else body = new Rectangle(5, 19, bodyWidth, bodyHeight);

                using (GraphicsPath shape = RoundedRectangle(body, 16))
                using (GraphicsPath tail = Tail(body, placement))
                using (GraphicsPath shadow = new GraphicsPath())
                using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(32, 49, 42, 40)))
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(252, 250, 246, 240)))
                using (Pen border = new Pen(Color.FromArgb(218, 183, 141, 139), 1.1f))
                using (SolidBrush accent = new SolidBrush(Color.FromArgb(188, 111, 111)))
                using (SolidBrush ink = new SolidBrush(Color.FromArgb(63, 59, 58)))
                {
                    shadow.AddPath(shape, false);
                    shadow.AddPath(tail, false);
                    using (Matrix offset = new Matrix())
                    {
                        offset.Translate(2, 3);
                        shadow.Transform(offset);
                    }
                    graphics.FillPath(shadowBrush, shadow);
                    graphics.FillPath(fill, shape);
                    graphics.FillPath(fill, tail);
                    graphics.DrawPath(border, shape);
                    graphics.DrawPath(border, tail);
                    graphics.FillEllipse(accent, body.Left + 13, body.Top + 15, 6, 6);
                    graphics.DrawString(text, font, ink,
                        new RectangleF(body.Left + 27, body.Top + 13, body.Width - 39, body.Height - 21));
                }
            }
            Bitmap old = frame;
            frame = next;
            SetFrame(next);
            if (old != null) old.Dispose();
        }

        private static GraphicsPath RoundedRectangle(Rectangle rectangle, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath Tail(Rectangle body, BubblePlacement placement)
        {
            Point[] points;
            int middleY = body.Top + body.Height / 2;
            int middleX = body.Left + body.Width / 2;
            if (placement == BubblePlacement.Left)
                points = new[] { new Point(body.Right - 1, middleY - 9), new Point(body.Right + 14, middleY), new Point(body.Right - 1, middleY + 9) };
            else if (placement == BubblePlacement.Right)
                points = new[] { new Point(body.Left + 1, middleY - 9), new Point(body.Left - 14, middleY), new Point(body.Left + 1, middleY + 9) };
            else if (placement == BubblePlacement.Above)
                points = new[] { new Point(middleX - 9, body.Bottom - 1), new Point(middleX, body.Bottom + 14), new Point(middleX + 9, body.Bottom - 1) };
            else
                points = new[] { new Point(middleX - 9, body.Top + 1), new Point(middleX, body.Top - 14), new Point(middleX + 9, body.Top + 1) };
            GraphicsPath path = new GraphicsPath();
            path.AddPolygon(points);
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                font.Dispose();
                if (frame != null) frame.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
