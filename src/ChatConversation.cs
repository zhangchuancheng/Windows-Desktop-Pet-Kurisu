using System;
using System.Collections.Generic;
using System.Text;

namespace MakiseDesktopPet
{
    internal sealed class ChatConversation
    {
        private const int MaximumTurns = 12;
        private const int MaximumHistoryCharacters = 3200;
        private const int MaximumNotes = 6;
        private const int MaximumNoteCharacters = 180;
        private const int MaximumNotesCharacters = 720;

        private readonly List<ChatMessage> history = new List<ChatMessage>();
        private readonly List<ConversationNote> notes = new List<ConversationNote>();

        internal IEnumerable<ChatMessage> RecentMessages { get { return history; } }

        internal string EarlierUserFacts
        {
            get
            {
                if (notes.Count == 0) return String.Empty;
                StringBuilder text = new StringBuilder();
                text.AppendLine("本次会话中用户较早说过的原话；可能已过时，只在相关时参考，较新的用户发言优先：");
                foreach (ConversationNote note in notes)
                    text.AppendLine("- " + note.Text);
                return text.ToString();
            }
        }

        internal string RecentUserContext
        {
            get
            {
                StringBuilder text = new StringBuilder();
                foreach (ConversationNote note in notes) text.AppendLine(note.Text);
                for (int index = 0; index < history.Count; index++)
                    if (history[index].role == "user") text.AppendLine(history[index].content);
                return text.ToString();
            }
        }

        internal void AddTurn(string userText, string reply)
        {
            history.Add(new ChatMessage("user", userText));
            history.Add(new ChatMessage("assistant", reply));
            while (history.Count / 2 > MaximumTurns || HistoryCharacters() > MaximumHistoryCharacters)
            {
                RememberIfUseful(history[0].content);
                history.RemoveRange(0, 2);
            }
        }

        internal void Clear()
        {
            history.Clear();
            notes.Clear();
        }

        private int HistoryCharacters()
        {
            int total = 0;
            foreach (ChatMessage message in history) total += message.content.Length;
            return total;
        }

        private void RememberIfUseful(string userText)
        {
            string compact = userText.Trim().Replace('\r', ' ').Replace('\n', ' ');
            if (!IsUsefulFact(compact)) return;
            if (compact.Length > MaximumNoteCharacters)
                compact = compact.Substring(0, MaximumNoteCharacters).TrimEnd() + "…";
            string category = FactCategory(compact);
            for (int index = notes.Count - 1; index >= 0; index--)
                if (notes[index].Text == compact ||
                    (category != null && notes[index].Category == category))
                    notes.RemoveAt(index);
            notes.Add(new ConversationNote(category, compact));
            while (notes.Count > MaximumNotes || NotesCharacters() > MaximumNotesCharacters)
                notes.RemoveAt(0);
        }

        private int NotesCharacters()
        {
            int total = 0;
            foreach (ConversationNote note in notes) total += note.Text.Length;
            return total;
        }

        private static bool IsUsefulFact(string text)
        {
            if (text.Length < 4) return false;
            foreach (string marker in new[] { "记住", "我叫", "叫我", "我的名字", "我在学",
                "我正在学", "我最近在学", "我在做", "我正在做", "我喜欢", "我不喜欢",
                "我讨厌", "我偏好", "我的项目", "这个项目叫", "项目名字", "项目叫",
                "我正在用", "我使用", "我住在", "我的目标" })
                if (text.IndexOf(marker, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        private static string FactCategory(string text)
        {
            if (text.StartsWith("我叫", StringComparison.Ordinal) ||
                text.StartsWith("叫我", StringComparison.Ordinal) ||
                text.StartsWith("我的名字", StringComparison.Ordinal)) return "name";
            if (text.Contains("项目") && (text.Contains("叫") || text.Contains("名字")))
                return "project-name";
            if (text.Contains("我在学") || text.Contains("我正在学") ||
                text.Contains("我最近在学")) return "study";
            return null;
        }

        private sealed class ConversationNote
        {
            internal readonly string Category;
            internal readonly string Text;

            internal ConversationNote(string category, string text)
            {
                Category = category;
                Text = text;
            }
        }
    }

    internal sealed class ChatMessage
    {
        public string role { get; set; }
        public string content { get; set; }

        internal ChatMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }
}
