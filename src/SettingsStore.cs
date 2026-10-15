using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace MakiseDesktopPet
{
    internal sealed class PetSettings
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Height { get; set; }
        public bool TopMost { get; set; }
        public bool HasPosition { get; set; }

        internal static PetSettings Defaults()
        {
            return new PetSettings { Height = 220, TopMost = true, HasPosition = false };
        }
    }

    internal sealed class SettingsStore
    {
        internal readonly string PathOnDisk;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

        internal SettingsStore()
        {
            PathOnDisk = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MakiseDesktopPet", "settings.json");
        }

        internal PetSettings Load()
        {
            try
            {
                if (!File.Exists(PathOnDisk)) return PetSettings.Defaults();
                PetSettings result = serializer.Deserialize<PetSettings>(File.ReadAllText(PathOnDisk, Encoding.UTF8));
                if (result == null) return PetSettings.Defaults();
                result.Height = Math.Max(120, Math.Min(440, result.Height));
                return result;
            }
            catch (Exception)
            {
                return PetSettings.Defaults();
            }
        }

        internal void Save(PetSettings settings)
        {
            string directory = Path.GetDirectoryName(PathOnDisk);
            Directory.CreateDirectory(directory);
            string temporary = PathOnDisk + ".tmp";
            File.WriteAllText(temporary, serializer.Serialize(settings), Encoding.UTF8);
            if (File.Exists(PathOnDisk))
            {
                try { File.Replace(temporary, PathOnDisk, null); }
                catch (PlatformNotSupportedException)
                {
                    File.Copy(temporary, PathOnDisk, true);
                    File.Delete(temporary);
                }
            }
            else File.Move(temporary, PathOnDisk);
        }
    }
}
