using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Spawning;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Spawning;

/// <summary>
/// ObjectiveDropCondition 单元测试
/// 测试收集物掉落的 PRD 概率、三维计数器、依赖关系
/// </summary>
public class ObjectiveDropConditionTests
{
    private GameState CreateStateWithSink(int width = 8, int height = 8)
    {
        var state = new GameState(width, height, 6, new SequentialRandom());
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                state.SetTile(x, y, new Tile(y * width + x, ElementType.None, x, y));
        // Place a Sink at bottom-center
        state.Cells[(height - 1) * width + width / 2] = CellKind.Sink;
        return state;
    }

    private GameState CreateStateNoSink(int width = 8, int height = 8)
    {
        var state = new GameState(width, height, 6, new SequentialRandom());
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                state.SetTile(x, y, new Tile(y * width + x, ElementType.None, x, y));
        return state;
    }

    private ObjectiveDropCondition CreateBirdCondition(
        float baseProbability = 0.3f,
        int objectiveIndex = 0,
        int maxPerRound = 1,
        int maxTotal = 10,
        int maxOnBoard = 3)
    {
        return new ObjectiveDropCondition(
            ElementType.Bird,
            baseProbability,
            StubRandom.WithFixedValue(0), // Always rolls 0 → always passes probability
            objectiveIndex,
            new SpawnCounter
            {
                MaxPerRound = maxPerRound,
                MaxTotal = maxTotal,
                MaxOnBoard = maxOnBoard
            },
            relyOn: CellKind.Sink);
    }

    #region Column Distribution (no position bias)

    [Fact]
    public void IsConditionMet_BirdColumn_UniformDistribution()
    {
        // Regression: PRD must not accumulate MissCount across columns.
        // Bird should appear uniformly across all columns, not biased left.
        const int width = 8;
        var columnHits = new int[width];
        const int moves = 2000;

        var rng = new Match3.Random.XorShift64(42);
        var condition = new ObjectiveDropCondition(
            ElementType.Bird, 0.5f, rng, 0,
            new SpawnCounter { MaxPerRound = 1, MaxTotal = moves, MaxOnBoard = 10 },
            relyOn: CellKind.Sink);

        var state = CreateStateWithSink(width);
        var context = SpawnContext.Default;

        for (int move = 0; move < moves; move++)
        {
            state.MoveCount = move;
            for (int x = 0; x < width; x++)
            {
                if (condition.IsConditionMet(ref state, x, in context))
                {
                    columnHits[x]++;
                    condition.Generate(ref state, x, in context);
                }
            }
        }

        int totalHits = 0;
        for (int i = 0; i < width; i++) totalHits += columnHits[i];

        // Skip if too few hits to judge distribution
        Assert.True(totalHits > 100, $"Too few hits ({totalHits}) to judge distribution");

        // Each column should get roughly 1/width of the hits.
        // Allow 3× tolerance for random variance.
        float expected = (float)totalHits / width;
        for (int i = 0; i < width; i++)
        {
            Assert.True(columnHits[i] < expected * 3,
                $"Column {i} got {columnHits[i]} hits (expected ~{expected:F0}); distribution is biased");
        }
    }

    [Fact]
    public void IsConditionMet_RollsOncePerMove_NotPerColumn()
    {
        // With maxPerRound=1 and high probability, Bird should appear in exactly
        // one column per move (not multiple).
        var rng = StubRandom.WithFixedValue(0); // always hits PRD, always picks column 0
        var condition = new ObjectiveDropCondition(
            ElementType.Bird, 0.9f, rng, 0,
            new SpawnCounter { MaxPerRound = 1, MaxTotal = 100, MaxOnBoard = 10 },
            relyOn: CellKind.Sink);

        var state = CreateStateWithSink();
        var context = SpawnContext.Default;
        state.MoveCount = 1;

        int hitCount = 0;
        for (int x = 0; x < state.Width; x++)
        {
            if (condition.IsConditionMet(ref state, x, in context))
                hitCount++;
        }

        // PRD rolls once per move → at most 1 column matches
        Assert.Equal(1, hitCount);
    }

    #endregion

    #region Basic Properties

    [Fact]
    public void Priority_Is400()
    {
        var condition = CreateBirdCondition();
        Assert.Equal(400, condition.Priority);
    }

    [Fact]
    public void Generate_ReturnsBird()
    {
        var condition = CreateBirdCondition();
        var state = CreateStateWithSink();
        var context = SpawnContext.Default;

        Assert.True(condition.IsConditionMet(ref state, 0, in context));
        Assert.Equal(ElementType.Bird, condition.Generate(ref state, 0, in context));
    }

    #endregion

    #region PRD Probability

    [Fact]
    public void IsConditionMet_Fires_WithHighProbability()
    {
        // baseProbability = 0.5, roll = 0 → always triggers
        var condition = new ObjectiveDropCondition(
            ElementType.Bird, 0.5f,
            StubRandom.WithFixedValue(0), 0,
            new SpawnCounter { MaxPerRound = 10, MaxTotal = 100, MaxOnBoard = 10 },
            CellKind.Sink);

        var state = CreateStateWithSink();
        var context = SpawnContext.Default;

        Assert.True(condition.IsConditionMet(ref state, 0, in context));
    }

    [Fact]
    public void IsConditionMet_PrdEscalates_AfterMisses()
    {
        // PRD rolls once per move. Each move increments MoveCount.
        // With C=0.1, threshold grows: 1000, 2000, 3000, ...
        // PRD roll consumes 1 RNG call; on hit, column pick consumes another.
        // Sequence: all PRD rolls are 9000. Column picks (on hit) don't matter
        // since we check all columns.
        var rng = new StubRandom(
            9000, // move 0: PRD roll 9000 > threshold 1000 → miss
            9000, // move 1: PRD roll 9000 > threshold 2000 → miss
            9000, // move 2: 9000 > 3000 → miss
            9000, // move 3: 9000 > 4000 → miss
            9000, // move 4: 9000 > 5000 → miss
            9000, // move 5: 9000 > 6000 → miss
            9000, // move 6: 9000 > 7000 → miss
            9000, // move 7: 9000 > 8000 → miss
            9000, // move 8: 9000 > 9000 → miss
            9000, 0 // move 9: PRD roll 9000 < threshold 10000 → HIT! column pick = 0
        );
        var condition = new ObjectiveDropCondition(
            ElementType.Bird, 0.1f, rng, 0,
            new SpawnCounter { MaxPerRound = 100, MaxTotal = 100, MaxOnBoard = 100 },
            CellKind.Sink);

        var state = CreateStateWithSink();
        var context = SpawnContext.Default;

        int missedMoves = 0;
        bool triggered = false;
        for (int move = 0; move < 15; move++)
        {
            state.MoveCount = move;
            // Check all columns — PRD rolls once per move
            bool hitThisMove = false;
            for (int x = 0; x < state.Width; x++)
            {
                if (condition.IsConditionMet(ref state, x, in context))
                {
                    hitThisMove = true;
                    break;
                }
            }
            if (hitThisMove)
            {
                triggered = true;
                break;
            }
            missedMoves++;
        }

        Assert.True(triggered, $"PRD should eventually trigger; missed {missedMoves} moves");
        Assert.True(missedMoves > 0, "Should have missed at least one move before triggering");
    }

    #endregion

    #region Three-Dimensional Counters

    [Fact]
    public void IsConditionMet_RespectsMaxPerRound()
    {
        var condition = CreateBirdCondition(baseProbability: 0.9f, maxPerRound: 1);
        var state = CreateStateWithSink();
        var context = SpawnContext.Default;

        // First spawn should succeed
        Assert.True(condition.IsConditionMet(ref state, 0, in context));
        condition.Generate(ref state, 0, in context);

        // Second spawn in same round should fail
        Assert.False(condition.IsConditionMet(ref state, 1, in context));
    }

    [Fact]
    public void IsConditionMet_ResetsPerRound_OnNewMove()
    {
        var condition = CreateBirdCondition(baseProbability: 0.9f, maxPerRound: 1);
        var state = CreateStateWithSink();
        var context = SpawnContext.Default;

        // First spawn
        Assert.True(condition.IsConditionMet(ref state, 0, in context));
        condition.Generate(ref state, 0, in context);

        // Simulate new move
        state.MoveCount++;

        // Should be allowed again in new round
        Assert.True(condition.IsConditionMet(ref state, 0, in context));
    }

    [Fact]
    public void IsConditionMet_RespectsMaxTotal()
    {
        var condition = CreateBirdCondition(baseProbability: 0.9f, maxTotal: 2, maxPerRound: 10);
        var state = CreateStateWithSink();
        var context = SpawnContext.Default;

        // Spawn twice
        for (int i = 0; i < 2; i++)
        {
            Assert.True(condition.IsConditionMet(ref state, 0, in context));
            condition.Generate(ref state, 0, in context);
            state.MoveCount++;
        }

        // Third spawn should fail (maxTotal = 2)
        Assert.False(condition.IsConditionMet(ref state, 0, in context));
    }

    [Fact]
    public void IsConditionMet_RespectsMaxOnBoard()
    {
        var condition = CreateBirdCondition(baseProbability: 0.9f, maxOnBoard: 2, maxPerRound: 10);
        var state = CreateStateWithSink();
        var context = SpawnContext.Default;

        // Place 2 Birds on board
        state.SetTile(0, 0, new Tile(100, ElementType.Bird, 0, 0));
        state.SetTile(1, 0, new Tile(101, ElementType.Bird, 1, 0));

        // Should not spawn more (maxOnBoard = 2)
        Assert.False(condition.IsConditionMet(ref state, 2, in context));
    }

    #endregion

    #region SpawnCounter Split (UpdateRound + CanSpawn)

    [Fact]
    public void SpawnCounter_UpdateRound_ResetsOnNewMove()
    {
        var counter = new SpawnCounter { MaxPerRound = 2, MaxTotal = 100, MaxOnBoard = 100 };

        counter.UpdateRound(0);
        counter.OnSpawned();
        counter.OnSpawned();
        Assert.False(counter.CanSpawn(0)); // maxPerRound reached

        counter.UpdateRound(1); // new move
        Assert.True(counter.CanSpawn(0)); // reset
    }

    [Fact]
    public void SpawnCounter_CanSpawn_PureQuery_NoSideEffects()
    {
        var counter = new SpawnCounter { MaxPerRound = 1, MaxTotal = 100, MaxOnBoard = 100 };

        counter.UpdateRound(0);

        // Call CanSpawn multiple times — should not change result
        Assert.True(counter.CanSpawn(0));
        Assert.True(counter.CanSpawn(0));
        Assert.True(counter.CanSpawn(0));
    }

    [Fact]
    public void SpawnCounter_CanSpawn_RespectsOnBoardCount()
    {
        var counter = new SpawnCounter { MaxPerRound = 100, MaxTotal = 100, MaxOnBoard = 3 };

        counter.UpdateRound(0);
        Assert.True(counter.CanSpawn(2));  // 2 on board, max 3
        Assert.False(counter.CanSpawn(3)); // 3 on board, max 3
    }

    [Fact]
    public void BoardAnalyzer_CountElementOnBoard_CountsCorrectType()
    {
        var state = CreateStateWithSink();
        state.SetTile(0, 0, new Tile(100, ElementType.Bird, 0, 0));
        state.SetTile(1, 0, new Tile(101, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(102, ElementType.Bird, 2, 0));

        Assert.Equal(2, BoardAnalyzer.CountElementOnBoard(ref state, ElementType.Bird));
        Assert.Equal(1, BoardAnalyzer.CountElementOnBoard(ref state, ElementType.Item1));
        Assert.Equal(0, BoardAnalyzer.CountElementOnBoard(ref state, ElementType.Item3));
    }

    #endregion

    #region Selected Column Ineligible

    [Fact]
    public void IsConditionMet_SelectedColumnIneligible_BirdSkipped()
    {
        // When PRD picks a column that RealtimeRefillSystem would skip
        // (e.g., obstacle, Void), Bird is not spawned that move.
        // This is acceptable behavior — PRD already hit, MissCount resets,
        // next move gets a fresh roll.
        var rng = StubRandom.WithFixedValue(0); // PRD hits, picks column 0
        var condition = new ObjectiveDropCondition(
            ElementType.Bird, 0.9f, rng, 0,
            new SpawnCounter { MaxPerRound = 1, MaxTotal = 100, MaxOnBoard = 10 },
            relyOn: CellKind.Sink);

        var state = CreateStateWithSink();
        var context = SpawnContext.Default;
        state.MoveCount = 1;

        // PRD selects column 0, but caller only queries columns 1-7
        // (simulating column 0 being blocked by obstacle)
        bool anyHit = false;
        for (int x = 1; x < state.Width; x++)
        {
            if (condition.IsConditionMet(ref state, x, in context))
                anyHit = true;
        }

        Assert.False(anyHit, "Bird should not spawn in other columns when selected column is skipped");
    }

    #endregion

    #region RelyOn Dependency (nullable)

    [Fact]
    public void IsConditionMet_NullRelyOn_AlwaysPasses()
    {
        // No dependency — should work on any board
        var condition = new ObjectiveDropCondition(
            ElementType.Bird, 0.9f,
            StubRandom.WithFixedValue(0), 0,
            new SpawnCounter { MaxPerRound = 10, MaxTotal = 100, MaxOnBoard = 10 },
            relyOn: null); // no dependency

        var state = CreateStateNoSink(); // no Sink, but relyOn is null
        var context = SpawnContext.Default;

        Assert.True(condition.IsConditionMet(ref state, 0, in context));
    }

    [Fact]
    public void IsConditionMet_RequiresSink_ForBird()
    {
        var condition = CreateBirdCondition(baseProbability: 0.9f);
        var state = CreateStateNoSink(); // No Sink!
        var context = SpawnContext.Default;

        Assert.False(condition.IsConditionMet(ref state, 0, in context));
    }

    [Fact]
    public void IsConditionMet_Passes_WithSinkPresent()
    {
        var condition = CreateBirdCondition(baseProbability: 0.9f);
        var state = CreateStateWithSink();
        var context = SpawnContext.Default;

        Assert.True(condition.IsConditionMet(ref state, 0, in context));
    }

    #endregion

    #region Objective Completion Reduction

    [Fact]
    public void IsConditionMet_ReducedProbability_AfterObjectiveMet()
    {
        // High base probability with reduction after goal met
        var rng = StubRandom.WithFixedValue(500); // Moderate roll
        var condition = new ObjectiveDropCondition(
            ElementType.Bird, 0.3f, rng, 0,
            new SpawnCounter { MaxPerRound = 10, MaxTotal = 100, MaxOnBoard = 10 },
            CellKind.Sink, reductionRate: 0.1f);

        var state = CreateStateWithSink();
        var context = SpawnContext.Default;

        // Mark objective as completed
        state.ObjectiveProgress[0] = new ObjectiveProgress
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Bird,
            TargetCount = 8,
            CurrentCount = 8
        };

        // Count triggers over many attempts — should be much lower
        int triggerCount = 0;
        for (int i = 0; i < 100; i++)
        {
            var testRng = StubRandom.WithFixedValue(500);
            var testCondition = new ObjectiveDropCondition(
                ElementType.Bird, 0.3f, testRng, 0,
                new SpawnCounter { MaxPerRound = 100, MaxTotal = 1000, MaxOnBoard = 100 },
                CellKind.Sink, reductionRate: 0.1f);

            if (testCondition.IsConditionMet(ref state, 0, in context))
                triggerCount++;
        }

        // With 0.3 * 0.1 = 0.03 effective C, first roll threshold = 0.03
        // Roll of 500/10000 = 0.05 > 0.03, so should rarely trigger
        Assert.True(triggerCount < 50,
            $"Triggered {triggerCount}/100 times; expected much less after objective met");
    }

    #endregion

    #region Factory

    [Fact]
    public void FromObjective_CreatesBirdCondition()
    {
        var objective = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Bird,
            TargetCount = 8
        };

        var condition = ObjectiveDropCondition.FromObjective(
            objective, 0, 25, new SequentialRandom());

        Assert.NotNull(condition);
        Assert.Equal(400, condition!.Priority);
    }

    [Fact]
    public void FromObjective_ReturnsNull_ForNonCollectible()
    {
        var objective = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1, // Regular color, not collectible
            TargetCount = 15
        };

        var condition = ObjectiveDropCondition.FromObjective(
            objective, 0, 25, new SequentialRandom());

        Assert.Null(condition);
    }

    [Fact]
    public void FromObjective_ReturnsNull_ForNonTileLayer()
    {
        var objective = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Cover,
            ElementType = (int)CoverType.Cage,
            TargetCount = 10
        };

        var condition = ObjectiveDropCondition.FromObjective(
            objective, 0, 25, new SequentialRandom());

        Assert.Null(condition);
    }

    [Fact]
    public void FromObjective_ReturnsNull_ForObstacleSpawnedCollectible()
    {
        // Pearl is spawned by Oyster death, not from top
        var objective = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Pearl,
            TargetCount = 5
        };

        var condition = ObjectiveDropCondition.FromObjective(
            objective, 0, 25, new SequentialRandom());

        Assert.Null(condition);
    }

    #endregion
}
