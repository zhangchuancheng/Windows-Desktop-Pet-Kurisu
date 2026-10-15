using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace MakiseDesktopPet
{
    internal static class DialogueSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Read(object o, string n) { return o.GetType().GetField(n, Private).GetValue(o); }
        private static void Write(object o, string n, object v) { o.GetType().GetField(n, Private).SetValue(o, v); }
        private static object Call(object o, string n, params object[] a) { return o.GetType().GetMethod(n, Private).Invoke(o, a); }
        private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        private static Dictionary<string, string[]> groups;
        private static bool In(string category, string text) { return Array.IndexOf(groups[category], text) >= 0; }
        private static bool ForPose(Pose pose, string text)
        {
            if (pose == Pose.PokeBlock) return In("click-block", text) || In("thought", text);
            if (pose == Pose.PokeFold) return In("click-fold", text) || In("thought", text);
            if (pose == Pose.PokeCheek) return In("click-cheek", text) || In("company", text);
            return In("thought", text) || In("company", text);
        }

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
            string filename = Path.Combine(root, "dialogues.json");
            groups = new JavaScriptSerializer().Deserialize<Dictionary<string, string[]>>(File.ReadAllText(filename));
            Dialogues lines = new Dialogues(filename, new Random(42));
            HashSet<string> all = new HashSet<string>();
            foreach (string category in Dialogues.Categories)
            {
                foreach (string text in groups[category]) Require(all.Add(text), "Duplicate or overlapping line");
                string previous = null;
                for (int cycle = 0; cycle < 20; cycle++)
                {
                    HashSet<string> round = new HashSet<string>();
                    for (int i = 0; i < groups[category].Length; i++)
                    {
                        string text = lines.Pick(category);
                        Require(text != previous && In(category, text) && round.Add(text), "Shuffle coverage/repetition failure");
                        previous = text;
                    }
                }
            }
            Require(all.Count == 60, "Expected 60 distinct lines");
            string last = null;
            Pose[] poses = { Pose.PokeBlock, Pose.PokeFold, Pose.PokeCheek };
            int[] specific = new int[3];
            for (int i = 0; i < 6000; i++)
            {
                int slot = i % 3;
                string text = lines.PickClick(poses[slot]);
                Require(ForPose(poses[slot], text) && text != last, "Wrong expression dialogue or repeated line");
                if (In(new[] { "click-block", "click-fold", "click-cheek" }[slot], text)) specific[slot]++;
                last = text;
            }
            foreach (int count in specific) Require(count > 1300 && count < 1700, "Unexpected dialogue mixing ratio");
            Console.WriteLine("PASS: 60 distinct lines, 20 complete shuffled rounds per category, 6000 pose-matched selections without adjacent repeats.");

            using (BubbleForm bubble = new BubbleForm())
                foreach (string text in all)
                    foreach (BubblePlacement placement in Enum.GetValues(typeof(BubblePlacement)))
                    {
                        bubble.Render(text, placement);
                        Require(bubble.Width <= 296 && bubble.Height <= 180, "Expanded dialogue exceeded compact bubble bounds");
                    }
            Console.WriteLine("PASS: all 60 lines render in four bubble directions within compact bounds.");

            string output = Path.Combine(root, "test-artifacts", "dialogues");
            Directory.CreateDirectory(output);
            using (PetForm pet = new PetForm(root))
            {
                ((NotifyIcon)Read(pet, "tray")).Visible = false;
                Write(Read(pet, "settingsStore"), "PathOnDisk", Path.Combine(output, "isolated-settings.json"));
                try
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Write(pet, "pressed", true);
                        Call(pet, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
                        Require((Pose)Read(pet, "pose") == poses[i] && ForPose(poses[i], (string)Read(pet, "bubbleText")), "Actual click routing mismatch");
                    }
                    Write(pet, "pressed", true);
                    Call(pet, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
                    string rapid = (string)Read(pet, "bubbleText");
                    long cooldown = (long)Read(pet, "rapidCooldownUntil");
                    Require(In("rapid", rapid) && cooldown > 0, "Rapid response missing");
                    for (int i = 0; i < 4; i++)
                    {
                        Write(pet, "pressed", true);
                        Call(pet, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
                    }
                    Require((string)Read(pet, "bubbleText") == rapid && (long)Read(pet, "rapidCooldownUntil") == cooldown, "Rapid bubble/cooldown spam");
                    Write(pet, "specialUntil", 0L);
                    Write(pet, "rapidPoseUntil", 0L);
                    Call(pet, "OnTimerTick", null, EventArgs.Empty);
                    Write(pet, "pressed", true);
                    Call(pet, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
                    Require(ForPose((Pose)Read(pet, "pose"), (string)Read(pet, "bubbleText")), "Cooldown did not resume normal lines");
                    int index = (int)Read(pet, "nextClickPose");
                    Write(pet, "pressed", true); Write(pet, "dragging", true);
                    Call(pet, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
                    Require(In("drag", (string)Read(pet, "bubbleText")) && (int)Read(pet, "nextClickPose") == index, "Drag routing advanced click sequence");
                    Call(pet, "ChangeDialogue");
                    Require(ForPose(Pose.Idle, (string)Read(pet, "bubbleText")), "Idle menu line describes a click gesture");
                    pet.EnterSleep();
                    Call(pet, "ChangeDialogue");
                    Require(!(bool)Read(pet, "sleeping") && (Pose)Read(pet, "pose") == Pose.Wake && In("wake", (string)Read(pet, "bubbleText")), "Sleep menu did not wake coherently");
                    Require((int)Read(pet, "nextClickPose") == index, "Menu/wake advanced click sequence");
                    Require(!pet.Visible && !((BubbleForm)Read(pet, "bubble")).Visible, "Test showed a window");
                    Console.WriteLine("PASS: hidden PetForm clicks, rapid cooldown, drag, menu conversation and menu wake routing; click sequence preserved.");
                }
                finally { Call(pet, "OnFormClosed", new FormClosedEventArgs(CloseReason.ApplicationExitCall)); }
            }
        }
    }
}
