using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace MakiseDesktopPet
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4));
            }
            catch (EntryPointNotFoundException)
            {
                NativeMethods.SetProcessDPIAware();
            }

            bool firstInstance;
            using (Mutex mutex = new Mutex(true, "Local\\MakiseDesktopPet_8BE4AE7B", out firstInstance))
            {
                if (!firstInstance) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try
                {
                    string directory = AppDomain.CurrentDomain.BaseDirectory;
                    PetForm pet = new PetForm(directory);
                    if (Array.IndexOf(args, "--preview-sleep") >= 0) pet.EnterSleep();
                    if (Array.IndexOf(args, "--preview-chat") >= 0)
                        pet.Shown += delegate { pet.OpenChat(); };
                    Application.Run(pet);
                }
                catch (Exception error)
                {
                    string message = "桌宠启动或运行时出错：\n" + error.Message +
                        "\n\n请确认 assets/sprites、assets/icons、dialogues.json 和 chat-persona.json 与程序在同一目录。";
                    MessageBox.Show(message, "牧濑红莉栖桌宠", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
