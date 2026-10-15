using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace MakiseDesktopPet
{
    internal static class RapidClickSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Read(object o, string n) { return o.GetType().GetField(n, Private).GetValue(o); }
        private static void Write(object o, string n, object v) { o.GetType().GetField(n, Private).SetValue(o, v); }
        private static object Call(object o, string n, params object[] a) { return o.GetType().GetMethod(n, Private).Invoke(o, a); }
        private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        private static void Click(PetForm pet) { Write(pet, "pressed", true); Call(pet, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0)); }
        private static void Tick(PetForm pet) { Call(pet, "OnTimerTick", null, EventArgs.Empty); }
        private static void Burst(PetForm pet) { for (int i = 0; i < 4; i++) Click(pet); }
        private static long Now(PetForm pet) { return ((Stopwatch)Read(pet, "clock")).ElapsedMilliseconds; }
        [STAThread]
        private static void Main(string[] args)
        {
            try { Run(args[0]); }
            catch (Exception error) { Console.WriteLine("FAIL: " + error); Environment.ExitCode = 1; }
        }
        private static void Run(string root)
        {
            NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4));
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string output = Path.Combine(root, "test-artifacts", "rapid-click");
            Directory.CreateDirectory(output);
            using (PetForm pet = new PetForm(root))
            {
                ((NotifyIcon)Read(pet, "tray")).Visible = false;
                Write(Read(pet, "settingsStore"), "PathOnDisk", Path.Combine(output, "isolated-settings.json"));
                try
                {
                    Pose[] normal = { Pose.PokeBlock, Pose.PokeFold, Pose.PokeCheek };
                    for (int i = 0; i < 3; i++) { Click(pet); Require((Pose)Read(pet, "pose") == normal[i], "Early rapid trigger"); }
                    int index = (int)Read(pet, "nextClickPose");
                    Click(pet);
                    Require((Pose)Read(pet, "pose") == Pose.Rapid && (long)Read(pet, "poseUntil") == 0, "Fourth click did not lock X pose");
                    long bubbleDeadline = (long)Read(pet, "bubbleUntil"), cooldown = (long)Read(pet, "rapidCooldownUntil");
                    string text = (string)Read(pet, "bubbleText");
                    Thread.Sleep(700); Tick(pet);
                    Require((Pose)Read(pet, "pose") == Pose.Rapid, "Old 650ms timeout interrupted X pose");
                    Click(pet);
                    Require((long)Read(pet, "rapidPoseUntil") - Now(pet) > 1100, "Repeat click did not extend pose");
                    Thread.Sleep(700); Tick(pet);
                    Require((Pose)Read(pet, "pose") == Pose.Rapid, "Pose expired relative to first click");
                    Require((long)Read(pet, "bubbleUntil") == bubbleDeadline && (long)Read(pet, "rapidCooldownUntil") == cooldown && (string)Read(pet, "bubbleText") == text, "Repeated click refreshed speech");
                    Require((int)Read(pet, "nextClickPose") == index, "Held reaction consumed normal click poses");
                    Thread.Sleep(550); Tick(pet);
                    Require((Pose)Read(pet, "pose") == Pose.Idle && (long)Read(pet, "rapidPoseUntil") == 0, "Pause did not return to idle");
                    Console.WriteLine("PASS: real timed pause/resume, 1.2s since final click, no 650ms timeout, no speech refresh or click-index advance.");

                    Write(pet, "specialUntil", 0L);
                    Burst(pet);
                    Require((Pose)Read(pet, "pose") == Pose.Rapid && (long)Read(pet, "rapidCooldownUntil") == cooldown && (long)Read(pet, "bubbleUntil") == 0, "Cooldown blocked animation or repeated speech");
                    // Even after speech cooldown expires, an uninterrupted held
                    // reaction must not start another speech on every click.
                    Write(pet, "rapidCooldownUntil", 0L); Click(pet);
                    Require((long)Read(pet, "rapidCooldownUntil") == 0 && (long)Read(pet, "bubbleUntil") == 0, "Sustained clicks restarted speech");
                    Write(pet, "pressed", true); Write(pet, "rapidPoseUntil", 0L); Tick(pet);
                    Require((Pose)Read(pet, "pose") == Pose.Rapid, "Pressed reaction dropped mid-press");
                    Click(pet);
                    foreach (int height in new[] { 160, 220, 280, 340 })
                    {
                        Call(pet, "ChangeSize", height);
                        Require((Pose)Read(pet, "pose") == Pose.Rapid && pet.Height == height, "Resize lost held pose");
                    }
                    Console.WriteLine("PASS: independent speech cooldown, continued hold, pressed state and four sizes.");

                    // Read the pointer only; do not send physical mouse input or capture it.
                    Point pointer = (Point)typeof(PetForm).GetMethod("PhysicalMousePosition", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    Write(pet, "pressed", true);
                    Write(pet, "pressPoint", new Point(pointer.X - 20, pointer.Y));
                    Write(pet, "pressBase", pet.Location);
                    Call(pet, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
                    Require((Pose)Read(pet, "pose") == Pose.Drag && (long)Read(pet, "rapidPoseUntil") == 0, "Drag did not interrupt held X");
                    index = (int)Read(pet, "nextClickPose");
                    Call(pet, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
                    Require((Pose)Read(pet, "pose") == Pose.Idle && (int)Read(pet, "nextClickPose") == index, "Drag release consumed a normal click");
                    Burst(pet); Require((Pose)Read(pet, "pose") == Pose.Rapid, "Fresh streak failed");
                    Call(pet, "HideToTray");
                    Require((Pose)Read(pet, "pose") == Pose.Idle && (long)Read(pet, "rapidPoseUntil") == 0, "Hide retained reaction state");
                    Burst(pet); pet.EnterSleep();
                    Require((Pose)Read(pet, "pose") == Pose.Sleep && (long)Read(pet, "rapidPoseUntil") == 0, "Sleep retained reaction state");
                    Require(!pet.Visible && !((BubbleForm)Read(pet, "bubble")).Visible, "Test showed a window");
                    Console.WriteLine("PASS: actual hidden drag entry interrupts X, release/index, hide and sleep cleanup; no physical input sent.");
                }
                finally { Call(pet, "OnFormClosed", new FormClosedEventArgs(CloseReason.ApplicationExitCall)); }
            }
        }
    }
}
