using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace MakiseDesktopPet
{
    internal sealed class OllamaChatClient : IDisposable
    {
        internal const string ModelName = "qwen3:4b-instruct";
        private readonly HttpClient http;
        private readonly ChatPersona persona;
        private readonly ChatPreferencesStore preferencesStore;
        private readonly ChatConversation conversation = new ChatConversation();
        private readonly object conversationGate = new object();
        private ChatPreferences preferences;
        private int conversationVersion;
        internal bool LastReplyWasRetried { get; private set; }
        internal bool LastReplyWasFallback { get; private set; }

        internal OllamaChatClient(string directory) : this(directory, new ChatPreferencesStore()) { }

        internal OllamaChatClient(string directory, ChatPreferencesStore store)
        {
            persona = ChatPersona.Load(directory);
            preferencesStore = store;
            preferences = store.Load();
            HttpClientHandler handler = new HttpClientHandler();
            handler.UseProxy = false;
            http = new HttpClient(handler);
            http.BaseAddress = new Uri("http://127.0.0.1:11434/");
            http.Timeout = TimeSpan.FromSeconds(120);
        }

        internal ChatPreferences GetPreferences()
        {
            lock (conversationGate) return preferences.Normalized();
        }

        internal void SavePreferences(ChatPreferences value)
        {
            ChatPreferences normalized = value.Normalized();
            preferencesStore.Save(normalized);
            lock (conversationGate)
            {
                preferences = normalized;
                conversation.Clear();
                conversationVersion++;
            }
        }

        internal async Task<string> SendAsync(string text, CancellationToken cancellation)
        {
            LastReplyWasRetried = false;
            LastReplyWasFallback = false;
            int version;
            string requestJson;
            string userContext;
            lock (conversationGate)
            {
                version = conversationVersion;
                requestJson = BuildRequestJsonCore(text, false);
                userContext = conversation.RecentUserContext;
            }
            string reply = await RequestReplyAsync(requestJson, cancellation)
                .ConfigureAwait(false);
            EnsureCurrentConversation(version, cancellation);
            if (!ChatReplyGuard.Accept(text, reply, userContext))
            {
                LastReplyWasRetried = true;
                lock (conversationGate)
                {
                    EnsureCurrentConversation(version, cancellation);
                    requestJson = BuildRequestJsonCore(text, true);
                }
                reply = await RequestReplyAsync(requestJson, cancellation)
                    .ConfigureAwait(false);
                EnsureCurrentConversation(version, cancellation);
                if (!ChatReplyGuard.Accept(text, reply, userContext))
                {
                    LastReplyWasFallback = true;
                    reply = ChatReplyGuard.Fallback(text);
                }
            }
            lock (conversationGate)
            {
                EnsureCurrentConversation(version, cancellation);
                conversation.AddTurn(text, reply);
            }
            return reply;
        }

        private void EnsureCurrentConversation(int version, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (version != Volatile.Read(ref conversationVersion))
                throw new OperationCanceledException("会话设置已更新。", cancellation);
        }

        private async Task<string> RequestReplyAsync(string json, CancellationToken cancellation)
        {
            using (StringContent content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (HttpResponseMessage response = await http.PostAsync("api/chat", content, cancellation)
                .ConfigureAwait(false))
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                    throw new InvalidOperationException("本机找不到模型。请运行 ollama pull " + ModelName + "。");
                response.EnsureSuccessStatusCode();
                string responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Dictionary<string, object> result = new JavaScriptSerializer()
                    .Deserialize<Dictionary<string, object>>(responseJson);
                object messageObject;
                Dictionary<string, object> message;
                object contentObject;
                if (result == null || !result.TryGetValue("message", out messageObject) ||
                    (message = messageObject as Dictionary<string, object>) == null ||
                    !message.TryGetValue("content", out contentObject) || contentObject == null)
                    throw new InvalidOperationException("本地模型没有返回可读的回复。");
                string reply = contentObject.ToString().Trim();
                if (reply.Length == 0)
                    throw new InvalidOperationException("本地模型返回了空回复，请再试一次。");
                return reply;
            }
        }

        internal string BuildRequestJson(string text)
        {
            lock (conversationGate) return BuildRequestJsonCore(text, false);
        }

        private string BuildRequestJsonCore(string text, bool retry)
        {
            List<ChatMessage> messages = new List<ChatMessage>();
            string instructions = persona.BuildSystemPrompt(preferences) +
                "接续本次对话。若用户用代词、省略或追问，结合之前的用户发言和你的回复理解所指；" +
                "不要把每条消息当作新话题。较新的用户发言优先；前文没有提供的信息就坦言不确定。" +
                "不要编造自己正在做的事、身处的环境或用户未说的事实。" +
                "只输出要显示在桌宠气泡里的中文回复。\n" +
                conversation.EarlierUserFacts;
            if (retry)
                instructions += "上一条候选回复含有未经证实的现实场景。重新回答时仍要承接相关前文，" +
                    "但删去虚构的天气、实验、笔记和自述经历。\n";
            messages.Add(new ChatMessage("system", instructions));
            messages.AddRange(conversation.RecentMessages);
            messages.Add(new ChatMessage("user", text));

            Dictionary<string, object> request = new Dictionary<string, object>();
            request["model"] = ModelName;
            request["messages"] = messages;
            request["stream"] = false;
            request["keep_alive"] = "2m";
            request["options"] = new Dictionary<string, object>
            {
                { "num_ctx", 8192 },
                { "num_predict", preferences.ReplyLength == "balanced" ? 170 : 110 },
                { "temperature", retry ? 0.25 : 0.5 }, { "top_p", 0.9 }
            };
            return new JavaScriptSerializer().Serialize(request);
        }

        public void Dispose() { http.Dispose(); }

    }
}
