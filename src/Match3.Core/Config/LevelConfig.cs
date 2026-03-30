using System;
using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;

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
    /// JSON: base64 string (1 byte per cell).
    /// </summary>
    public byte[] GroundHealths { get; set; }

    /// <summary>
    /// Cover layer configuration.
    /// </summary>
    public CoverType[] Covers { get; set; }

    /// <summary>
    /// Cover health values (optional, defaults to type's default health).
    /// JSON: base64 string (1 byte per cell).
    /// </summary>
    public byte[] CoverHealths { get; set; }

    /// <summary>
    /// Obstacle layer configuration (Box, Bush, Safe, etc.).
    /// </summary>
    public ObstacleType[] Obstacles { get; set; }

    /// <summary>
    /// Obstacle initial stage/HP values (optional, defaults to type's default stage).
    /// JSON: base64 string (1 byte per cell).
    /// </summary>
    public byte[] ObstacleStages { get; set; }

    /// <summary>
    /// Obstacle state values (optional, defaults to 0).
    /// Usage depends on type: ColorBox/Curtain = color variant (ElementType byte).
    /// JSON: base64 string (1 byte per cell).
    /// </summary>
    public byte[] ObstacleStates { get; set; }

    /// <summary>
    /// Tile initial stage/HP values (optional, defaults to 1 for normal tiles).
    /// Moving obstacles on tile layer use Stage > 1 for multi-hit damage.
    /// JSON: base64 string (1 byte per cell).
    /// </summary>
    public byte[] TileStages { get; set; }

    public int MoveLimit { get; set; } = 20;

    /// <summary>
    /// Target difficulty for spawn model (0.0 = easy, 1.0 = hard).
    /// Default 0.5 for medium difficulty.
    /// </summary>
    public float TargetDifficulty { get; set; } = 0.5f;

    /// <summary>
    /// Number of distinct tile colors (2-6). Overrides the global default when set.
    /// null = use global default from GameServiceConfiguration.
    /// </summary>
    public int? TileTypesCount { get; set; }

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
    /// Linked random groups (R-elements).
    /// Each entry maps a group name (e.g. "R1") to an array of linear cell indices.
    /// All cells in the same group get the same random color at board initialization.
    /// The color is re-randomized each time the level starts (not stored in Grid).
    /// Example JSON: { "R1": [6, 8, 16], "R2": [12, 14] }
    /// </summary>
    public Dictionary<string, int[]>? LinkedGroups { get; set; }

    /// <summary>
    /// Per-spawner configurations (optional).
    /// Each spawner controls element generation for specific columns.
    /// Columns not assigned to any spawner use global default behavior.
    /// null or empty = all columns share default behavior.
    /// </summary>
    public SpawnerConfig[]? Spawners { get; set; }

    /// <summary>
    /// Pre-approved seeds from offline analysis (optional).
    /// When present, runtime picks one at random for deterministic Refill/Drop RNG.
    /// Empty or null = use default seed from GameServiceConfiguration.
    /// TODO: Populate via offline analysis pipeline (spawner-architecture §4).
    /// </summary>
    public int[]? ApprovedSeeds { get; set; }

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
        Obstacles = new ObstacleType[size];
        ObstacleStages = new byte[size];
        ObstacleStates = new byte[size];
        TileStages = new byte[size];

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
        Obstacles = new ObstacleType[size];
        ObstacleStages = new byte[size];
        ObstacleStates = new byte[size];
        TileStages = new byte[size];

        Array.Fill(Cells, CellKind.Slot);
    }

    public LevelConfig DeepCopy()
    {
        var copy = new LevelConfig(Width, Height)
        {
            MoveLimit = MoveLimit,
            TargetDifficulty = TargetDifficulty,
            TileTypesCount = TileTypesCount,
            Spawners = DeepCopySpawners(),
            ApprovedSeeds = ApprovedSeeds != null ? (int[])ApprovedSeeds.Clone() : null,
            AnalysisCache = AnalysisCache
        };
        if (Grid != null) Array.Copy(Grid, copy.Grid, Math.Min(Grid.Length, copy.Grid.Length));
        if (Cells != null) Array.Copy(Cells, copy.Cells, Math.Min(Cells.Length, copy.Cells.Length));
        if (Grounds != null) Array.Copy(Grounds, copy.Grounds, Math.Min(Grounds.Length, copy.Grounds.Length));
        if (GroundHealths != null) Array.Copy(GroundHealths, copy.GroundHealths, Math.Min(GroundHealths.Length, copy.GroundHealths.Length));
        if (Covers != null) Array.Copy(Covers, copy.Covers, Math.Min(Covers.Length, copy.Covers.Length));
        if (CoverHealths != null) Array.Copy(CoverHealths, copy.CoverHealths, Math.Min(CoverHealths.Length, copy.CoverHealths.Length));
        if (Obstacles != null) Array.Copy(Obstacles, copy.Obstacles, Math.Min(Obstacles.Length, copy.Obstacles.Length));
        if (ObstacleStages != null) Array.Copy(ObstacleStages, copy.ObstacleStages, Math.Min(ObstacleStages.Length, copy.ObstacleStages.Length));
        if (ObstacleStates != null) Array.Copy(ObstacleStates, copy.ObstacleStates, Math.Min(ObstacleStates.Length, copy.ObstacleStates.Length));
        if (TileStages != null) Array.Copy(TileStages, copy.TileStages, Math.Min(TileStages.Length, copy.TileStages.Length));
        for (int i = 0; i < Objectives.Length; i++)
            copy.Objectives[i] = Objectives[i];
        return copy;
    }

    private SpawnerConfig[]? DeepCopySpawners()
    {
        if (Spawners == null) return null;
        var copy = new SpawnerConfig[Spawners.Length];
        for (int i = 0; i < Spawners.Length; i++)
        {
            var src = Spawners[i];
            copy[i] = new SpawnerConfig
            {
                Id = src.Id,
                Columns = src.Columns != null ? (int[])src.Columns.Clone() : Array.Empty<int>(),
                Weights = src.Weights != null ? new Dictionary<ElementType, int>(src.Weights) : null,
                Preset = src.Preset != null ? new PresetQueueConfig
                {
                    Sequence = src.Preset.Sequence != null ? (ElementType[])src.Preset.Sequence.Clone() : Array.Empty<ElementType>(),
                    Cycles = src.Preset.Cycles
                } : null,
                Phases = DeepCopyPhases(src.Phases)
            };
        }
        return copy;
    }

    private static SpawnerPhaseConfig[]? DeepCopyPhases(SpawnerPhaseConfig[]? phases)
    {
        if (phases == null) return null;
        var copy = new SpawnerPhaseConfig[phases.Length];
        for (int i = 0; i < phases.Length; i++)
        {
            var src = phases[i];
            copy[i] = new SpawnerPhaseConfig
            {
                Trigger = new PhaseTriggerConfig
                {
                    Type = src.Trigger?.Type ?? "",
                    ObstacleType = src.Trigger?.ObstacleType ?? ""
                },
                Preset = src.Preset != null ? new PresetQueueConfig
                {
                    Sequence = src.Preset.Sequence != null
                        ? (ElementType[])src.Preset.Sequence.Clone()
                        : Array.Empty<ElementType>(),
                    Cycles = src.Preset.Cycles
                } : new PresetQueueConfig()
            };
        }
        return copy;
    }
}
