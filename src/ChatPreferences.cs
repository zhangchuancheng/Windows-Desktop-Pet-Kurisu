using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace MakiseDesktopPet
{
    internal sealed class ChatPreferences
    {
        public string PreferredName { get; set; }
        public string ReplyLength { get; set; }
        public string Interests { get; set; }
        public string AvoidTopics { get; set; }

        internal static ChatPreferences Defaults()
        {
            return new ChatPreferences { PreferredName = "", ReplyLength = "short",
                Interests = "", AvoidTopics = "" };
        }

        internal ChatPreferences Normalized()
        {
            return new ChatPreferences
            {
                PreferredName = Clean(PreferredName, 24),
                ReplyLength = ReplyLength == "balanced" ? "balanced" : "short",
                Interests = Clean(Interests, 160),
                AvoidTopics = Clean(AvoidTopics, 160)
            };
        }

        private static string Clean(string value, int limit)
        {
            if (String.IsNullOrWhiteSpace(value)) return "";
            string flattened = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return flattened.Length > limit ? flattened.Substring(0, limit) : flattened;
        }
    }

    internal sealed class ChatPreferencesStore
    {
        internal readonly string PathOnDisk;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

        internal ChatPreferencesStore() : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MakiseDesktopPet", "chat-preferences.json")) { }

        internal ChatPreferencesStore(string path) { PathOnDisk = path; }

        internal ChatPreferences Load()
        {
            try
            {
                if (!File.Exists(PathOnDisk)) return ChatPreferences.Defaults();
                ChatPreferences value = serializer.Deserialize<ChatPreferences>(
                    File.ReadAllText(PathOnDisk, Encoding.UTF8));
                return value == null ? ChatPreferences.Defaults() : value.Normalized();
            }
            catch (Exception) { return ChatPreferences.Defaults(); }
        }

        internal void Save(ChatPreferences value)
        {
            ChatPreferences normalized = value.Normalized();
            Directory.CreateDirectory(Path.GetDirectoryName(PathOnDisk));
            string temporary = PathOnDisk + ".tmp";
            File.WriteAllText(temporary, serializer.Serialize(normalized), Encoding.UTF8);
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
