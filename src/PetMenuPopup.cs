using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MakiseDesktopPet
{
    internal sealed class PetMenuPopup : ToolStripDropDown
    {
        private readonly PetMenuCanvas canvas;
        private readonly ToolStripControlHost host;
        private readonly Timer previewTimer;
        private readonly Action<int> previewSize;
        private readonly Action saveSize;
        private int appliedHeight;
        private bool sizeChanged;

        internal PetMenuPopup(Action<int> previewSize, Action saveSize, Action<bool> changeLayer,
            Action<int> changeUiScale, Action changeDialogue, Action hidePet, Action exitPet,
            Action closed)
        {
            this.previewSize = previewSize;
            this.saveSize = saveSize;
            canvas = new PetMenuCanvas();
            host = new ToolStripControlHost(canvas);
            host.AutoSize = false;
            host.Size = canvas.Size;
            host.Margin = Padding.Empty;
            host.Padding = Padding.Empty;
            AutoSize = false;
            Padding = Padding.Empty;
            Margin = Padding.Empty;
            Size = canvas.Size;
            BackColor = Color.FromArgb(252, 250, 246);
            DropShadowEnabled = true;
            AutoClose = true;
            Items.Add(host);

            previewTimer = new Timer();
            previewTimer.Interval = 120;
            previewTimer.Tick += delegate
            {
                ApplyPendingSize();
                previewTimer.Stop();
            };
            canvas.HeightChanged += delegate
            {
                if (canvas.SelectedHeight != appliedHeight && !previewTimer.Enabled)
                    previewTimer.Start();
            };
            canvas.HeightCommitted += CommitSize;
            canvas.PhraseSelected += delegate { CompleteAction(changeDialogue); };
            canvas.HideSelected += delegate { CompleteAction(hidePet); };
            canvas.ExitSelected += delegate { CompleteAction(exitPet); };
            canvas.LayerSelected += delegate(bool topmost)
            {
                CompleteAction(delegate { changeLayer(topmost); });
            };
            canvas.UiScaleSelected += delegate(int percent)
            {
                CommitSize();
                ResizeToCanvas();
                changeUiScale(percent);
            };
            Closed += delegate
            {
                CommitSize();
                closed();
            };
            Disposed += delegate { previewTimer.Dispose(); };
            UpdateRegion();
        }

        internal PetMenuCanvas Canvas { get { return canvas; } }

        internal void ShowAt(Point pointer, int height, bool topmost, int uiScalePercent)
        {
            previewTimer.Stop();
            appliedHeight = height;
            sizeChanged = false;
            canvas.Reset(height, topmost, uiScalePercent);
            ResizeToCanvas();
            Rectangle work = Screen.FromPoint(pointer).WorkingArea;
            int x = pointer.X + 4;
            int y = pointer.Y + 4;
            if (x + Width > work.Right) x = pointer.X - Width - 4;
            if (y + Height > work.Bottom) y = pointer.Y - Height - 4;
            x = Math.Max(work.Left, Math.Min(work.Right - Width, x));
            y = Math.Max(work.Top, Math.Min(work.Bottom - Height, y));
            Show(new Point(x, y));
        }

        private void ResizeToCanvas()
        {
            host.Size = canvas.Size;
            Size = canvas.Size;
            UpdateRegion();
            if (!Visible) return;
            Rectangle work = Screen.FromRectangle(Bounds).WorkingArea;
            int x = Math.Max(work.Left, Math.Min(work.Right - Width, Left));
            int y = Math.Max(work.Top, Math.Min(work.Bottom - Height, Top));
            Location = new Point(x, y);
        }

        private void ApplyPendingSize()
        {
            if (canvas.SelectedHeight == appliedHeight) return;
            previewSize(canvas.SelectedHeight);
            appliedHeight = canvas.SelectedHeight;
            sizeChanged = true;
        }

        private void CommitSize()
        {
            previewTimer.Stop();
            ApplyPendingSize();
            if (sizeChanged) saveSize();
            sizeChanged = false;
        }

        private void CompleteAction(Action action)
        {
            CommitSize();
            Close();
            action();
        }

        private void UpdateRegion()
        {
            using (GraphicsPath path = PetMenuCanvas.RoundedRectangle(
                new RectangleF(0, 0, Width, Height), 15 * canvas.UiScale))
            {
                Region previous = Region;
                Region = new Region(path);
                if (previous != null) previous.Dispose();
            }
        }
    }

    internal sealed class PetMenuCanvas : Control
    {
        private enum Target { None, Phrase, Slider, Top, Bottom, UiSmaller, UiLarger, Hide, Exit }

        private const int BaseWidth = 264;
        private const int BaseHeight = 346;

        private static readonly Color Cream = Color.FromArgb(252, 250, 246);
        private static readonly Color Ink = Color.FromArgb(63, 59, 58);
        private static readonly Color Accent = Color.FromArgb(188, 111, 111);
        private static readonly Color Muted = Color.FromArgb(155, 142, 136);
        private static readonly Color Rule = Color.FromArgb(233, 223, 215);
        private static readonly Color SegmentBackground = Color.FromArgb(238, 231, 224);
        private readonly Font regular = new Font("Microsoft YaHei UI", 13f, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Font bold = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel);
        private readonly Font small = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Font valueFont = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel);
        private Target hover;
        private Target pressed;
        private bool sliding;
        private int selectedHeight = 220;
        private bool selectedTopMost = true;
        private int selectedUiScalePercent = PetSettings.DefaultUiScalePercent;

        internal event Action HeightChanged;
        internal event Action HeightCommitted;
        internal event Action<bool> LayerSelected;
        internal event Action<int> UiScaleSelected;
        internal event Action PhraseSelected;
        internal event Action HideSelected;
        internal event Action ExitSelected;

        internal PetMenuCanvas()
        {
            Size = new Size((int)Math.Round(BaseWidth * UiScale),
                (int)Math.Round(BaseHeight * UiScale));
            BackColor = Cream;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        internal int SelectedHeight { get { return selectedHeight; } }
        internal int SelectedUiScalePercent { get { return selectedUiScalePercent; } }
        internal float UiScale { get { return selectedUiScalePercent / 100f * 1.2f; } }

        internal void Reset(int height, bool topmost, int uiScalePercent)
        {
            selectedHeight = Math.Max(PetSettings.MinimumHeight,
                Math.Min(PetSettings.MaximumHeight, height));
            selectedTopMost = topmost;
            SetUiScale(uiScalePercent);
            sliding = false;
            pressed = Target.None;
            hover = Target.None;
            Invalidate();
        }

        private void SetUiScale(int percent)
        {
            selectedUiScalePercent = Math.Max(PetSettings.MinimumUiScalePercent,
                Math.Min(PetSettings.MaximumUiScalePercent, percent));
            Size = new Size((int)Math.Round(BaseWidth * UiScale),
                (int)Math.Round(BaseHeight * UiScale));
            Invalidate();
        }

        private void StepUiScale(int direction)
        {
            int previous = selectedUiScalePercent;
            SetUiScale(previous + direction * 10);
            if (previous != selectedUiScalePercent && UiScaleSelected != null)
                UiScaleSelected(selectedUiScalePercent);
        }

        private Point LogicalPoint(Point point)
        {
            return new Point((int)Math.Floor(point.X / UiScale),
                (int)Math.Floor(point.Y / UiScale));
        }

        private void SetHeight(int height)
        {
            height = Math.Max(PetSettings.MinimumHeight,
                Math.Min(PetSettings.MaximumHeight, height));
            if (height == selectedHeight) return;
            selectedHeight = height;
            Invalidate();
            if (HeightChanged != null) HeightChanged();
        }

        private void SetHeightFromPointer(int x)
        {
            double fraction = Math.Max(0, Math.Min(1, (x - 22) / 220.0));
            SetHeight(PetSettings.MinimumHeight + (int)Math.Round(fraction *
                (PetSettings.MaximumHeight - PetSettings.MinimumHeight)));
        }

        private Target HitTest(Point point)
        {
            if (point.X < 10 || point.X >= 254) return Target.None;
            if (point.Y >= 39 && point.Y < 75) return Target.Phrase;
            if (point.Y >= 99 && point.Y < 128) return Target.Slider;
            if (point.Y >= 177 && point.Y < 211)
                return point.X < 132 ? Target.Top : Target.Bottom;
            if (point.Y >= 222 && point.Y < 258)
            {
                if (point.X >= 154 && point.X < 181)
                    return selectedUiScalePercent > PetSettings.MinimumUiScalePercent
                        ? Target.UiSmaller : Target.None;
                if (point.X >= 221 && point.X < 248)
                    return selectedUiScalePercent < PetSettings.MaximumUiScalePercent
                        ? Target.UiLarger : Target.None;
                return Target.None;
            }
            if (point.Y >= 264 && point.Y < 300) return Target.Hide;
            if (point.Y >= 300 && point.Y < 336) return Target.Exit;
            return Target.None;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            pressed = HitTest(LogicalPoint(e.Location));
            if (pressed == Target.None) return;
            Capture = true;
            if (pressed == Target.Slider)
            {
                sliding = true;
                SetHeightFromPointer(LogicalPoint(e.Location).X);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (sliding) SetHeightFromPointer(LogicalPoint(e.Location).X);
            Target next = sliding ? Target.Slider : HitTest(LogicalPoint(e.Location));
            if (hover != next)
            {
                hover = next;
                Cursor = next == Target.None ? Cursors.Default : Cursors.Hand;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            Target activated = pressed;
            pressed = Target.None;
            Capture = false;
            if (sliding)
            {
                sliding = false;
                SetHeightFromPointer(LogicalPoint(e.Location).X);
                if (HeightCommitted != null) HeightCommitted();
                return;
            }
            if (activated == Target.None || activated != HitTest(LogicalPoint(e.Location))) return;
            if (activated == Target.Phrase && PhraseSelected != null) PhraseSelected();
            else if (activated == Target.UiSmaller) StepUiScale(-1);
            else if (activated == Target.UiLarger) StepUiScale(1);
            else if (activated == Target.Hide && HideSelected != null) HideSelected();
            else if (activated == Target.Exit && ExitSelected != null) ExitSelected();
            else if (activated == Target.Top || activated == Target.Bottom)
            {
                selectedTopMost = activated == Target.Top;
                Invalidate();
                if (LayerSelected != null) LayerSelected(selectedTopMost);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (HitTest(LogicalPoint(e.Location)) != Target.Slider) return;
            SetHeight(selectedHeight + (e.Delta > 0 ? 10 : -10));
            if (HeightCommitted != null) HeightCommitted();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (sliding || hover == Target.None) return;
            hover = Target.None;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Cream);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.ScaleTransform(UiScale, UiScale);
            using (GraphicsPath card = RoundedRectangle(new RectangleF(0.5f, 0.5f,
                BaseWidth - 1, BaseHeight - 1), 14))
            using (Pen border = new Pen(Color.FromArgb(183, 141, 139), 1))
                g.DrawPath(border, card);

            using (SolidBrush dot = new SolidBrush(Accent)) g.FillEllipse(dot, 20, 15, 6, 6);
            DrawText(g, "桌宠设置", small, Color.FromArgb(139, 119, 116), new Rectangle(32, 9, 120, 22));

            DrawRow(g, Target.Phrase, 39, "换一句话");
            DrawSpeechIcon(g, 24, 50);
            DrawRule(g, 75);
            DrawText(g, "角色大小", bold, Ink, new Rectangle(20, 80, 110, 23));
            DrawText(g, selectedHeight + " px", valueFont, Accent,
                new Rectangle(174, 80, 70, 23), TextFormatFlags.Right);
            DrawSlider(g);
            DrawText(g, "小 · " + PetSettings.MinimumHeight, small, Muted, new Rectangle(20, 128, 95, 20));
            DrawText(g, PetSettings.MaximumHeight + " · 大", small, Muted,
                new Rectangle(149, 128, 95, 20), TextFormatFlags.Right);

            DrawText(g, "窗口层级", bold, Ink, new Rectangle(20, 154, 120, 23));
            DrawSegments(g);
            DrawRule(g, 216);
            DrawUiScale(g);
            DrawRule(g, 259);
            DrawRow(g, Target.Hide, 264, "隐藏到托盘");
            DrawTrayIcon(g, 24, 275);
            DrawRow(g, Target.Exit, 300, "退出桌宠");
            DrawPowerIcon(g, 24, 312);
        }

        private void DrawUiScale(Graphics g)
        {
            DrawText(g, "界面大小", regular, Ink, new Rectangle(20, 226, 125, 28));
            DrawText(g, selectedUiScalePercent + "%", small, Accent,
                new Rectangle(181, 226, 39, 27), TextFormatFlags.HorizontalCenter);
            DrawUiScaleButton(g, Target.UiSmaller, 154,
                selectedUiScalePercent > PetSettings.MinimumUiScalePercent, false);
            DrawUiScaleButton(g, Target.UiLarger, 221,
                selectedUiScalePercent < PetSettings.MaximumUiScalePercent, true);
        }

        private void DrawUiScaleButton(Graphics g, Target target, int x,
            bool enabled, bool plus)
        {
            Color background = hover == target && enabled
                ? Color.FromArgb(240, 224, 220) : SegmentBackground;
            FillRounded(g, new RectangleF(x, 226, 27, 27), 7, background);
            using (Pen pen = new Pen(enabled ? Accent : Muted, 1.6f))
            {
                g.DrawLine(pen, x + 8, 239.5f, x + 19, 239.5f);
                if (plus) g.DrawLine(pen, x + 13.5f, 234, x + 13.5f, 245);
            }
        }

        private void DrawRow(Graphics g, Target target, int top, string text)
        {
            if (hover == target)
                FillRounded(g, new RectangleF(10, top, 244, 36), 9,
                    Color.FromArgb(246, 237, 232));
            DrawText(g, text, regular, target == Target.Exit ? Color.FromArgb(164, 93, 96) : Ink,
                new Rectangle(49, top + 6, 180, 24));
        }

        private void DrawSlider(Graphics g)
        {
            float fraction = (selectedHeight - PetSettings.MinimumHeight) /
                (float)(PetSettings.MaximumHeight - PetSettings.MinimumHeight);
            float thumbX = 22 + 220 * fraction;
            FillRounded(g, new RectangleF(20, 114, 224, 5), 2.5f,
                Color.FromArgb(231, 219, 213));
            if (thumbX > 22)
                FillRounded(g, new RectangleF(20, 114, thumbX - 18, 5), 2.5f, Accent);
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(40, 65, 47, 47)))
            using (SolidBrush white = new SolidBrush(Color.FromArgb(255, 250, 245)))
            using (SolidBrush fill = new SolidBrush(Accent))
            using (Pen edge = new Pen(Color.FromArgb(189, 133, 132), 1))
            {
                g.FillEllipse(shadow, thumbX - 8, 110, 18, 18);
                g.FillEllipse(white, thumbX - 9, 108, 18, 18);
                g.DrawEllipse(edge, thumbX - 9, 108, 18, 18);
                g.FillEllipse(fill, thumbX - 4, 113, 8, 8);
            }
        }

        private void DrawSegments(Graphics g)
        {
            FillRounded(g, new RectangleF(19, 177, 226, 34), 9, SegmentBackground);
            int selectedLeft = selectedTopMost ? 22 : 132;
            FillRounded(g, new RectangleF(selectedLeft, 180, 110, 28), 7,
                Color.FromArgb(251, 246, 240));
            if (hover == Target.Top && !selectedTopMost)
                FillRounded(g, new RectangleF(22, 180, 110, 28), 7,
                    Color.FromArgb(247, 241, 235));
            if (hover == Target.Bottom && selectedTopMost)
                FillRounded(g, new RectangleF(132, 180, 110, 28), 7,
                    Color.FromArgb(247, 241, 235));
            DrawText(g, "置顶", selectedTopMost ? bold : regular,
                selectedTopMost ? Color.FromArgb(164, 86, 89) : Color.FromArgb(116, 106, 103),
                new Rectangle(22, 181, 110, 26), TextFormatFlags.HorizontalCenter);
            DrawText(g, "置底", selectedTopMost ? regular : bold,
                selectedTopMost ? Color.FromArgb(116, 106, 103) : Color.FromArgb(164, 86, 89),
                new Rectangle(132, 181, 110, 26), TextFormatFlags.HorizontalCenter);
        }

        private static void DrawRule(Graphics g, int y)
        {
            using (Pen line = new Pen(Rule, 1)) g.DrawLine(line, 19, y + 0.5f, 245, y + 0.5f);
        }

        private static void DrawText(Graphics g, string text, Font font, Color color, Rectangle bounds)
        {
            DrawText(g, text, font, color, bounds, TextFormatFlags.Left);
        }

        private static void DrawText(Graphics g, string text, Font font, Color color,
            Rectangle bounds, TextFormatFlags align)
        {
            using (SolidBrush brush = new SolidBrush(color))
            using (StringFormat format = new StringFormat())
            {
                format.LineAlignment = StringAlignment.Center;
                format.Alignment = (align & TextFormatFlags.Right) != 0 ? StringAlignment.Far :
                    (align & TextFormatFlags.HorizontalCenter) != 0 ? StringAlignment.Center :
                    StringAlignment.Near;
                format.FormatFlags = StringFormatFlags.NoWrap;
                format.Trimming = StringTrimming.EllipsisCharacter;
                g.DrawString(text, font, brush, bounds, format);
            }
        }

        private static void DrawSpeechIcon(Graphics g, int x, int y)
        {
            using (Pen pen = new Pen(Color.FromArgb(158, 119, 116), 1.3f))
            using (GraphicsPath bubble = RoundedRectangle(new RectangleF(x - 5, y - 1, 13, 9), 2))
            {
                g.DrawPath(pen, bubble);
                g.DrawLines(pen, new[] { new Point(x - 2, y + 8), new Point(x - 2, y + 10),
                    new Point(x + 1, y + 8) });
                g.DrawLine(pen, x - 2, y + 2, x + 5, y + 2);
                g.DrawLine(pen, x - 2, y + 5, x + 3, y + 5);
            }
        }

        private static void DrawTrayIcon(Graphics g, int x, int y)
        {
            using (Pen pen = new Pen(Color.FromArgb(158, 119, 116), 1.3f))
            {
                g.DrawRectangle(pen, x - 5, y + 2, 12, 8);
                g.DrawLine(pen, x - 2, y - 1, x + 4, y - 1);
                g.DrawLine(pen, x - 1, y + 5, x + 3, y + 5);
            }
        }

        private static void DrawPowerIcon(Graphics g, int x, int y)
        {
            using (Pen pen = new Pen(Color.FromArgb(180, 120, 121), 1.4f))
            {
                g.DrawArc(pen, x - 6, y - 3, 13, 13, -52, 284);
                g.DrawLine(pen, x, y - 5, x, y + 2);
            }
        }

        private static void FillRounded(Graphics g, RectangleF bounds, float radius, Color color)
        {
            using (GraphicsPath path = RoundedRectangle(bounds, radius))
            using (SolidBrush brush = new SolidBrush(color)) g.FillPath(brush, path);
        }

        internal static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
        {
            float diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                regular.Dispose();
                bold.Dispose();
                small.Dispose();
                valueFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
