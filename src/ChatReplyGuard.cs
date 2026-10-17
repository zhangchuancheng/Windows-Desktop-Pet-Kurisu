using System;
using System.Text.RegularExpressions;

namespace MakiseDesktopPet
{
    internal static class ChatReplyGuard
    {
        internal static bool Accept(string userText, string reply)
        {
            return Accept(userText, reply, String.Empty);
        }

        internal static bool Accept(string userText, string reply, string earlierUserText)
        {
            if (String.IsNullOrWhiteSpace(reply) || reply.Length > 500) return false;
            if (WantsStory(userText)) return true;
            string grounding = userText + "\n" + earlierUserText;
            if (Regex.IsMatch(reply, @"\*[^*]{2,80}\*")) return false;
            if (ContainsAny(reply, "我刚在", "我刚从", "我刚把", "我刚做",
                "我正在", "我这边有", "我之前在"))
                return false;
            if (!ContainsAny(grounding, "天气", "下雨", "晴", "气象") &&
                ContainsAny(reply, "天气不错", "天气很好", "下雨了", "天气预报", "气象数据"))
                return false;
            if (!ContainsAny(grounding, "实验", "研究", "科学", "数据", "论文") &&
                ContainsAny(reply, "实验室", "实验数据", "新实验", "论文", "脑电波数据"))
                return false;
            if (!ContainsAny(grounding, "笔记", "清单", "记录") &&
                ContainsAny(reply, "我的笔记", "我有笔记", "我这边有份", "我记录过"))
                return false;
            foreach (string name in new[] { "冈部", "真由理", "桥田", "中钵", "凤凰院" })
                if (reply.Contains(name) && !grounding.Contains(name)) return false;
            return true;
        }

        internal static string Fallback(string userText)
        {
            if (ContainsAny(userText, "报错", "错误", "代码", "程序"))
                return "把报错信息和相关代码发我，我帮你一起查。先别急。";
            if (ContainsAny(userText, "难过", "伤心", "累", "烦死", "低落"))
                return "先歇一会儿吧。想说说怎么回事，我会听。";
            if (ContainsAny(userText, "无聊", "没事做"))
                return "无聊到来找我？好吧，聊个有意思的问题，还是说说今天发生的事？";
            return "这件事我想听你多说一点。你最在意的是哪一部分？";
        }

        private static bool WantsStory(string text)
        {
            return ContainsAny(text, "角色扮演", "写一段剧情", "讲个故事",
                "编个故事", "假设我们在", "想象一下");
        }

        private static bool ContainsAny(string text, params string[] terms)
        {
            foreach (string term in terms)
                if (text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
    }
}
