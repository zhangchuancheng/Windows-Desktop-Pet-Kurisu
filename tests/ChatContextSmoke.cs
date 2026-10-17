using System;
using System.Reflection;
using System.Threading;

namespace MakiseDesktopPet
{
    internal static class ChatContextSmoke
    {
        private static void Main(string[] args)
        {
            ChatConversation context = new ChatConversation();
            context.AddTurn("我的项目叫蓝杉计划。", "记下了，是蓝杉计划。");
            for (int index = 0; index < 13; index++)
                context.AddTurn("第" + index + "轮，我在调整按钮。", "好，继续调整。");
            if (!context.EarlierUserFacts.Contains("蓝杉计划"))
                throw new Exception("An older explicit user fact was forgotten.");
            foreach (ChatMessage message in context.RecentMessages)
                if (message.content.Contains("蓝杉计划"))
                    throw new Exception("An evicted turn remained in recent history.");
            context.AddTurn("我的项目叫青柠计划。", "好，是青柠计划。");
            for (int index = 0; index < 13; index++)
                context.AddTurn("继续调整第" + index + "个按钮。", "好。");
            if (context.EarlierUserFacts.Contains("蓝杉计划") ||
                !context.EarlierUserFacts.Contains("青柠计划"))
                throw new Exception("An outdated project name replaced a newer one.");

            using (OllamaChatClient client = new OllamaChatClient(args[0],
                new ChatPreferencesStore(System.IO.Path.Combine(args[0], "test-artifacts",
                    "chat-context-preferences-smoke.json"))))
            {
                FieldInfo field = typeof(OllamaChatClient).GetField("conversation",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                ChatConversation clientContext = (ChatConversation)field.GetValue(client);
                clientContext.AddTurn("我的项目叫蓝杉计划。", "记下了，是蓝杉计划。");
                for (int index = 0; index < 13; index++)
                    clientContext.AddTurn("第" + index + "轮，我在调整按钮。", "好，继续调整。");
                string payload = client.BuildRequestJson("我刚才说的项目叫什么？");
                if (!payload.Contains("蓝杉计划") || !payload.Contains("我刚才说的项目叫什么") ||
                    payload.Contains("只回应上面的用户原话") || !payload.Contains("8192"))
                    throw new Exception("The chat request lost earlier facts or dialogue context.");

                string answer = client.SendAsync("我刚才说的项目叫什么？",
                    CancellationToken.None).GetAwaiter().GetResult();
                if (!answer.Contains("蓝杉计划"))
                    throw new Exception("The local model failed to recall the stated project name: " + answer);
                client.SavePreferences(ChatPreferences.Defaults());
                if (client.BuildRequestJson("你好").Contains("蓝杉计划"))
                    throw new Exception("Starting a new conversation retained old session facts.");
                Console.WriteLine("PASS: older user fact survived context trimming and model recalled it.");
                Console.WriteLine("Reply: " + answer.Replace('\n', ' '));
            }

            if (!ChatReplyGuard.Accept("那这个呢？", "实验数据要先核对。",
                "我在整理实验数据。"))
                throw new Exception("The guard rejected a topic established by the user earlier.");
            if (!ChatReplyGuard.Accept("你刚才说什么？", "我刚才说的是先核对错误信息。"))
                throw new Exception("The guard rejected a legitimate conversation reference.");
        }
    }
}
