using System.Collections.Generic;
using Match3.Core.Models.Gameplay;

namespace Match3.Editor.Models
{
    /// <summary>
    /// 当前关卡上下文 - 提供给 AI 的信息
    /// </summary>
    public class LevelContext
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int MoveLimit { get; set; }
        public LevelObjective[]? Objectives { get; set; }
        public string? GridSummary { get; set; }
        public float? WinRate { get; set; }
        public string? DifficultyText { get; set; }
    }

    /// <summary>
    /// AI 响应结果
    /// </summary>
    public class AIChatResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<LevelIntent> Intents { get; set; } = new List<LevelIntent>();
        public string? Error { get; set; }

        /// <summary>
        /// 是否使用了深度思考
        /// </summary>
        public bool UsedDeepThinking { get; set; }
    }

    /// <summary>
    /// AI 处理进度状态
    /// </summary>
    public static class AIProgressStatus
    {
        public const string Thinking = "思考中...";
        public const string DeepThinking = "💭 深度思考中...";
        public const string Executing = "执行操作...";
        public const string Analyzing = "分析关卡...";
    }

    /// <summary>
    /// 关卡操作意图
    /// </summary>
    public class LevelIntent
    {
        public LevelIntentType Type { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

        public int GetInt(string key, int defaultValue = 0)
        {
            if (Parameters.TryGetValue(key, out var value))
            {
                if (value is int i) return i;
                if (value is long l) return (int)l;
                if (value is double d) return (int)d;
                if (int.TryParse(value?.ToString(), out var parsed)) return parsed;
            }
            return defaultValue;
        }

        public string GetString(string key, string defaultValue = "")
        {
            if (Parameters.TryGetValue(key, out var value))
                return value?.ToString() ?? defaultValue;
            return defaultValue;
        }

        public T GetEnum<T>(string key, T defaultValue = default) where T : struct
        {
            if (Parameters.TryGetValue(key, out var value))
            {
                var str = value?.ToString();
                if (!string.IsNullOrEmpty(str) && System.Enum.TryParse<T>(str, true, out var result))
                    return result;
            }
            return defaultValue;
        }
    }

    /// <summary>
    /// 关卡操作意图类型
    /// </summary>
    public enum LevelIntentType
    {
        None,
        SetGridSize,
        SetMoveLimit,
        SetObjective,
        AddObjective,
        RemoveObjective,
        PaintTile,
        PaintTileRegion,
        PaintCover,
        PaintCoverRegion,
        PaintGround,
        PaintGroundRegion,
        PlaceBomb,
        GenerateRandomLevel,
        GenerateLevelByDifficulty,
        ClearRegion,
        ClearAll
    }
}
