using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace MakiseDesktopPet
{
    internal static class ChatSmoke
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            string preferencesPath = Path.Combine(args[0], "test-artifacts", "chat-preferences-smoke.json");
            ChatPreferencesStore store = new ChatPreferencesStore(preferencesPath);
            store.Save(new ChatPreferences { PreferredName = "测试者", ReplyLength = "balanced",
                Interests = "记忆科学", AvoidTopics = "剧情剧透" });
            ChatPreferences saved = store.Load();
            if (saved.PreferredName != "测试者" || saved.Interests != "记忆科学" ||
                saved.AvoidTopics != "剧情剧透" || saved.ReplyLength != "balanced")
                throw new Exception("Local chat preferences were not persisted.");
            if (ChatReplyGuard.Accept("今天有点无聊", "今天天气不错，我刚在实验室整理数据。") ||
                ChatReplyGuard.Accept("程序报错了", "我这边有份常见错误清单。") ||
                !ChatReplyGuard.Accept("我累了", "先休息一会儿，别硬撑。"))
                throw new Exception("The reply guard did not distinguish grounded replies.");
            using (OllamaChatClient client = new OllamaChatClient(args[0], store))
            {
                string personalized = client.BuildRequestJson("你好");
                if (!personalized.Contains("测试者") || !personalized.Contains("记忆科学") ||
                    !personalized.Contains("剧情剧透") || !personalized.Contains("170"))
                    throw new Exception("The model request omitted saved preferences.");
                string preferredNameReply = client.SendAsync("你应该怎么称呼我？",
                    CancellationToken.None).GetAwaiter().GetResult();
                if (!preferredNameReply.Contains("测试者"))
                    throw new Exception("The local model ignored the saved preferred name: " +
                        preferredNameReply);
                client.SavePreferences(ChatPreferences.Defaults());
                string payload = client.BuildRequestJson("今天有点无聊，陪我说两句。");
                if (!payload.Contains("没有现实位置") ||
                    payload.Contains("你应该怎么称呼我") ||
                    !payload.Contains("\"role\":\"system\"") ||
                    !payload.Contains("\"role\":\"user\""))
                    throw new Exception("Chat request omitted the card persona or user message.");
                Stopwatch watch = Stopwatch.StartNew();
                string answer = client.SendAsync("今天有点无聊，陪我说两句。",
                    CancellationToken.None).GetAwaiter().GetResult();
                watch.Stop();
                if (string.IsNullOrWhiteSpace(answer))
                    throw new Exception("The local model returned an empty reply.");
                if (!ChatReplyGuard.Accept("今天有点无聊，陪我说两句。", answer))
                    throw new Exception("The chat client returned an ungrounded first reply.");
                bool firstRetried = client.LastReplyWasRetried;
                bool firstFallback = client.LastReplyWasFallback;
                string followUp = client.SendAsync("今天写程序一直报错，快烦死了。",
                    CancellationToken.None).GetAwaiter().GetResult();
                if (string.IsNullOrWhiteSpace(followUp))
                    throw new Exception("The follow-up reply was empty.");
                if (!ChatReplyGuard.Accept("今天写程序一直报错，快烦死了。", followUp))
                    throw new Exception("The chat client returned an ungrounded follow-up reply.");
                using (ChatForm chat = new ChatForm(client, delegate { }, delegate { }))
                {
                    TextBox input = (TextBox)typeof(ChatForm).GetField("input",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(chat);
                    input.Text = "今天有点无聊，聊点什么？";
                    chat.CreateControl();
                    int textBoxes = 0;
                    foreach (Control control in chat.Controls)
                        if (control is TextBox) textBoxes++;
                    if (textBoxes != 1 || chat.Height >= 180)
                        throw new Exception("The compact chat panel still contains a reply area.");
                    chat.ApplyUiScale(150);
                    if (chat.Size != new Size(554, 198))
                        throw new Exception("The compact input panel did not scale to 150 percent.");
                    chat.ApplyUiScale(80);
                    if (chat.Size != new Size(296, 106))
                        throw new Exception("The compact input panel did not scale to 80 percent.");
                    chat.ApplyUiScale(100);
                    string path = Path.Combine(args[0], "test-artifacts", "chat-preview.png");
                    SaveFormPreview(chat, path);
                    using (ChatPreferencesForm editor = new ChatPreferencesForm(
                        new ChatPreferences { PreferredName = "小研究员", ReplyLength = "balanced",
                            Interests = "脑科学、编程", AvoidTopics = "剧情剧透" },
                        100, new Size(1920, 1080)))
                    {
                        SaveFormPreview(editor, Path.Combine(args[0], "test-artifacts",
                            "chat-preferences-preview.png"));
                    }
                    Console.WriteLine("PASS: local chat responded in " +
                        watch.Elapsed.TotalSeconds.ToString("0.0") + " s; preview: " + path);
                    Console.WriteLine("Reply: " + answer.Replace('\n', ' '));
                    Console.WriteLine("Follow-up: " + followUp.Replace('\n', ' '));
                    Console.WriteLine("Preferred name check: " +
                        preferredNameReply.Replace('\n', ' '));
                    Console.WriteLine("Guard: first retry=" + firstRetried + ", fallback=" +
                        firstFallback + "; follow-up retry=" + client.LastReplyWasRetried +
                        ", fallback=" + client.LastReplyWasFallback);
                }
            }
        }

        private static void SaveFormPreview(Form form, string path)
        {
            form.CreateControl();
            using (Bitmap image = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                using (Graphics graphics = Graphics.FromImage(image))
                {
                    foreach (Control control in form.Controls)
                    {
                        control.CreateControl();
                        using (Bitmap layer = new Bitmap(control.Width, control.Height))
                        {
                            control.DrawToBitmap(layer, new Rectangle(Point.Empty, layer.Size));
                            graphics.DrawImageUnscaled(layer, control.Location);
                        }
                    }
                }
                image.Save(path, ImageFormat.Png);
            }
        }
    }
}
