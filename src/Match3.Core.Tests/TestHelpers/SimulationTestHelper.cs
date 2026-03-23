using Match3.Core.Config;
using Match3.Core.DependencyInjection;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Matching;
using Match3.Core.Utility;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Tests.TestHelpers;

/// <summary>
/// Shared helpers for simulation invariant, determinism, and round-trip tests.
/// </summary>
public static class SimulationTestHelper
{
    /// <summary>
    /// Creates a fully-wired GameSession via GameServiceBuilder.
    /// </summary>
    public static GameSession CreateSession(int seed, LevelConfig? levelConfig = null, int tileTypesCount = 6)
    {
        var factory = new GameServiceBuilder().UseDefaultServices().Build();
        var config = new GameServiceConfiguration
        {
            RngSeed = seed,
            TileTypesCount = tileTypesCount,
            EnableEventCollection = false,
            SimulationConfig = SimulationConfig.ForAI()
        };
        return factory.CreateGameSession(config, levelConfig);
    }

    /// <summary>
    /// Runs the engine until fully settled — no floating tiles remain.
    /// The engine's IsStable() may return true before gravity processes newly-cleared cells
    /// (match clearing happens AFTER physics in the tick pipeline). This method handles the
    /// multi-phase settling by forcing extra ticks when gaps exist below tiles.
    /// </summary>
    public static void SettleCompletely(SimulationEngine engine, int maxPasses = 20)
    {
        for (int pass = 0; pass < maxPasses; pass++)
        {
            engine.RunUntilStable();
            if (!HasFloatingTiles(engine.State))
                return;
            // Force one tick so gravity discovers the new gaps
            engine.Tick();
        }
        engine.RunUntilStable();
    }

    /// <summary>
    /// Checks if any movable tile sits above an empty playable cell in the same column.
    /// Skips Void cells. Immovable tiles (cover/lock) act as floors.
    /// Returns true if floating tiles exist.
    /// </summary>
    public static bool HasFloatingTiles(GameState state)
    {
        for (int x = 0; x < state.Width; x++)
        {
            bool gapBelow = false;
            for (int y = state.Height - 1; y >= 0; y--)
            {
                int idx = y * state.Width + x;
                if (state.Cells[idx] == CellKind.Void) continue;

                if (state.Grid[idx].Type == ElementType.None)
                    gapBelow = true;
                else if (!state.CanMove(x, y))
                    gapBelow = false; // immovable tile acts as floor
                else if (gapBelow)
                    return true;
                else
                    gapBelow = false;
            }
        }
        return false;
    }

    /// <summary>
    /// Finds valid swap moves and applies one deterministically (index = tickIndex % count).
    /// Returns true if a move was applied.
    /// </summary>
    public static bool TryApplyRandomMove(SimulationEngine engine, int tickIndex)
    {
        var state = engine.State;

        // Check for tappable bombs first
        if (ValidMoveDetector.HasTappableBomb(in state))
        {
            for (int y = 0; y < state.Height; y++)
            {
                for (int x = 0; x < state.Width; x++)
                {
                    var tile = state.GetTile(x, y);
                    if (tile.Type.IsBomb() && state.CanInteract(new Position(x, y)))
                    {
                        engine.ActivateBomb(new Position(x, y));
                        return true;
                    }
                }
            }
        }

        var moves = ValidMoveDetector.FindAllValidMoves(in state, engine.MatchFinder);
        try
        {
            if (moves.Count == 0) return false;
            var move = moves[(tickIndex * 7 + 13) % moves.Count]; // spread selection
            engine.ApplyMove(move.From, move.To);
            return true;
        }
        finally
        {
            Pools.Release(moves);
        }
    }

    /// <summary>
    /// Searches the entire grid for a tile with the given ID.
    /// Returns <c>default</c> (Type == None) if not found.
    /// </summary>
    public static Tile FindTileById(in GameState state, long id)
    {
        for (int i = 0; i < state.Grid.Length; i++)
        {
            if (state.Grid[i].Id == id) return state.Grid[i];
        }
        return default;
    }

    /// <summary>
    /// Asserts two game states are logically equivalent (grid types, IDs, scores, objectives).
    /// </summary>
    public static void AssertStateEqual(GameState a, GameState b, string context = "")
    {
        string Ctx(string field) => string.IsNullOrEmpty(context) ? field : $"{context}: {field}";

        Assert.Equal(a.Width, b.Width);
        Assert.Equal(a.Height, b.Height);
        Assert.Equal(a.Score, b.Score);
        Assert.Equal(a.MoveCount, b.MoveCount);
        Assert.Equal(a.NextTileId, b.NextTileId);
        Assert.Equal(a.LevelStatus, b.LevelStatus);

        int size = a.Width * a.Height;
        for (int i = 0; i < size; i++)
        {
            if (a.Grid[i].Type != b.Grid[i].Type)
                Assert.Fail($"{Ctx("Grid")}: index {i} Type mismatch: {a.Grid[i].Type} vs {b.Grid[i].Type}");
            if (a.Grid[i].Id != b.Grid[i].Id)
                Assert.Fail($"{Ctx("Grid")}: index {i} Id mismatch: {a.Grid[i].Id} vs {b.Grid[i].Id}");
        }

        for (int i = 0; i < 4; i++)
        {
            if (a.ObjectiveProgress[i].CurrentCount != b.ObjectiveProgress[i].CurrentCount)
                Assert.Fail($"{Ctx("Objective")}: slot {i} CurrentCount mismatch: " +
                            $"{a.ObjectiveProgress[i].CurrentCount} vs {b.ObjectiveProgress[i].CurrentCount}");
        }
    }

    /// <summary>
    /// Returns a (LevelConfig, tileTypesCount) pair for the given preset name.
    /// </summary>
    public static (LevelConfig? Config, int TileTypesCount) GetLevelPreset(string name)
    {
        return name switch
        {
            "Standard8x8" => (null, 6),
            "Small5x5_3Colors" => (new LevelConfig(5, 5) { MoveLimit = 30 }, 3),
            "WithCover" => (CreateCoverConfig(), 5),
            "WithGround" => (CreateGroundConfig(), 5),
            "Irregular7x7" => (CreateIrregularConfig(), 5),
            "WithObjectives" => (CreateObjectiveConfig(), 4),
            "WithObstacles" => (CreateObstacleConfig(), 5),
            "WithMailbox" => (CreateMailboxConfig(), 5),
            _ => throw new ArgumentException($"Unknown level preset: {name}")
        };
    }

    public static readonly string[] AllPresetNames =
    {
        "Standard8x8", "Small5x5_3Colors", "WithCover",
        "WithGround", "Irregular7x7", "WithObjectives", "WithObstacles",
        "WithMailbox"
    };

    // ── Level config presets ──────────────────────────────────────────

    private static LevelConfig CreateCoverConfig()
    {
        var config = new LevelConfig(8, 8) { MoveLimit = 25 };
        config.Covers = new CoverType[64];
        config.CoverHealths = new byte[64];

        // Scatter Cage covers on a diagonal
        for (int i = 0; i < 8; i++)
        {
            int idx = i * 8 + i;
            config.Covers[idx] = CoverType.Cage;
            config.CoverHealths[idx] = 1;
        }

        // Some Bubble covers
        config.Covers[2 * 8 + 5] = CoverType.Bubble;
        config.CoverHealths[2 * 8 + 5] = 1;
        config.Covers[4 * 8 + 1] = CoverType.Bubble;
        config.CoverHealths[4 * 8 + 1] = 1;

        return config;
    }

    private static LevelConfig CreateGroundConfig()
    {
        var config = new LevelConfig(8, 8) { MoveLimit = 25 };
        config.Grounds = new GroundType[64];
        config.GroundHealths = new byte[64];

        // Bottom 2 rows covered with Ice (health 2)
        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                int idx = y * 8 + x;
                config.Grounds[idx] = GroundType.Ice;
                config.GroundHealths[idx] = 2;
            }
        }

        return config;
    }

    private static LevelConfig CreateIrregularConfig()
    {
        var config = new LevelConfig(7, 7) { MoveLimit = 25 };
        config.Cells = new CellKind[49];

        // Fill with Slot, then punch Void holes in center cross
        for (int i = 0; i < 49; i++)
            config.Cells[i] = CellKind.Slot;

        // Center 3×3 becomes Void
        for (int dy = 2; dy <= 4; dy++)
            for (int dx = 2; dx <= 4; dx++)
                config.Cells[dy * 7 + dx] = CellKind.Void;

        return config;
    }

    private static LevelConfig CreateObjectiveConfig()
    {
        var config = new LevelConfig(8, 8) { MoveLimit = 30 };
        config.Objectives =
        [
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Item1,
                TargetCount = 15
            },
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Item2,
                TargetCount = 15
            },
            default,
            default
        ];
        return config;
    }

    private static LevelConfig CreateMailboxConfig()
    {
        var config = new LevelConfig(8, 8) { MoveLimit = 30 };

        void PlaceMailbox(int x, int y)
        {
            int i = y * 8 + x;
            config.Obstacles[i] = ObstacleType.Mailbox;
            config.ObstacleStages[i] = 1;
        }

        PlaceMailbox(3, 3);
        PlaceMailbox(5, 5);

        config.Objectives =
        [
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Envelope,
                TargetCount = 4
            },
            default,
            default,
            default
        ];
        return config;
    }

    private static LevelConfig CreateObstacleConfig()
    {
        var config = new LevelConfig(8, 8) { MoveLimit = 30 };

        // Place 4 boxes at varied stages
        void PlaceBox(int x, int y, byte stage)
        {
            int i = y * 8 + x;
            config.Obstacles[i] = ObstacleType.Box;
            config.ObstacleStages[i] = stage;
        }

        PlaceBox(2, 2, 2);
        PlaceBox(5, 2, 1);
        PlaceBox(2, 5, 3);
        PlaceBox(5, 5, 4);

        config.Objectives =
        [
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Obstacle,
                ElementType = (int)ObstacleType.Box,
                TargetCount = 4
            },
            default,
            default,
            default
        ];
        return config;
    }
}

/// <summary>
/// Tracks monotonic values and cell-structure snapshots for invariant checking.
/// </summary>
public sealed class InvariantTracker
{
    public int InitialWidth { get; }
    public int InitialHeight { get; }
    public CellKind[] InitialCells { get; }

    public int PrevScore;
    public int PrevMoveCount;
    public int PrevNextTileId;
    public int[] PrevObjectiveCounts = new int[4];
    public LevelStatus PrevLevelStatus = LevelStatus.InProgress;
    public int PrevTotalCoverHealth;
    public int PrevTotalGroundHealth;

    public InvariantTracker(GameState state)
    {
        InitialWidth = state.Width;
        InitialHeight = state.Height;
        InitialCells = (CellKind[])state.Cells.Clone();

        PrevScore = state.Score;
        PrevMoveCount = state.MoveCount;
        PrevNextTileId = state.NextTileId;
        PrevTotalCoverHealth = ComputeTotalCoverHealth(state);
        PrevTotalGroundHealth = ComputeTotalGroundHealth(state);

        for (int i = 0; i < 4; i++)
            PrevObjectiveCounts[i] = state.ObjectiveProgress[i].CurrentCount;
    }

    public static int ComputeTotalCoverHealth(GameState state)
    {
        int total = 0;
        for (int i = 0; i < state.CoverLayer.Length; i++)
            total += state.CoverLayer[i].Health;
        return total;
    }

    public static int ComputeTotalGroundHealth(GameState state)
    {
        int total = 0;
        for (int i = 0; i < state.GroundLayer.Length; i++)
            total += state.GroundLayer[i].Health;
        return total;
    }
}
