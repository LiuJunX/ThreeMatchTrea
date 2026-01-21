using System;
using System.Collections.Generic;

namespace Match3.Editor.Models
{
    /// <summary>
    /// 对话角色
    /// </summary>
    public enum ChatRole
    {
        User,
        Assistant,
        System
    }

    /// <summary>
    /// 对话消息
    /// </summary>
    public class ChatMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public ChatRole Role { get; set; }
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public List<LevelIntent>? Intents { get; set; }
        public bool IsStreaming { get; set; }
        public bool IsError { get; set; }

        public static ChatMessage User(string content) => new ChatMessage
        {
            Role = ChatRole.User,
            Content = content
        };

        public static ChatMessage Assistant(string content, List<LevelIntent>? intents = null) => new ChatMessage
        {
            Role = ChatRole.Assistant,
            Content = content,
            Intents = intents
        };

        public static ChatMessage Error(string error) => new ChatMessage
        {
            Role = ChatRole.Assistant,
            Content = error,
            IsError = true
        };
    }
}
