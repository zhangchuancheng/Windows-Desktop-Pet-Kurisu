using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MakiseDesktopPet
{
    internal static class OutfitMenuInputSmoke
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
        [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
        private static object Read(object o, string name) { return o.GetType().GetField(name, Hidden).GetValue(o); }
        private static void Pump(int ms)
        {
            DateTime end = DateTime.UtcNow.AddMilliseconds(ms);
            do { Application.DoEvents(); Thread.Sleep(10); } while (DateTime.UtcNow < end);
        }
        private static void Click(Point point, bool right)
        {
            SetCursorPos(point.X, point.Y); Pump(100);
            mouse_event(right ? 8u : 2u, 0, 0, 0, UIntPtr.Zero); Pump(60);
            mouse_event(right ? 16u : 4u, 0, 0, 0, UIntPtr.Zero); Pump(250);
        }
        [STAThread] private static void Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "--background")
            {
                using (Form other = new Form())
                {
                    other.Bounds = new Rectangle(40, 40, 220, 200);
                    other.Text = "Outfit input test background";
                    Application.Run(other);
                }
                return;
            }
            PetForm pet = null;
            Process background = null;
            Point originalPointer = Cursor.Position;
            try
            {
                NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4));
                Application.EnableVisualStyles();
                pet = new PetForm(Path.Combine(args[0], "dist"));
                ((NotifyIcon)Read(pet, "tray")).Visible = false;
                object store = Read(pet, "settingsStore");
                store.GetType().GetField("PathOnDisk", Hidden).SetValue(store,
                    Path.Combine(args[0], "test-artifacts", "outfit-input-settings.json"));
                pet.Location = new Point(400, 120);
                typeof(PetForm).GetMethod("ChangeLayer", Hidden).Invoke(pet, new object[] { true });
                pet.Show(); Pump(200);
                PetMenuPopup popup = (PetMenuPopup)Read(pet, "petMenu");
                popup.Closed += delegate(object sender, ToolStripDropDownClosedEventArgs e)
                    { Console.WriteLine("Settings closed: " + e.CloseReason); };
                ContextMenuStrip picker = (ContextMenuStrip)Read(pet, "outfitMenu");
                picker.Closed += delegate(object sender, ToolStripDropDownClosedEventArgs e)
                    { Console.WriteLine("Picker closed: " + e.CloseReason); };
                background = Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--background")
                    { UseShellExecute = false, CreateNoWindow = true });
                Pump(500);
                {
                    for (int attempt = 0; attempt < 16; attempt++)
                    {
                        typeof(PetForm).GetMethod("ChangeLayer", Hidden).Invoke(pet,
                            new object[] { attempt < 8 });
                        background.Refresh();
                        SetForegroundWindow(background.MainWindowHandle); Pump(100);
                        if (GetForegroundWindow() != background.MainWindowHandle)
                            throw new Exception("External foreground prerequisite not met");
                        Click(pet.PointToScreen(new Point(pet.Width / 2, pet.Height / 2)), true);
                        if (!popup.Visible) throw new Exception("Settings did not open, attempt " + attempt);
                        PetMenuCanvas canvas = popup.Canvas;
                        Click(canvas.PointToScreen(new Point((int)(70 * canvas.UiScale),
                            (int)(282 * canvas.UiScale))), false);
                        if (!picker.Visible) throw new Exception("Picker dismissed before selection, attempt " + attempt);
                        int index = attempt % picker.Items.Count;
                        ToolStripItem item = picker.Items[index];
                        Point target = picker.PointToScreen(new Point(item.Bounds.Left + 30,
                            item.Bounds.Top + item.Height / 2));
                        Click(target, false);
                        if ((string)Read(pet, "outfitId") != (string)item.Tag)
                            throw new Exception("Mouse selection lost, attempt " + attempt);
                        Console.WriteLine("PASS: native right-click settings selection " + attempt);
                    }
                    Click(pet.PointToScreen(new Point(pet.Width / 2, pet.Height / 2)), true);
                    keybd_event(27, 0, 0, UIntPtr.Zero);
                    keybd_event(27, 0, 2, UIntPtr.Zero); Pump(150);
                    if (popup.Visible) throw new Exception("Escape did not close settings");
                    Click(pet.PointToScreen(new Point(pet.Width / 2, pet.Height / 2)), true);
                    Click(new Point(80, 100), false);
                    if (popup.Visible) throw new Exception("Outside click did not close settings");
                    Console.WriteLine("PASS: Escape and outside-click dismissal");
                }
            }
            catch (Exception e) { Console.WriteLine("FAIL: " + e); Environment.ExitCode = 1; }
            finally
            {
                if (background != null) { background.CloseMainWindow(); background.Dispose(); }
                if (pet != null) { pet.Close(); pet.Dispose(); }
                SetCursorPos(originalPointer.X, originalPointer.Y);
            }
        }
    }
}
