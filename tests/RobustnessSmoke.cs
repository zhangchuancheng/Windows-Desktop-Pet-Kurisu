using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MakiseDesktopPet
{
    internal static class RobustnessSmoke
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Read(object target, string name)
        { return target.GetType().GetField(name, Hidden).GetValue(target); }
        private static void Write(object target, string name, object value)
        { target.GetType().GetField(name, Hidden).SetValue(target, value); }
        private static object Call(object target, string name, params object[] args)
        { return target.GetType().GetMethod(name, Hidden).Invoke(target, args); }
        private static void Require(bool condition, string message)
        { if (!condition) throw new Exception(message); }
        private static void PumpUntil(Func<bool> done)
        {
            Stopwatch watch = Stopwatch.StartNew();
            while (!done() && watch.ElapsedMilliseconds < 5000)
            { Application.DoEvents(); Thread.Sleep(5); }
            Require(done(), "Asynchronous check timed out");
        }
        private sealed class DelayedHandler : HttpMessageHandler
        {
            internal readonly TaskCompletionSource<HttpResponseMessage> Reply =
                new TaskCompletionSource<HttpResponseMessage>();
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken token) { return Reply.Task; }
        }

        private static void CheckChat(string root, string artifacts, int scenario)
        {
            using (OllamaChatClient client = new OllamaChatClient(root,
                new ChatPreferencesStore(Path.Combine(artifacts, "chat.json"))))
            {
                DelayedHandler handler = new DelayedHandler();
                ((HttpClient)Read(client, "http")).Dispose();
                Write(client, "http", new HttpClient(handler) {
                    BaseAddress = new Uri("http://127.0.0.1:11434/") });
                bool replied = false;
                using (ChatForm chat = new ChatForm(client, text => replied = true, () => {}))
                {
                    IntPtr handle = chat.Handle;
                    TextBox input = (TextBox)Read(chat, "input");
                    input.Text = "你好";
                    Call(chat, "SendCurrentMessage");
                    if (scenario == 1) input.Text = "新草稿";
                    if (scenario == 2) { input.Text = "改过"; input.Text = "你好"; }
                    if (scenario == 3) { input.Text = "取消后的草稿"; chat.HideChat(); }
                    if (scenario == 4) handler.Reply.SetException(new HttpRequestException("test"));
                    else handler.Reply.SetResult(new HttpResponseMessage(HttpStatusCode.OK) {
                        Content = new StringContent("{\"message\":{\"content\":\"你好。\"}}",
                            Encoding.UTF8, "application/json") });
                    PumpUntil(() => Read(chat, "pending") == null);
                    string[] expected = { "", "新草稿", "你好", "取消后的草稿", "你好" };
                    Require(input.Text == expected[scenario], "Draft changed in scenario " + scenario);
                    Require(replied == (scenario < 3), "Cancelled or failed request displayed a reply");
                    Require(((Button)Read(chat, "send")).Enabled, "Send stayed disabled");
                }
            }
        }

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4));
                Application.EnableVisualStyles();
                string root = args[0];
                string artifacts = Path.Combine(root, "test-artifacts", "robustness");
                Directory.CreateDirectory(artifacts);
                for (int i = 0; i < 5; i++) CheckChat(root, artifacts, i);
                Console.WriteLine("PASS: unchanged input clears; new/retyped drafts, cancellation and failures preserve input");

                using (PetMenuCanvas canvas = new PetMenuCanvas())
                {
                    canvas.Reset(220, true, 150, "原版");
                    foreach (Size work in new[] { new Size(1366, 728), new Size(800, 560),
                        new Size(360, 600) })
                    {
                        canvas.FitTo(work);
                        Require(canvas.Width <= work.Width - 16 && canvas.Height <= work.Height - 16,
                            "Menu exceeds small work area");
                        Point physical = new Point((int)(70 * canvas.UiScale), (int)(426 * canvas.UiScale));
                        Point logical = (Point)Call(canvas, "LogicalPoint", physical);
                        Require(Call(canvas, "HitTest", logical).ToString() == "Exit",
                            "Fitted menu exit hit region differs from drawing");
                        Require(physical.Y < canvas.Height, "Exit falls outside menu");
                        using (Bitmap preview = new Bitmap(canvas.Width, canvas.Height))
                        {
                            canvas.DrawToBitmap(preview, new Rectangle(Point.Empty, preview.Size));
                            preview.Save(Path.Combine(artifacts,
                                "menu-" + work.Width + "x" + work.Height + ".png"));
                        }
                    }
                    canvas.FitTo(new Size(2560, 1528));
                    Require(canvas.SelectedUiScalePercent == 150 && canvas.Height == 817,
                        "Screen fit changed the user's scale preference");
                    int commits = 0;
                    canvas.HeightCommitted += () => commits++;
                    Write(canvas, "pressed", Enum.Parse(Read(canvas, "pressed").GetType(), "Slider"));
                    Write(canvas, "sliding", true);
                    Call(canvas, "OnMouseCaptureChanged", EventArgs.Empty);
                    Require(!(bool)Read(canvas, "sliding") && commits == 1,
                        "Interrupted slider did not finish and commit");
                }
                Console.WriteLine("PASS: menu fits small work areas, exit hit testing matches and preferred scale restores");

                PetForm pet = new PetForm(Path.Combine(root, "dist"));
                bool cleaned = false;
                try
                {
                    ((System.Windows.Forms.Timer)Read(pet, "timer")).Stop();
                    ((NotifyIcon)Read(pet, "tray")).Visible = false;
                    SettingsStore store = (SettingsStore)Read(pet, "settingsStore");
                    string isolated = Path.Combine(artifacts, "settings.json");
                    Write(store, "PathOnDisk", isolated);
                    IntPtr handle = pet.Handle;
                    Write(pet, "pressed", true);
                    Write(pet, "dragging", true);
                    Call(pet, "SetPose", Pose.Drag);
                    Call(pet, "OnMouseCaptureChanged", EventArgs.Empty);
                    Require(!(bool)Read(pet, "pressed") && !(bool)Read(pet, "dragging") &&
                        (Pose)Read(pet, "pose") == Pose.Idle, "Capture loss left drag active");
                    Write(pet, "lastInteraction", ((Stopwatch)Read(pet, "clock")).ElapsedMilliseconds - 180001);
                    Call(pet, "OnTimerTick", null, EventArgs.Empty);
                    Require((bool)Read(pet, "sleeping"), "Capture loss prevents later sleep");
                    Write(pet, "sleeping", false);
                    Call(pet, "SetPose", Pose.Idle);
                    Write(pet, "pressed", true);
                    Write(pet, "dragging", true);
                    Call(pet, "SetPose", Pose.Drag);
                    Call(pet, "WndProc", Message.Create(handle, 0x001F, IntPtr.Zero, IntPtr.Zero));
                    Require(!(bool)Read(pet, "pressed") && !(bool)Read(pet, "dragging"),
                        "WM_CANCELMODE did not cancel drag");
                    Write(pet, "pressed", true);
                    Write(pet, "dragging", true);
                    Call(pet, "SetPose", Pose.Drag);
                    Call(pet, "HideToTray");
                    Require(!(bool)Read(pet, "pressed") && !(bool)Read(pet, "dragging"),
                        "Hide did not cancel drag");
                    Write(pet, "pressed", true);
                    int index = (int)Read(pet, "nextClickPose");
                    Call(pet, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
                    Require((int)Read(pet, "nextClickPose") == (index + 1) % 3,
                        "Normal mouse-up was swallowed");
                    Console.WriteLine("PASS: capture loss, WM_CANCELMODE and hide cancel dragging; sleep and normal click recover");

                    foreach (string id in new[] { "original", "lab-coat", "battle-suit" })
                    {
                        Call(pet, "ChangeOutfit", id);
                        IdleMotionAnimation animation = (IdleMotionAnimation)Read(pet, "idleAnimation");
                        Bitmap[] cached = (Bitmap[])Read(animation, "localBlinkOriginals");
                        Bitmap first = cached[0], second = cached[1];
                        foreach (int height in new[] { 220, 355, 500, 356 })
                        {
                            Stopwatch watch = Stopwatch.StartNew();
                            Call(pet, "ApplySize", height);
                            Console.WriteLine("RESIZE {0} {1}px: {2} ms", id, height, watch.ElapsedMilliseconds);
                            Require(Object.ReferenceEquals(cached[0], first) &&
                                Object.ReferenceEquals(cached[1], second), "Resize regenerated blink originals");
                        }
                    }
                    Console.WriteLine("PASS: resizing reuses blink originals");
                    Call(pet, "SaveSettings");
                    using (FileStream locked = new FileStream(isolated, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        Call(pet, "ChangeSize", 220);
                        Require((bool)Read(pet, "settingsSaveFailed"), "Write lock was not reported");
                        Require(((PetSettings)Read(pet, "settings")).Height == 220,
                            "Write failure lost the in-memory choice");
                    }
                    Call(pet, "ChangeSize", 221);
                    Require(!(bool)Read(pet, "settingsSaveFailed") && store.Load().Height == 221,
                        "Saving did not recover after file unlock");
                    ChatForm chat = (ChatForm)Read(pet, "chat");
                    Write(pet, "pressed", true);
                    Write(pet, "dragging", true);
                    using (FileStream locked = new FileStream(isolated, FileMode.Open, FileAccess.Read, FileShare.Read))
                        Call(pet, "OnFormClosed", new FormClosedEventArgs(CloseReason.UserClosing));
                    cleaned = true;
                    Require(chat.IsDisposed, "Save error interrupted exit cleanup");
                    Require(!(bool)Read(pet, "pressed") && !(bool)Read(pet, "dragging"),
                        "Closing retained a pointer interaction after releasing resources");
                    Console.WriteLine("PASS: locked settings do not abort actions or exit; saving recovers after unlock");
                }
                finally
                {
                    if (!cleaned) Call(pet, "OnFormClosed", new FormClosedEventArgs(CloseReason.UserClosing));
                    pet.Dispose();
                }
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
