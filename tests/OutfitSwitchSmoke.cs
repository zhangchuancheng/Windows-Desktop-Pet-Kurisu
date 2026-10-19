using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MakiseDesktopPet
{
    internal static class OutfitSwitchSmoke
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const int WmClose = 0x0010;
        private const int WmCommand = 0x0111;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string windowName);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        private static object Read(object target, string name)
        {
            return target.GetType().GetField(name, Hidden).GetValue(target);
        }

        private static void Write(object target, string name, object value)
        {
            target.GetType().GetField(name, Hidden).SetValue(target, value);
        }

        private static object Call(object target, string name, params object[] args)
        {
            return target.GetType().GetMethod(name, Hidden).Invoke(target, args);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static byte[] Pixels(Bitmap image)
        {
            BitmapData data = image.LockBits(new Rectangle(Point.Empty, image.Size),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                int stride = image.Width * 4;
                byte[] pixels = new byte[stride * image.Height];
                for (int y = 0; y < image.Height; y++)
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels,
                        y * stride, stride);
                return pixels;
            }
            finally { image.UnlockBits(data); }
        }

        private static void SameBitmap(Bitmap actual, Bitmap expected, string description)
        {
            Require(actual.Size == expected.Size, description + " canvas differs");
            byte[] left = Pixels(actual);
            byte[] right = Pixels(expected);
            for (int i = 0; i < left.Length; i++)
                if (left[i] != right[i])
                    throw new Exception(description + " uses pixels from the wrong outfit");
        }

        private static void DismissExpectedWarning(ManualResetEvent stop, Action found)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (!stop.WaitOne(30))
            {
                IntPtr dialog = FindWindow("#32770", "切换服装");
                uint processId;
                if (dialog != IntPtr.Zero &&
                    GetWindowThreadProcessId(dialog, out processId) != 0 &&
                    processId == (uint)Process.GetCurrentProcess().Id)
                {
                    found();
                    PostMessage(dialog, WmCommand, new IntPtr(1), IntPtr.Zero);
                    PostMessage(dialog, WmClose, IntPtr.Zero, IntPtr.Zero);
                    return;
                }
                if (DateTime.UtcNow >= deadline)
                {
                    Console.Error.WriteLine("FAIL: missing outfit warning did not appear or close");
                    Environment.Exit(1);
                }
            }
        }

        [STAThread]
        private static void Main(string[] args)
        {
            PetForm pet = null;
            try
            {
                Require(args.Length > 0, "Project root argument is required");
                string projectRoot = args[0];
                string runtimeRoot = Path.Combine(projectRoot, "dist");
                string artifacts = Path.Combine(projectRoot, "test-artifacts");
                Directory.CreateDirectory(artifacts);
                string isolated = Path.Combine(artifacts, "outfit-settings.json");
                string[] expectedIds = { "lab-coat", "maid", "yukata", "christmas-v2",
                    "red-dress-handfix", "beachwear-v2", "battle-suit" };
                Require(OutfitCatalog.All.Length == expectedIds.Length + 1 &&
                    OutfitCatalog.All[0].Id == OutfitCatalog.OriginalId,
                    "Catalog should expose original plus seven outfits");
                for (int i = 0; i < expectedIds.Length; i++)
                    Require(OutfitCatalog.All[i + 1].Id == expectedIds[i],
                        "Catalog outfit order or ID differs at " + i);

                NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4));
                Application.EnableVisualStyles();
                Require(File.Exists(Path.Combine(runtimeRoot, "dialogues.json")),
                    "Build dist before running outfit integration tests");
                pet = new PetForm(runtimeRoot);
                ((NotifyIcon)Read(pet, "tray")).Visible = false;
                SettingsStore store = (SettingsStore)Read(pet, "settingsStore");
                Write(store, "PathOnDisk", isolated);
                if (File.Exists(isolated)) File.Delete(isolated);
                PetSettings settings = (PetSettings)Read(pet, "settings");
                ContextMenuStrip outfitMenu = (ContextMenuStrip)Read(pet, "outfitMenu");
                Require(outfitMenu.Items.Count == expectedIds.Length + 1,
                    "Outfit picker should list original plus seven outfits");

                for (int outfitIndex = 0; outfitIndex < expectedIds.Length; outfitIndex++)
                {
                    string id = expectedIds[outfitIndex];
                    string directory = OutfitCatalog.SpriteDirectory(runtimeRoot, id);
                    Require(Directory.Exists(directory), "Missing packaged outfit: " + id);
                    ToolStripMenuItem pickerItem = outfitMenu.Items[outfitIndex + 1]
                        as ToolStripMenuItem;
                    Require(pickerItem != null && (string)pickerItem.Tag == id,
                        "Outfit picker item has the wrong ID: " + id);
                    outfitMenu.Show(new Point(20, 20));
                    Application.DoEvents();
                    pickerItem.PerformClick();
                    outfitMenu.Close();
                    Require((string)Read(pet, "outfitId") == id &&
                        settings.OutfitId == id && store.Load().OutfitId == id,
                        "Outfit selection did not persist: " + id);
                    Require((Pose)Read(pet, "pose") == Pose.Idle,
                        "Outfit switch should settle on idle: " + id);
                    SpriteAssets active = (SpriteAssets)Read(pet, "sprites");
                    using (SpriteAssets expected = new SpriteAssets(directory, settings.Height))
                        foreach (Pose pose in Enum.GetValues(typeof(Pose)))
                            SameBitmap(active.Get(pose), expected.Get(pose),
                                id + "/" + pose);
                    Require(Read(pet, "idleAnimation") != null,
                        "Outfit idle animation was not recreated: " + id);
                    Require(!pet.Visible, "Outfit test unexpectedly showed the pet window");
                    Console.WriteLine("PASS: " + id + " loaded every pose and persisted");
                }

                // Switching from the final catalog entry must also work. The
                // forward loop alone never exercised the battle suit as the
                // starting outfit for a picker selection.
                for (int outfitIndex = expectedIds.Length - 2; outfitIndex >= 0; outfitIndex--)
                {
                    string id = expectedIds[outfitIndex];
                    ToolStripMenuItem pickerItem = outfitMenu.Items[outfitIndex + 1]
                        as ToolStripMenuItem;
                    outfitMenu.Show(new Point(20, 20));
                    Application.DoEvents();
                    pickerItem.PerformClick();
                    outfitMenu.Close();
                    Require((string)Read(pet, "outfitId") == id &&
                        settings.OutfitId == id && store.Load().OutfitId == id,
                        "Reverse outfit selection did not persist: " + id);
                    string directory = OutfitCatalog.SpriteDirectory(runtimeRoot, id);
                    SpriteAssets active = (SpriteAssets)Read(pet, "sprites");
                    using (SpriteAssets expected = new SpriteAssets(directory, settings.Height))
                        foreach (Pose pose in Enum.GetValues(typeof(Pose)))
                            SameBitmap(active.Get(pose), expected.Get(pose),
                                "reverse " + id + "/" + pose);
                    Console.WriteLine("PASS: reverse switch to " + id);
                }

                // Return to battle suit and use the actual picker item for
                // original, so that this case is not covered only by ID
                // normalization below.
                ((ToolStripMenuItem)outfitMenu.Items[expectedIds.Length]).PerformClick();
                Require((string)Read(pet, "outfitId") == "battle-suit",
                    "Could not reselect battle suit before returning to original");
                ((ToolStripMenuItem)outfitMenu.Items[0]).PerformClick();
                Require((string)Read(pet, "outfitId") == OutfitCatalog.OriginalId &&
                    settings.OutfitId == OutfitCatalog.OriginalId &&
                    store.Load().OutfitId == OutfitCatalog.OriginalId,
                    "Picker could not switch from battle suit back to original");
                Console.WriteLine("PASS: battle suit returned to original through picker");

                ((ToolStripMenuItem)outfitMenu.Items[expectedIds.Length]).PerformClick();
                Call(pet, "ChangeOutfit", "unlisted-outfit");
                Require((string)Read(pet, "outfitId") == OutfitCatalog.OriginalId &&
                    settings.OutfitId == OutfitCatalog.OriginalId &&
                    store.Load().OutfitId == OutfitCatalog.OriginalId,
                    "Unknown outfit ID should restore and save original");

                SpriteAssets beforeSprites = (SpriteAssets)Read(pet, "sprites");
                string beforeRoot = (string)Read(pet, "appDirectory");
                string missingRoot = Path.Combine(artifacts, "missing-outfit-root");
                Write(pet, "appDirectory", missingRoot);
                bool warningSeen = false;
                using (ManualResetEvent stop = new ManualResetEvent(false))
                {
                    Thread closer = new Thread((ThreadStart)delegate
                    {
                        DismissExpectedWarning(stop, delegate { warningSeen = true; });
                    });
                    closer.IsBackground = true;
                    closer.Start();
                    try { Call(pet, "ChangeOutfit", expectedIds[0]); }
                    finally
                    {
                        stop.Set();
                        closer.Join(1000);
                        Write(pet, "appDirectory", beforeRoot);
                    }
                }
                Require(warningSeen && (string)Read(pet, "outfitId") == OutfitCatalog.OriginalId &&
                    ReferenceEquals(Read(pet, "sprites"), beforeSprites) &&
                    store.Load().OutfitId == OutfitCatalog.OriginalId,
                    "Missing pack should warn while keeping the existing outfit and settings");

                File.WriteAllText(isolated,
                    "{\"Height\":220,\"TopMost\":true,\"UiScaleVersion\":2," +
                    "\"UiScalePercent\":100,\"OutfitId\":\"unlisted-outfit\"}");
                Require(store.Load().OutfitId == OutfitCatalog.OriginalId,
                    "Unknown outfit in saved settings should normalize to original");
                Require(!pet.Visible, "Outfit test unexpectedly showed the pet window");

                ContextMenuStrip trayMenu = (ContextMenuStrip)Read(pet, "trayMenu");
                ToolStripMenuItem trayOutfits = null;
                foreach (ToolStripItem item in trayMenu.Items)
                    if ((string)item.Tag == "outfit-picker")
                        trayOutfits = item as ToolStripMenuItem;
                Require(trayOutfits != null &&
                    trayOutfits.DropDownItems.Count == expectedIds.Length + 1,
                    "Tray outfit submenu is missing one or more outfits");
                for (int i = 0; i < trayOutfits.DropDownItems.Count; i++)
                    Require((string)trayOutfits.DropDownItems[i].Tag == OutfitCatalog.All[i].Id,
                        "Tray outfit submenu entry has wrong ID: " + i);
                ((ToolStripMenuItem)trayOutfits.DropDownItems[expectedIds.Length]).PerformClick();
                ((ToolStripMenuItem)trayOutfits.DropDownItems[0]).PerformClick();
                Require((string)Read(pet, "outfitId") == OutfitCatalog.OriginalId &&
                    store.Load().OutfitId == OutfitCatalog.OriginalId,
                    "Tray submenu could not switch battle suit back to original");
                ((ToolStripMenuItem)trayOutfits.DropDownItems[expectedIds.Length]).PerformClick();
                ((ToolStripMenuItem)trayOutfits.DropDownItems[1]).PerformClick();
                Require((string)Read(pet, "outfitId") == expectedIds[0] &&
                    store.Load().OutfitId == expectedIds[0],
                    "Tray submenu could not switch battle suit to another outfit");
                Console.WriteLine("PASS: tray switched battle suit to original and another outfit");

                // The hidden checks above bypass the popup handoff and the
                // visible pet's window level. Exercise that route explicitly,
                // beginning with battle suit and choosing original.
                ((ToolStripMenuItem)outfitMenu.Items[expectedIds.Length]).PerformClick();
                pet.Location = new Point(100, 100);
                pet.Show();
                Application.DoEvents();
                PetMenuPopup popup = (PetMenuPopup)Read(pet, "petMenu");
                popup.ShowAt(new Point(100, 100), settings.Height, settings.TopMost,
                    settings.UiScalePercent, OutfitCatalog.DisplayName("battle-suit"));
                Application.DoEvents();
                Require(popup.Visible, "Settings popup did not open on visible battle suit");
                PetMenuCanvas canvas = popup.Canvas;
                int clickX = (int)Math.Round(70 * canvas.UiScale);
                int clickY = (int)Math.Round(282 * canvas.UiScale);
                Call(canvas, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1,
                    clickX, clickY, 0));
                Call(canvas, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1,
                    clickX, clickY, 0));
                Application.DoEvents();
                Thread.Sleep(60);
                Application.DoEvents();
                Require(!popup.Visible && outfitMenu.Visible,
                    "Visible settings popup did not hand off to outfit picker");
                ((ToolStripMenuItem)outfitMenu.Items[0]).PerformClick();
                Application.DoEvents();
                Require((string)Read(pet, "outfitId") == OutfitCatalog.OriginalId &&
                    store.Load().OutfitId == OutfitCatalog.OriginalId,
                    "Visible battle suit could not return to original through menu handoff");
                outfitMenu.Close();
                pet.Hide();
                Console.WriteLine("PASS: visible battle suit menu handed off to picker and restored original");
                Console.WriteLine("PASS: seven outfits, original fallback, missing pack rollback and isolated settings");
            }
            catch (Exception error)
            {
                Console.WriteLine("FAIL: " + error);
                Environment.ExitCode = 1;
            }
            finally
            {
                if (pet != null)
                {
                    typeof(PetForm).GetMethod("OnFormClosed", Hidden).Invoke(pet,
                        new object[] { new FormClosedEventArgs(CloseReason.ApplicationExitCall) });
                    pet.Dispose();
                }
            }
        }
    }
}
