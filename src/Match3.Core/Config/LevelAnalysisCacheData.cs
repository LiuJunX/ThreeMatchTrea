using System;

namespace Match3.Core.Config;

/// <summary>
/// 关卡分析缓存数据
/// </summary>
[Serializable]
public class LevelAnalysisCacheData
{
    /// <summary>通过率 (0-1)</summary>
    public float WinRate { get; set; }

    /// <summary>死锁率 (0-1)</summary>
    public float DeadlockRate { get; set; }

    /// <summary>平均使用步数</summary>
    public float AverageMovesUsed { get; set; }

    /// <summary>难度评级</summary>
    public string Difficulty { get; set; } = "";

    /// <summary>分析时的模拟次数</summary>
    public int SimulationCount { get; set; }

    /// <summary>分析时间 (DateTime.Ticks)</summary>
    public long AnalyzedAtTicks { get; set; }

    /// <summary>分析时间</summary>
    public DateTime AnalyzedAt
    {
        get => new DateTime(AnalyzedAtTicks);
        set => AnalyzedAtTicks = value.Ticks;
    }
}
