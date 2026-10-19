using System;
using System.IO;

namespace MakiseDesktopPet
{
    internal static class OutfitCatalog
    {
        internal const string OriginalId = "original";

        internal sealed class Outfit
        {
            internal readonly string Id;
            internal readonly string Name;

            internal Outfit(string id, string name)
            {
                Id = id;
                Name = name;
            }
        }

        internal static readonly Outfit[] All =
        {
            new Outfit(OriginalId, "原版"),
            new Outfit("lab-coat", "实验服"),
            new Outfit("maid", "女仆装"),
            new Outfit("yukata", "浴衣"),
            new Outfit("christmas-v2", "圣诞装"),
            new Outfit("red-dress-handfix", "红礼服"),
            new Outfit("beachwear-v2", "运动泳装"),
            new Outfit("battle-suit", "战斗装")
        };

        internal static string Normalize(string id)
        {
            foreach (Outfit outfit in All)
                if (string.Equals(outfit.Id, id, StringComparison.Ordinal))
                    return outfit.Id;
            return OriginalId;
        }

        internal static string DisplayName(string id)
        {
            foreach (Outfit outfit in All)
                if (string.Equals(outfit.Id, id, StringComparison.Ordinal))
                    return outfit.Name;
            return All[0].Name;
        }

        internal static string SpriteDirectory(string root, string id)
        {
            string known = Normalize(id);
            return known == OriginalId
                ? Path.Combine(root, "assets", "sprites")
                : Path.Combine(root, "assets", "outfits", known);
        }
    }
}
