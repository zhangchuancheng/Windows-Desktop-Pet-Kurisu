using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MakiseDesktopPet
{
    internal sealed class ChatPreferencesForm : Form
    {
        private const int BaseWidth = 354;
        private const int BaseHeight = 410;
        private static readonly Color Cream = Color.FromArgb(252, 250, 246);
        private static readonly Color White = Color.FromArgb(255, 253, 249);
        private static readonly Color Ink = Color.FromArgb(63, 59, 58);
        private static readonly Color Accent = Color.FromArgb(188, 111, 111);
        private readonly Label title = new Label();
        private readonly Label subtitle = new Label();
        private readonly Label nameLabel = new Label();
        private readonly Label lengthLabel = new Label();
        private readonly Label interestsLabel = new Label();
        private readonly Label avoidLabel = new Label();
        private readonly Label hint = new Label();
        private readonly TextBox nameInput = new TextBox();
        private readonly ComboBox lengthInput = new ComboBox();
        private readonly TextBox interestsInput = new TextBox();
        private readonly TextBox avoidInput = new TextBox();
        private readonly Button reset = new Button();
        private readonly Button save = new Button();
        private readonly Button close = new Button();
        private readonly Font headingFont;
        private readonly Font bodyFont;
        private readonly Font smallFont;
        private readonly float scale;

        internal ChatPreferences Value { get; private set; }

        internal ChatPreferencesForm(ChatPreferences preferences, int uiScalePercent,
            Size availableArea)
        {
            Value = preferences.Normalized();
            float requestedScale = uiScalePercent / 100f * 1.2f;
            scale = availableArea.IsEmpty ? requestedScale : Math.Min(requestedScale,
                Math.Min((availableArea.Width - 16f) / BaseWidth,
                    (availableArea.Height - 16f) / BaseHeight));
            headingFont = new Font("Microsoft YaHei UI", 14f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            bodyFont = new Font("Microsoft YaHei UI", 12f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            smallFont = new Font("Microsoft YaHei UI", 10f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Cream;
            DoubleBuffered = true;
            KeyPreview = true;
            Size = new Size(P(BaseWidth), P(BaseHeight));

            AddLabel(title, "聊天个性化", headingFont, 26, 12, 270, 27);
            AddLabel(subtitle, "角色口吻来自本地角色卡精简版", smallFont, 20, 44, 320, 20);
            AddLabel(nameLabel, "希望我怎么称呼你", bodyFont, 20, 70, 310, 23);
            AddLabel(lengthLabel, "回复长度", bodyFont, 20, 130, 310, 23);
            AddLabel(interestsLabel, "感兴趣的话题（可留空）", bodyFont, 20, 193, 310, 23);
            AddLabel(avoidLabel, "希望避开的话题（可留空）", bodyFont, 20, 268, 310, 23);
            AddLabel(hint, "仅存于本机；保存后从新话题开始聊天", smallFont, 20, 343, 320, 20);

            nameInput.MaxLength = 24;
            nameInput.BorderStyle = BorderStyle.None;
            nameInput.BackColor = White;
            nameInput.ForeColor = Ink;
            nameInput.Font = bodyFont;
            nameInput.Bounds = At(28, 101, 296, 24);
            Controls.Add(nameInput);

            lengthInput.DropDownStyle = ComboBoxStyle.DropDownList;
            lengthInput.Items.AddRange(new object[] { "简短 · 一两句", "适中 · 两到四句" });
            lengthInput.FlatStyle = FlatStyle.Flat;
            lengthInput.BackColor = White;
            lengthInput.ForeColor = Ink;
            lengthInput.Font = bodyFont;
            lengthInput.Bounds = At(22, 159, 312, 30);
            Controls.Add(lengthInput);

            AddTextBox(interestsInput, 28, 225, 296, 43, 160);
            AddTextBox(avoidInput, 28, 300, 296, 43, 160);

            SetupButton(close, "×", 321, 10, 25, 27, Cream, Ink, headingFont);
            close.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            SetupButton(reset, "恢复默认", 20, 373, 114, 28, Color.FromArgb(238, 231, 224), Ink, bodyFont);
            reset.Click += delegate { Fill(ChatPreferences.Defaults()); };
            SetupButton(save, "保存", 233, 370, 101, 32, Accent, Color.White, bodyFont);
            save.Click += delegate
            {
                Value = new ChatPreferences
                {
                    PreferredName = nameInput.Text,
                    ReplyLength = lengthInput.SelectedIndex == 1 ? "balanced" : "short",
                    Interests = interestsInput.Text,
                    AvoidTopics = avoidInput.Text
                }.Normalized();
                DialogResult = DialogResult.OK;
                Close();
            };
            Fill(Value);
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
            };
            using (GraphicsPath shape = PetMenuCanvas.RoundedRectangle(
                new RectangleF(0, 0, Width, Height), 14 * scale))
                Region = new Region(shape);
        }

        private int P(int value) { return (int)Math.Round(value * scale); }

        private Rectangle At(int x, int y, int width, int height)
        {
            return new Rectangle(P(x), P(y), P(width), P(height));
        }

        private void AddLabel(Label label, string text, Font font,
            int x, int y, int width, int height)
        {
            label.Text = text;
            label.Font = font;
            label.ForeColor = Ink;
            label.BackColor = Cream;
            label.Bounds = At(x, y, width, height);
            label.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(label);
        }

        private void AddTextBox(TextBox input, int x, int y, int width, int height, int maxLength)
        {
            input.Multiline = true;
            input.MaxLength = maxLength;
            input.BorderStyle = BorderStyle.None;
            input.BackColor = White;
            input.ForeColor = Ink;
            input.Font = bodyFont;
            input.Bounds = At(x, y, width, height);
            Controls.Add(input);
        }

        private void SetupButton(Button button, string text, int x, int y,
            int width, int height, Color background, Color foreground, Font font)
        {
            button.Text = text;
            button.Font = font;
            button.Bounds = At(x, y, width, height);
            button.BackColor = background;
            button.ForeColor = foreground;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Cursor = Cursors.Hand;
            Controls.Add(button);
        }

        private void Fill(ChatPreferences preferences)
        {
            nameInput.Text = preferences.PreferredName;
            lengthInput.SelectedIndex = preferences.ReplyLength == "balanced" ? 1 : 0;
            interestsInput.Text = preferences.Interests;
            avoidInput.Text = preferences.AvoidTopics;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Rectangle work = Screen.FromRectangle(Bounds).WorkingArea;
            Location = new Point(Math.Max(work.Left, Math.Min(work.Right - Width, Left)),
                Math.Max(work.Top, Math.Min(work.Bottom - Height, Top)));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.ScaleTransform(scale, scale);
            using (Pen border = new Pen(Color.FromArgb(183, 141, 139), 1))
            using (Pen fieldBorder = new Pen(Color.FromArgb(233, 223, 215), 1))
            using (SolidBrush white = new SolidBrush(White))
            using (SolidBrush dot = new SolidBrush(Accent))
            using (GraphicsPath card = PetMenuCanvas.RoundedRectangle(
                new RectangleF(0.5f, 0.5f, BaseWidth - 1, BaseHeight - 1), 14))
            {
                g.DrawPath(border, card);
                foreach (RectangleF bounds in new[]
                {
                    new RectangleF(20, 96, 314, 36),
                    new RectangleF(20, 217, 314, 56),
                    new RectangleF(20, 292, 314, 56)
                })
                using (GraphicsPath field = PetMenuCanvas.RoundedRectangle(bounds, 9))
                {
                    g.FillPath(white, field);
                    g.DrawPath(fieldBorder, field);
                }
                g.FillEllipse(dot, 14, 22, 6, 6);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                headingFont.Dispose();
                bodyFont.Dispose();
                smallFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
