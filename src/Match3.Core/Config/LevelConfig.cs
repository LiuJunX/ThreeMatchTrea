using System;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;

namespace Match3.Core.Config;

[Serializable]
public class LevelConfig
{
    public int Width { get; set; } = 8;
    public int Height { get; set; } = 8;
    
    // Split TileType[] Grid into Cells and Grid
    // Legacy Grid field is mapped to Grid (ElementType)
    // We should probably have separate arrays in config, but for now we'll reuse Grid for ElementType
    // and assume Cells are mostly Slots unless specified.
    
    /// <summary>
    /// Initial ElementType layout.
    /// </summary>
    public ElementType[] Grid { get; set; }
    
    /// <summary>
    /// Initial CellKind layout (Structure).
    /// </summary>
    public CellKind[] Cells { get; set; }

    /// <summary>
    /// Ground layer configuration.
    /// </summary>
    public GroundType[] Grounds { get; set; }

    /// <summary>
    /// Ground health values (optional, defaults to type's default health).
    /// </summary>
    public byte[] GroundHealths { get; set; }

    /// <summary>
    /// Cover layer configuration.
    /// </summary>
    public CoverType[] Covers { get; set; }

    /// <summary>
    /// Cover health values (optional, defaults to type's default health).
    /// </summary>
    public byte[] CoverHealths { get; set; }

    public int MoveLimit { get; set; } = 20;

    /// <summary>
    /// Target difficulty for spawn model (0.0 = easy, 1.0 = hard).
    /// Default 0.5 for medium difficulty.
    /// </summary>
    public float TargetDifficulty { get; set; } = 0.5f;

    /// <summary>
    /// Level objectives (max 4, fixed size array).
    /// </summary>
    private LevelObjective[] _objectives = new LevelObjective[4];
    public LevelObjective[] Objectives
    {
        get => _objectives;
        set
        {
            // Ensure fixed size of 4
            if (value == null || value.Length == 0)
            {
                _objectives = new LevelObjective[4];
            }
            else if (value.Length < 4)
            {
                _objectives = new LevelObjective[4];
                Array.Copy(value, _objectives, value.Length);
            }
            else if (value.Length > 4)
            {
                _objectives = new LevelObjective[4];
                Array.Copy(value, _objectives, 4);
            }
            else
            {
                _objectives = value;
            }
        }
    }

    /// <summary>
    /// 缓存的分析结果（可选，不影响游戏逻辑）
    /// </summary>
    public LevelAnalysisCacheData? AnalysisCache { get; set; }

    public LevelConfig()
    {
        var size = Width * Height;
        Grid = new ElementType[size];
        Cells = new CellKind[size];
        Grounds = new GroundType[size];
        GroundHealths = new byte[size];
        Covers = new CoverType[size];
        CoverHealths = new byte[size];

        // Default cells to Slot
        Array.Fill(Cells, CellKind.Slot);
    }

    public LevelConfig(int width, int height)
    {
        Width = width;
        Height = height;
        var size = width * height;
        Grid = new ElementType[size];
        Cells = new CellKind[size];
        Grounds = new GroundType[size];
        GroundHealths = new byte[size];
        Covers = new CoverType[size];
        CoverHealths = new byte[size];
        
        Array.Fill(Cells, CellKind.Slot);
    }

    public LevelConfig DeepCopy()
    {
        var copy = new LevelConfig(Width, Height)
        {
            MoveLimit = MoveLimit,
            TargetDifficulty = TargetDifficulty,
            AnalysisCache = AnalysisCache
        };
        Array.Copy(Grid, copy.Grid, Grid.Length);
        Array.Copy(Cells, copy.Cells, Cells.Length);
        Array.Copy(Grounds, copy.Grounds, Grounds.Length);
        Array.Copy(GroundHealths, copy.GroundHealths, GroundHealths.Length);
        Array.Copy(Covers, copy.Covers, Covers.Length);
        Array.Copy(CoverHealths, copy.CoverHealths, CoverHealths.Length);
        for (int i = 0; i < Objectives.Length; i++)
            copy.Objectives[i] = Objectives[i];
        return copy;
    }
}

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
