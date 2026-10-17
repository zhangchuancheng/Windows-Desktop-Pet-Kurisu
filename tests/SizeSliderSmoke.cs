using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MakiseDesktopPet
{
    internal static class SizeSliderSmoke
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

        private static object Read(object target, string name)
        {
            return target.GetType().GetField(name, Hidden).GetValue(target);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void Pump(int milliseconds)
        {
            DateTime until = DateTime.UtcNow.AddMilliseconds(milliseconds);
            while (DateTime.UtcNow < until)
            {
                Application.DoEvents();
                Thread.Sleep(5);
            }
        }

        private static object Call(object target, string name, params object[] args)
        {
            return target.GetType().GetMethod(name, Hidden).Invoke(target, args);
        }

        private static MouseEventArgs MouseAt(PetMenuCanvas canvas, MouseButtons button,
            int clicks, int logicalX, int logicalY)
        {
            int x = (int)Math.Round(logicalX * canvas.UiScale);
            int y = (int)Math.Round(logicalY * canvas.UiScale);
            return new MouseEventArgs(button, clicks, x, y, 0);
        }

        private static void Click(PetMenuCanvas canvas, int logicalX, int logicalY)
        {
            MouseEventArgs mouse = MouseAt(canvas, MouseButtons.Left, 1, logicalX, logicalY);
            Call(canvas, "OnMouseDown", mouse);
            Call(canvas, "OnMouseUp", mouse);
        }

        [STAThread]
        private static void Main(string[] args)
        {
            PetForm pet = null;
            try
            {
                NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4));
                Application.EnableVisualStyles();
                pet = new PetForm(args[0]);
                ((NotifyIcon)Read(pet, "tray")).Visible = false;
                SettingsStore store = (SettingsStore)Read(pet, "settingsStore");
                string isolated = Path.Combine(args[0], "test-artifacts", "size-slider-settings.json");
                store.GetType().GetField("PathOnDisk", Hidden).SetValue(store, isolated);
                if (File.Exists(isolated)) File.Delete(isolated);
                File.WriteAllText(isolated, "{\"Height\":220,\"TopMost\":true}");
                Require(store.Load().UiScalePercent == 100,
                    "Older settings without UI scale should use the new 100 percent default");
                File.WriteAllText(isolated,
                    "{\"Height\":220,\"TopMost\":true,\"UiScalePercent\":120}");
                Require(store.Load().UiScalePercent == 100 &&
                    store.Load().UiScaleVersion == PetSettings.CurrentUiScaleVersion,
                    "Previous 120 percent should migrate to the new 100 percent");
                File.WriteAllText(isolated,
                    "{\"Height\":220,\"TopMost\":true,\"UiScalePercent\":100," +
                    "\"UiScaleVersion\":2}");
                Require(store.Load().UiScalePercent == 100,
                    "New 100 percent should not be migrated a second time");
                File.Delete(isolated);

                PetMenuPopup menu = (PetMenuPopup)Read(pet, "petMenu");
                PetMenuCanvas canvas = menu.Canvas;
                Require(menu.Items.Count == 1 && canvas.Width == 317 && canvas.Height == 415,
                    "Compact menu canvas should replace nested system menus");
                PetSettings settings = (PetSettings)Read(pet, "settings");
                settings.UiScalePercent = 100;
                settings.UiScaleVersion = PetSettings.CurrentUiScaleVersion;
                canvas.Reset(settings.Height, settings.TopMost, settings.UiScalePercent);
                string preview = Path.Combine(args[0], "test-artifacts", "screenshots", "pet-menu-preview.png");
                Directory.CreateDirectory(Path.GetDirectoryName(preview));
                using (Bitmap image = new Bitmap(canvas.Width, canvas.Height))
                {
                    canvas.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                    image.Save(preview, ImageFormat.Png);
                }
                IntPtr foreground = GetForegroundWindow();

                int previewHeight = pet.Height == 220 ? 280 : 220;
                Call(canvas, "SetHeight", previewHeight);
                Pump(450);
                Require(pet.Height == previewHeight, "Sliding should preview the selected height");
                Require(!File.Exists(isolated), "Preview should not write settings");
                Call(menu, "CommitSize");
                Require(store.Load().Height == previewHeight, "Releasing should save the selected height");

                Call(canvas, "SetHeightFromPointer", 22);
                Pump(450);
                Require(canvas.SelectedHeight == 100 && pet.Height == 100,
                    "Left end should make the pet 100 pixels tall");
                Call(menu, "CommitSize");
                Require(store.Load().Height == 100, "Left end should persist");

                Call(canvas, "SetHeightFromPointer", 242);
                Pump(450);
                Require(canvas.SelectedHeight == 500 && pet.Height == 500,
                    "Right end should make the pet 500 pixels tall");
                Call(menu, "CommitSize");
                Require(store.Load().Height == 500, "Right end should persist");
                Require(GetForegroundWindow() == foreground, "Slider test should not take keyboard focus");
                Require(!pet.Visible, "Slider test should not show a pet window");

                for (int i = 0; i < 5; i++) Click(canvas, 234, 239);
                Require(canvas.SelectedUiScalePercent == 150 && settings.UiScalePercent == 150 &&
                    store.Load().UiScalePercent == 150 &&
                    store.Load().UiScaleVersion == PetSettings.CurrentUiScaleVersion,
                    "UI scale should reach and save 150 percent without re-migration");
                Require(canvas.Size == new Size(475, 623) && menu.Size == canvas.Size,
                    "Canvas, click area, and popup should scale together");
                Require(pet.Height == 500, "UI scale must not change character height");
                string large = Path.Combine(args[0], "test-artifacts", "screenshots", "pet-menu-150.png");
                using (Bitmap image = new Bitmap(canvas.Width, canvas.Height))
                {
                    canvas.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                    image.Save(large, ImageFormat.Png);
                }
                Click(canvas, 234, 239);
                Require(canvas.SelectedUiScalePercent == 150,
                    "UI scale must stop at 150 percent");
                for (int i = 0; i < 7; i++) Click(canvas, 167, 239);
                Require(canvas.SelectedUiScalePercent == 80 && settings.UiScalePercent == 80 &&
                    store.Load().UiScalePercent == 80 && canvas.Size == new Size(253, 332),
                    "UI scale should reach and save 80 percent with scaled click targets");
                Click(canvas, 167, 239);
                Require(canvas.SelectedUiScalePercent == 80,
                    "UI scale must stop at 80 percent");
                Click(canvas, 234, 239);
                Click(canvas, 234, 239);
                Require(settings.UiScalePercent == 100 && store.Load().UiScalePercent == 100,
                    "UI scale should restore 100 percent independently");
                Require(GetForegroundWindow() == foreground && !pet.Visible,
                    "UI scale buttons should preserve keyboard focus");
                Console.WriteLine("PASS: menu render, pet size endpoints, UI scale 80/150 percent, settings migration and focus");

                if (args.Length > 1 && args[1] == "--visible")
                {
                    menu.ShowAt(new Point(24, 24), settings.Height, settings.TopMost,
                        settings.UiScalePercent);
                    Pump(180);
                    Require(menu.Visible, "Popup should be visible on screen");
                    Rectangle bounds = menu.Bounds;
                    Rectangle work = Screen.FromRectangle(bounds).WorkingArea;
                    Require(work.Contains(bounds), "Popup should stay inside the visible work area");
                    string live = Path.Combine(args[0], "test-artifacts", "screenshots", "pet-menu-live.png");
                    using (Bitmap capture = new Bitmap(bounds.Width, bounds.Height))
                    using (Graphics graphics = Graphics.FromImage(capture))
                    {
                        graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                        capture.Save(live, ImageFormat.Png);
                    }
                    Click(canvas, 234, 239);
                    Pump(50);
                    Require(menu.Visible && settings.UiScalePercent == 110 &&
                        menu.Size == canvas.Size && work.Contains(menu.Bounds),
                        "Visible menu should resize in place and remain on screen");
                    string zoomed = Path.Combine(args[0], "test-artifacts", "screenshots",
                        "pet-menu-live-110.png");
                    using (Bitmap capture = new Bitmap(menu.Width, menu.Height))
                    using (Graphics graphics = Graphics.FromImage(capture))
                    {
                        graphics.CopyFromScreen(menu.Location, Point.Empty, menu.Size);
                        capture.Save(zoomed, ImageFormat.Png);
                    }
                    Click(canvas, 167, 239);
                    Require(settings.UiScalePercent == 100 && menu.Size == new Size(317, 415),
                        "Visible menu should shrink back to its original size");
                    Call(canvas, "OnMouseDown", MouseAt(canvas, MouseButtons.Left, 1, 242, 116));
                    Call(canvas, "OnMouseMove", MouseAt(canvas, MouseButtons.Left, 0, 132, 116));
                    Call(canvas, "OnMouseMove", MouseAt(canvas, MouseButtons.Left, 0, 22, 116));
                    Require(menu.Visible && canvas.SelectedHeight == 100,
                        "Dragging the thumb should keep the menu open and reach the left end");
                    Call(canvas, "OnMouseUp", MouseAt(canvas, MouseButtons.Left, 1, 22, 116));
                    Require(pet.Height == 100 && store.Load().Height == 100,
                        "Releasing a drag should apply and save the final height");
                    Call(canvas, "SetHeight", 460);
                    menu.Close();
                    Pump(50);
                    Require(store.Load().Height == 460,
                        "Closing the menu should commit a pending size change");
                    Require(!menu.Visible && GetForegroundWindow() == foreground,
                        "Opening and closing the popup should preserve focus");

                    menu.ShowAt(new Point(24, 24), settings.Height, settings.TopMost,
                        settings.UiScalePercent);
                    Click(canvas, 187, 193);
                    Require(!settings.TopMost && !store.Load().TopMost && !menu.Visible,
                        "Bottom segment should apply and close the menu");
                    menu.ShowAt(new Point(24, 24), settings.Height, settings.TopMost,
                        settings.UiScalePercent);
                    Click(canvas, 74, 193);
                    Require(settings.TopMost && store.Load().TopMost && !menu.Visible,
                        "Top segment should apply and close the menu");
                    Point corner = new Point(work.Right - 5, work.Bottom - 5);
                    menu.ShowAt(corner, settings.Height, settings.TopMost,
                        settings.UiScalePercent);
                    for (int i = 0; i < 5; i++) Click(canvas, 234, 239);
                    Pump(50);
                    Require(menu.Visible && menu.Size == new Size(475, 623) &&
                        work.Contains(menu.Bounds) && store.Load().UiScalePercent == 150,
                        "150 percent menu should stay visible near the screen corner");
                    string largeLive = Path.Combine(args[0], "test-artifacts", "screenshots",
                        "pet-menu-live-150.png");
                    using (Bitmap capture = new Bitmap(menu.Width, menu.Height))
                    using (Graphics graphics = Graphics.FromImage(capture))
                    {
                        graphics.CopyFromScreen(menu.Location, Point.Empty, menu.Size);
                        capture.Save(largeLive, ImageFormat.Png);
                    }
                    for (int i = 0; i < 5; i++) Click(canvas, 167, 239);
                    Require(settings.UiScalePercent == 100 && store.Load().UiScalePercent == 100,
                        "Visible scale changes should save when returned to 100 percent");
                    menu.Close();
                    Require(GetForegroundWindow() == foreground,
                        "Menu commands should preserve keyboard focus");
                    Console.WriteLine("PASS: live popup position, close-save, layer controls and focus");
                }
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
