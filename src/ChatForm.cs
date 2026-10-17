using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net.Http;
using System.Threading;
using System.Windows.Forms;

namespace MakiseDesktopPet
{
    internal sealed class ChatForm : Form
    {
        private const int BaseWidth = 308;
        private const int BaseHeight = 110;
        private static readonly Color Cream = Color.FromArgb(252, 250, 246);
        private static readonly Color White = Color.FromArgb(255, 253, 249);
        private static readonly Color Ink = Color.FromArgb(63, 59, 58);
        private static readonly Color Accent = Color.FromArgb(188, 111, 111);
        private readonly OllamaChatClient client;
        private readonly Action<string> showReply;
        private readonly Action interact;
        private readonly Label title = new Label();
        private readonly Label status = new Label();
        private readonly TextBox input = new TextBox();
        private readonly Button send = new Button();
        private readonly Button close = new Button();
        private CancellationTokenSource pending;
        private Font titleFont;
        private Font bodyFont;
        private Font smallFont;
        private int uiScalePercent;

        internal ChatForm(OllamaChatClient client, Action<string> showReply, Action interact)
        {
            this.client = client;
            this.showReply = showReply;
            this.interact = interact;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Cream;
            KeyPreview = true;
            DoubleBuffered = true;

            title.Text = "和红莉栖聊两句";
            title.ForeColor = Ink;
            title.BackColor = Cream;
            title.TextAlign = ContentAlignment.MiddleLeft;
            close.Text = "×";
            close.FlatStyle = FlatStyle.Flat;
            close.FlatAppearance.BorderSize = 0;
            close.BackColor = Cream;
            close.ForeColor = Ink;
            close.TabStop = false;
            close.Click += delegate { HideChat(); };
            input.Multiline = true;
            input.MaxLength = 280;
            input.BorderStyle = BorderStyle.None;
            input.BackColor = White;
            input.ForeColor = Ink;
            input.KeyDown += OnInputKeyDown;
            input.TextChanged += delegate { interact(); };

            send.Text = "发送";
            send.FlatStyle = FlatStyle.Flat;
            send.FlatAppearance.BorderSize = 0;
            send.BackColor = Accent;
            send.ForeColor = Color.White;
            send.Cursor = Cursors.Hand;
            send.Click += delegate { SendCurrentMessage(); };

            status.Text = "回车发送 · Shift+回车换行";
            status.ForeColor = Color.FromArgb(139, 119, 116);
            status.BackColor = Cream;
            status.TextAlign = ContentAlignment.MiddleLeft;

            Controls.Add(title);
            Controls.Add(close);
            Controls.Add(input);
            Controls.Add(send);
            Controls.Add(status);
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) HideChat();
            };
            ApplyUiScale(100);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ClassStyle |= 0x00020000;
                return parameters;
            }
        }

        internal void ShowNear(Rectangle pet, bool topMost, int scalePercent)
        {
            TopMost = topMost;
            ApplyUiScale(scalePercent);
            Rectangle work = Screen.FromRectangle(pet).WorkingArea;
            int x = pet.Left - Width - 12;
            if (x < work.Left) x = pet.Right + 12;
            int y = pet.Bottom - Height;
            if (x + Width > work.Right)
            {
                x = pet.Left + (pet.Width - Width) / 2;
                y = pet.Top - Height - 12;
                if (y < work.Top) y = pet.Bottom + 12;
            }
            x = Math.Max(work.Left, Math.Min(work.Right - Width, x));
            y = Math.Max(work.Top, Math.Min(work.Bottom - Height, y));
            Location = new Point(x, y);
            if (!Visible) Show();
            else BringToFront();
            Activate();
            input.Focus();
            interact();
        }

        internal void ApplyUiScale(int percent)
        {
            if (uiScalePercent == percent) return;
            uiScalePercent = percent;
            float scale = percent / 100f * 1.2f;
            Font oldTitle = titleFont;
            Font oldBody = bodyFont;
            Font oldSmall = smallFont;
            titleFont = new Font("Microsoft YaHei UI", 14f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            bodyFont = new Font("Microsoft YaHei UI", 12f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            smallFont = new Font("Microsoft YaHei UI", 10f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            title.Font = titleFont;
            input.Font = bodyFont;
            send.Font = bodyFont;
            close.Font = titleFont;
            status.Font = smallFont;
            Size = new Size(P(BaseWidth, scale), P(BaseHeight, scale));
            title.Bounds = BoundsAt(29, 7, 242, 24, scale);
            close.Bounds = BoundsAt(278, 6, 24, 24, scale);
            input.Bounds = BoundsAt(20, 43, 214, 40, scale);
            send.Bounds = BoundsAt(250, 37, 48, 52, scale);
            status.Bounds = BoundsAt(17, 94, 274, 14, scale);
            using (GraphicsPath shape = PetMenuCanvas.RoundedRectangle(
                new RectangleF(0, 0, Width, Height), 14 * scale))
            {
                Region previous = Region;
                Region = new Region(shape);
                if (previous != null) previous.Dispose();
            }
            if (oldTitle != null) oldTitle.Dispose();
            if (oldBody != null) oldBody.Dispose();
            if (oldSmall != null) oldSmall.Dispose();
            KeepVisible();
            Invalidate();
        }

        internal void KeepVisible()
        {
            if (!Visible) return;
            Rectangle work = Screen.FromRectangle(Bounds).WorkingArea;
            Location = new Point(
                Math.Max(work.Left, Math.Min(work.Right - Width, Left)),
                Math.Max(work.Top, Math.Min(work.Bottom - Height, Top)));
        }

        private static int P(int value, float scale) { return (int)Math.Round(value * scale); }

        private static Rectangle BoundsAt(int x, int y, int width, int height, float scale)
        {
            return new Rectangle(P(x, scale), P(y, scale), P(width, scale), P(height, scale));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float scale = uiScalePercent / 100f * 1.2f;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.ScaleTransform(scale, scale);
            using (GraphicsPath card = PetMenuCanvas.RoundedRectangle(
                new RectangleF(0.5f, 0.5f, BaseWidth - 1, BaseHeight - 1), 14))
            using (GraphicsPath prompt = PetMenuCanvas.RoundedRectangle(
                new RectangleF(11, 35, 231, 54), 9))
            using (Pen border = new Pen(Color.FromArgb(183, 141, 139), 1))
            using (Pen softBorder = new Pen(Color.FromArgb(233, 223, 215), 1))
            using (SolidBrush white = new SolidBrush(White))
            using (SolidBrush dot = new SolidBrush(Accent))
            {
                g.DrawPath(border, card);
                g.FillPath(white, prompt);
                g.DrawPath(softBorder, prompt);
                g.FillEllipse(dot, 20, 17, 6, 6);
            }
        }

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                HideChat();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                SendCurrentMessage();
            }
        }

        private async void SendCurrentMessage()
        {
            string text = input.Text.Trim();
            if (text.Length == 0 || pending != null) return;
            interact();
            CancellationTokenSource request = new CancellationTokenSource();
            pending = request;
            send.Enabled = false;
            status.Text = "正在想……首次加载可能稍慢";
            try
            {
                string answer = await client.SendAsync(text, request.Token);
                if (IsDisposed || request.IsCancellationRequested) return;
                input.Clear();
                status.Text = "已回复 · 可以继续说";
                showReply(answer);
                input.Focus();
            }
            catch (OperationCanceledException)
            {
                if (!request.IsCancellationRequested && !IsDisposed)
                    status.Text = "等待超时，点发送可以重试";
            }
            catch (HttpRequestException)
            {
                if (!IsDisposed) status.Text = "没连上本机 Ollama，请先启动它";
            }
            catch (Exception error)
            {
                if (!IsDisposed) status.Text = error.Message;
            }
            finally
            {
                if (!IsDisposed) send.Enabled = true;
                if (pending == request) pending = null;
                request.Dispose();
            }
        }

        internal void HideChat()
        {
            if (pending != null) pending.Cancel();
            Hide();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideChat();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (pending != null) pending.Cancel();
                if (titleFont != null) titleFont.Dispose();
                if (bodyFont != null) bodyFont.Dispose();
                if (smallFont != null) smallFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
