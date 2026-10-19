using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace MakiseDesktopPet
{
    internal sealed class PetSettings
    {
        internal const int MinimumHeight = 100;
        internal const int MaximumHeight = 500;
        internal const int MinimumUiScalePercent = 80;
        internal const int MaximumUiScalePercent = 150;
        internal const int DefaultUiScalePercent = 100;
        internal const int CurrentUiScaleVersion = 2;
        public int X { get; set; }
        public int Y { get; set; }
        public int Height { get; set; }
        public bool TopMost { get; set; }
        public bool HasPosition { get; set; }
        public int UiScalePercent { get; set; }
        public int UiScaleVersion { get; set; }
        public string OutfitId { get; set; }

        internal static PetSettings Defaults()
        {
            return new PetSettings { Height = 220, TopMost = true, HasPosition = false,
                UiScalePercent = DefaultUiScalePercent,
                UiScaleVersion = CurrentUiScaleVersion,
                OutfitId = OutfitCatalog.OriginalId };
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
                result.Height = Math.Max(PetSettings.MinimumHeight,
                    Math.Min(PetSettings.MaximumHeight, result.Height));
                // The new 100% is the previous 120%. Preserve the displayed size
                // when reading settings saved before the reference size changed.
                if (result.UiScaleVersion < PetSettings.CurrentUiScaleVersion)
                {
                    result.UiScalePercent = result.UiScalePercent > 0
                        ? (int)Math.Round(result.UiScalePercent / 12d) * 10
                        : PetSettings.DefaultUiScalePercent;
                    result.UiScaleVersion = PetSettings.CurrentUiScaleVersion;
                }
                if (result.UiScalePercent == 0)
                    result.UiScalePercent = PetSettings.DefaultUiScalePercent;
                result.UiScalePercent = Math.Max(PetSettings.MinimumUiScalePercent,
                    Math.Min(PetSettings.MaximumUiScalePercent, result.UiScalePercent));
                result.OutfitId = OutfitCatalog.Normalize(result.OutfitId);
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
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    try
                    {
                        File.Replace(temporary, PathOnDisk, null);
                        break;
                    }
                    catch (PlatformNotSupportedException)
                    {
                        File.Copy(temporary, PathOnDisk, true);
                        File.Delete(temporary);
                        break;
                    }
                    catch (IOException)
                    {
                        if (attempt == 3) throw;
                        Thread.Sleep(35);
                    }
                }
            }
            else File.Move(temporary, PathOnDisk);
        }
    }
}
