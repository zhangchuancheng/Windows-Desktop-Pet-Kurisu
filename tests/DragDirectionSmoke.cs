using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MakiseDesktopPet
{
    internal static class DragDirectionSmoke
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private static void Require(bool condition, string description)
        {
            if (!condition) throw new Exception(description);
        }

        [STAThread]
        private static void Main(string[] args)
        {
            NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4));
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (SpriteAssets sprites = new SpriteAssets(Path.Combine(args[0], "assets", "sprites"), 220))
            {
                foreach (int height in new[] { 160, 220, 280, 340 })
                {
                    sprites.Resize(height);
                    Bitmap right = sprites.GetDrag(false), left = sprites.GetDrag(true);
                    Require(left.PixelFormat == PixelFormat.Format32bppPArgb, "Mirrored frame must remain premultiplied.");
                    for (int y = 0; y < right.Height; y++)
                        for (int x = 0; x < right.Width; x++)
                            Require(right.GetPixel(x, y).ToArgb() == left.GetPixel(right.Width - 1 - x, y).ToArgb(),
                                "Mirror changed color or alpha.");
                }
            }
            Console.WriteLine("PASS: mirrored color/alpha and layered pixel format at all four sizes.");

            string config = new SettingsStore().PathOnDisk;
            byte[] settings = File.Exists(config) ? File.ReadAllBytes(config) : null;
            IntPtr foreground = GetForegroundWindow();
            try
            {
                using (PetForm pet = new PetForm(args[0]))
                {
                    pet.Show();
                    try
                    {
                        Type type = typeof(PetForm);
                        type.GetField("dragDirectionAnchorX", Private).SetValue(pet, 100);
                        type.GetMethod("SetPose", Private).Invoke(pet, new object[] { Pose.Drag });
                        MethodInfo update = type.GetMethod("UpdateDragDirection", Private);
                        FieldInfo movingLeft = type.GetField("dragMovingLeft", Private);
                        FieldInfo frame = typeof(LayeredForm).GetField("currentFrame", Private);
                        SpriteAssets sprites = (SpriteAssets)type.GetField("sprites", Private).GetValue(pet);
                        int[] positions = { 90, 70, 72, 74, 80, 80, 77, 76 };
                        bool[] expected = { true, true, true, false, false, false, false, true };
                        for (int i = 0; i < positions.Length; i++)
                        {
                            update.Invoke(pet, new object[] { positions[i] });
                            Require((bool)movingLeft.GetValue(pet) == expected[i], "Wrong direction at step " + i);
                            Require(object.ReferenceEquals(frame.GetValue(pet), sprites.GetDrag(expected[i])),
                                "Actual uploaded frame did not follow direction.");
                        }
                        type.GetField("pressed", Private).SetValue(pet, true);
                        type.GetField("dragging", Private).SetValue(pet, true);
                        type.GetMethod("OnMouseUp", Private).Invoke(pet,
                            new object[] { new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0) });
                        Require((Pose)type.GetField("pose", Private).GetValue(pet) == Pose.Idle, "Release did not restore idle.");
                        Require((int)type.GetField("nextClickPose", Private).GetValue(pet) == 0, "Drag release advanced click cycle.");
                        Require(GetForegroundWindow() == foreground, "Pet changed keyboard focus.");
                        Console.WriteLine("PASS: left/right, reversal before start point, jitter, no horizontal movement, actual frame uploads.");
                        Console.WriteLine("PASS: release restores idle without advancing clicks; foreground unchanged.");
                    }
                    finally { pet.Close(); }
                }
            }
            finally
            {
                if (settings != null) File.WriteAllBytes(config, settings);
                else if (File.Exists(config)) File.Delete(config);
            }
        }
    }
}
