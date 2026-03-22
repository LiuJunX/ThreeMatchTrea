using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Spawning;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Spawning;

/// <summary>
/// ConditionBasedSpawnModel 单元测试
/// 测试条件优先级体系和 SpawnConditionFactory
/// </summary>
public class ConditionBasedSpawnModelTests
{
    private GameState CreateState(int width = 8, int height = 8)
    {
        var state = new GameState(width, height, 6, new SequentialRandom());
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                state.SetTile(x, y, new Tile(y * width + x, ElementType.None, x, y));
        return state;
    }

    private GameState CreateStateWithSink(int width = 8, int height = 8)
    {
        var state = CreateState(width, height);
        state.Cells[(height - 1) * width + width / 2] = CellKind.Sink;
        return state;
    }

    #region Priority Ordering

    [Fact]
    public void Predict_HighPriorityCondition_TakesPrecedence()
    {
        var highPriority = new StubCondition(400, true, ElementType.Bird);
        var lowPriority = new StubCondition(100, true, ElementType.Item1);

        var model = new ConditionBasedSpawnModel(new ISpawnCondition[]
        {
            highPriority, lowPriority
        });

        var state = CreateState();
        var context = SpawnContext.Default;

        var result = model.Predict(ref state, 0, in context);

        Assert.Equal(ElementType.Bird, result);
    }

    [Fact]
    public void Predict_FallsThrough_WhenHighPriorityNotMet()
    {
        var highPriority = new StubCondition(400, false, ElementType.Bird);
        var lowPriority = new StubCondition(100, true, ElementType.Item1);

        var model = new ConditionBasedSpawnModel(new ISpawnCondition[]
        {
            highPriority, lowPriority
        });

        var state = CreateState();
        var context = SpawnContext.Default;

        var result = model.Predict(ref state, 0, in context);

        Assert.Equal(ElementType.Item1, result);
    }

    #endregion

    #region DefaultCondition Fallback

    [Fact]
    public void Predict_DefaultAlwaysMatches()
    {
        var defaultCondition = new DefaultCondition(new SequentialRandom(), 6);
        var model = new ConditionBasedSpawnModel(new ISpawnCondition[]
        {
            defaultCondition
        });

        var state = CreateState();
        var context = SpawnContext.Default;

        var result = model.Predict(ref state, 0, in context);

        Assert.NotEqual(ElementType.None, result);
        Assert.True(result.IsColor());
    }

    #endregion

    #region SpawnConditionFactory

    [Fact]
    public void Factory_CreatesDefaultOnly_WhenNoCollectibleObjectives()
    {
        var objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Item1,
                TargetCount = 15
            },
            new LevelObjective(),
            new LevelObjective(),
            new LevelObjective()
        };

        var model = SpawnConditionFactory.Create(6, objectives, 25, new SequentialRandom(), new SequentialRandom());

        // Only DefaultCondition (no collectibles need spawning)
        Assert.Equal(1, model.ConditionCount);
        Assert.Equal(100, model.GetCondition(0).Priority);
    }

    [Fact]
    public void Factory_CreatesBirdCondition_FromObjective()
    {
        var objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Bird,
                TargetCount = 8
            },
            new LevelObjective(),
            new LevelObjective(),
            new LevelObjective()
        };

        var model = SpawnConditionFactory.Create(6, objectives, 25, new SequentialRandom(), new SequentialRandom());

        // ObjectiveDropCondition (400) + DefaultCondition (100)
        Assert.Equal(2, model.ConditionCount);
        Assert.Equal(400, model.GetCondition(0).Priority);
        Assert.Equal(100, model.GetCondition(1).Priority);
    }

    [Fact]
    public void Factory_SortsByPriorityDescending()
    {
        var objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Bird,
                TargetCount = 8
            },
            new LevelObjective(),
            new LevelObjective(),
            new LevelObjective()
        };

        var model = SpawnConditionFactory.Create(6, objectives, 25, new SequentialRandom(), new SequentialRandom());

        // Verify descending priority order
        for (int i = 1; i < model.ConditionCount; i++)
        {
            Assert.True(model.GetCondition(i - 1).Priority >= model.GetCondition(i).Priority);
        }
    }

    [Fact]
    public void Factory_SkipsInactiveObjectiveSlots()
    {
        var objectives = new[]
        {
            new LevelObjective(), // Inactive
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Bird,
                TargetCount = 8
            },
            new LevelObjective(), // Inactive
            new LevelObjective()  // Inactive
        };

        var model = SpawnConditionFactory.Create(6, objectives, 25, new SequentialRandom(), new SequentialRandom());

        // Only 1 ObjectiveDropCondition + 1 DefaultCondition
        Assert.Equal(2, model.ConditionCount);
    }

    #endregion

    #region Integration

    [Fact]
    public void Predict_SpawnsBird_WhenConditionMet()
    {
        // Use a RNG that always triggers Bird (roll = 0)
        var rng = StubRandom.WithFixedValue(0);
        var objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Bird,
                TargetCount = 8
            },
            new LevelObjective(),
            new LevelObjective(),
            new LevelObjective()
        };

        var model = SpawnConditionFactory.Create(6, objectives, 25, rng, rng);
        var state = CreateStateWithSink();
        var context = SpawnContext.Default;

        var result = model.Predict(ref state, 0, in context);

        Assert.Equal(ElementType.Bird, result);
    }

    [Fact]
    public void Predict_SpawnsColor_WhenBirdConditionNotMet()
    {
        // High roll value → Bird won't trigger
        var rng = StubRandom.WithFixedValue(9999);
        var objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Bird,
                TargetCount = 8
            },
            new LevelObjective(),
            new LevelObjective(),
            new LevelObjective()
        };

        var model = SpawnConditionFactory.Create(6, objectives, 25, rng, rng);
        var state = CreateStateWithSink();
        var context = SpawnContext.Default;

        var result = model.Predict(ref state, 0, in context);

        Assert.True(result.IsColor(), "Should fall through to DefaultCondition for a color");
    }

    [Fact]
    public void Predict_SpawnsColor_WhenNoSink()
    {
        var rng = StubRandom.WithFixedValue(0);
        var objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Bird,
                TargetCount = 8
            },
            new LevelObjective(),
            new LevelObjective(),
            new LevelObjective()
        };

        var model = SpawnConditionFactory.Create(6, objectives, 25, rng, rng);
        var state = CreateState(); // No Sink!
        var context = SpawnContext.Default;

        var result = model.Predict(ref state, 0, in context);

        // Bird condition fails (no Sink), should get a color instead
        Assert.True(result.IsColor());
    }

    #endregion

    #region RNG Isolation

    [Fact]
    public void Factory_IsolatedRng_ColorUnaffectedByBirdObjectivePresence()
    {
        // Model A: has Bird objective but no Sink → Bird condition rejected at RelyOn → dropRng untouched
        // Model B: no Bird objective
        // Same colorRng seed → identical color sequences.
        // Proves: Bird condition's existence doesn't leak into colorRng.
        var birdObjectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Bird,
                TargetCount = 8
            },
            new LevelObjective(),
            new LevelObjective(),
            new LevelObjective()
        };
        var noObjectives = new LevelObjective[4];

        var colorRngA = new Match3.Random.XorShift64(100);
        var colorRngB = new Match3.Random.XorShift64(100);

        var modelA = SpawnConditionFactory.Create(6, birdObjectives, 25,
            colorRngA, new Match3.Random.XorShift64(999));
        var modelB = SpawnConditionFactory.Create(6, noObjectives, 25,
            colorRngB, new Match3.Random.XorShift64(1));

        // No Sink → Bird never fires in model A
        var stateA = CreateState();
        var stateB = CreateState();
        var context = SpawnContext.Default;

        for (int move = 0; move < 10; move++)
        {
            stateA.MoveCount = move;
            stateB.MoveCount = move;
            for (int x = 0; x < 8; x++)
            {
                var a = modelA.Predict(ref stateA, x, in context);
                var b = modelB.Predict(ref stateB, x, in context);
                Assert.True(a == b,
                    $"Move {move}, col {x}: {a} != {b} — Bird condition presence affected colors");
            }
        }
    }

    [Fact]
    public void Factory_TwoRngParams_GivesDifferentInstancesToConditions()
    {
        // Verify the factory wires colorRng to DefaultCondition and dropRng to ObjectiveDropCondition
        // by checking that consuming dropRng doesn't shift colorRng's sequence.
        var colorRng = new Match3.Random.XorShift64(42);
        var dropRng = new Match3.Random.XorShift64(99);

        // Consume dropRng externally — should NOT affect colors
        for (int i = 0; i < 50; i++) dropRng.Next(0, 10000);

        var objectives = new LevelObjective[4]; // no Bird → only DefaultCondition
        var model = SpawnConditionFactory.Create(6, objectives, 25, colorRng, dropRng);

        // Compare with fresh colorRng (same seed, no dropRng interference)
        var freshColorRng = new Match3.Random.XorShift64(42);
        var refModel = SpawnConditionFactory.Create(6, objectives, 25, freshColorRng, new Match3.Random.XorShift64(1));

        var stateA = CreateState();
        var stateB = CreateState();
        var context = SpawnContext.Default;

        for (int x = 0; x < 8; x++)
        {
            var a = model.Predict(ref stateA, x, in context);
            var b = refModel.Predict(ref stateB, x, in context);
            Assert.True(a == b, $"Col {x}: dropRng pre-consumption affected colorRng");
        }
    }

    [Fact]
    public void Factory_SeedManager_UsesCorrectDomains()
    {
        var objectives = new[]
        {
            new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)ElementType.Bird,
                TargetCount = 8
            },
            new LevelObjective(),
            new LevelObjective(),
            new LevelObjective()
        };

        var seedManager = new Match3.Random.SeedManager(42);
        var model = SpawnConditionFactory.Create(6, objectives, 25, seedManager);

        // Should create without error and have 2 conditions
        Assert.Equal(2, model.ConditionCount);

        // Verify it works
        var state = CreateStateWithSink();
        var context = SpawnContext.Default;
        var result = model.Predict(ref state, 0, in context);
        Assert.NotEqual(ElementType.None, result);
    }

    #endregion

    #region Stub Condition for Tests

    private class StubCondition : ISpawnCondition
    {
        private readonly bool _conditionMet;
        private readonly ElementType _elementType;

        public int Priority { get; }

        public StubCondition(int priority, bool conditionMet, ElementType elementType)
        {
            Priority = priority;
            _conditionMet = conditionMet;
            _elementType = elementType;
        }

        public bool IsConditionMet(ref GameState state, int spawnX, in SpawnContext context)
            => _conditionMet;

        public ElementType Generate(ref GameState state, int spawnX, in SpawnContext context)
            => _elementType;
    }

    #endregion
}
