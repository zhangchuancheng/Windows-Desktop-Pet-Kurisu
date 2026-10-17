using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace MakiseDesktopPet
{
    internal sealed class ChatPersona
    {
        public string source { get; set; }
        public string source_character_version { get; set; }
        public string identity { get; set; }
        public string voice { get; set; }
        public string boundaries { get; set; }

        internal static ChatPersona Load(string directory)
        {
            string path = Path.Combine(directory, "chat-persona.json");
            if (!File.Exists(path))
                throw new FileNotFoundException("缺少本地聊天角色设定文件 chat-persona.json。", path);
            ChatPersona persona = new JavaScriptSerializer().Deserialize<ChatPersona>(
                File.ReadAllText(path, Encoding.UTF8));
            if (persona == null || String.IsNullOrWhiteSpace(persona.identity) ||
                String.IsNullOrWhiteSpace(persona.voice) ||
                String.IsNullOrWhiteSpace(persona.boundaries))
                throw new InvalidDataException("chat-persona.json 缺少必要的角色设定。");
            if (persona.identity.Length + persona.voice.Length + persona.boundaries.Length > 5000)
                throw new InvalidDataException("chat-persona.json 太长，请保留适合桌宠短聊的精简设定。");
            return persona;
        }

        internal string BuildSystemPrompt(ChatPreferences preferences)
        {
            StringBuilder prompt = new StringBuilder();
            prompt.AppendLine(identity);
            prompt.AppendLine(voice);
            prompt.AppendLine(boundaries);
            prompt.AppendLine("下面是用户在本机填写的偏好；当次用户消息表达了新的偏好时，以当次消息为准。");
            if (preferences.PreferredName.Length > 0)
                prompt.AppendLine("用户希望被称呼为：" + preferences.PreferredName);
            if (preferences.Interests.Length > 0)
                prompt.AppendLine("用户感兴趣的话题：" + preferences.Interests);
            if (preferences.AvoidTopics.Length > 0)
                prompt.AppendLine("用户希望避开的话题：" + preferences.AvoidTopics);
            prompt.AppendLine(preferences.ReplyLength == "balanced"
                ? "回复长度偏好：适中。通常两到四句，解释问题时可以稍多，但不写长篇剧情。"
                : "回复长度偏好：简短。通常一到两句，需要解释时再稍长一些。");
            return prompt.ToString();
        }
    }
}
