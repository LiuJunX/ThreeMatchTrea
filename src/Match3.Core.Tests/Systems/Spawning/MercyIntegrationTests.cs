using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.Spawning;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Spawning;

/// <summary>
/// Integration tests: FailedAttempts flows from RealtimeRefillSystem → SpawnContext → DefaultCondition Mercy path.
/// </summary>
public class MercyIntegrationTests
{
    /// <summary>
    /// Builds a 5x5 board where only Item3 creates a match at (2,0).
    /// Row 0: [Item3, Item3, None, Item4, Item5]
    /// Rows 1-4: alternating Item4/Item5 (no accidental matches).
    /// </summary>
    private static GameState CreateMercyBoard(int moveCount, int targetCount, int currentCount)
    {
        var state = new GameState(5, 5, 6, new SequentialRandom());
        state.MoveLimit = 10;
        state.MoveCount = moveCount;

        state.ObjectiveProgress[0] = new ObjectiveProgress
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item3,
            TargetCount = targetCount,
            CurrentCount = currentCount
        };

        // Row 0: pair of Item3, then varied non-matching colors
        state.SetTile(0, 0, new Tile(1, ElementType.Item3, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item3, 1, 0));
        state.SetTile(2, 0, new Tile(0, ElementType.None, 2, 0)); // spawn point
        state.SetTile(3, 0, new Tile(4, ElementType.Item4, 3, 0));
        state.SetTile(4, 0, new Tile(5, ElementType.Item5, 4, 0));

        // Rows 1-4: alternating filler
        for (int y = 1; y < 5; y++)
            for (int x = 0; x < 5; x++)
                state.SetTile(x, y, new Tile(y * 5 + x + 1,
                    (x + y) % 2 == 0 ? ElementType.Item4 : ElementType.Item5, x, y));

        return state;
    }

    [Fact]
    public void Update_MercyConditionsMet_SpawnsMercyColor()
    {
        // FailedAttempts=3, RemainingMoves=2, GoalProgress=0.2 → all Mercy conditions met
        var state = CreateMercyBoard(moveCount: 8, targetCount: 10, currentCount: 2);

        var condition = new DefaultCondition(new SequentialRandom(), 6);
        var spawnModel = new ConditionBasedSpawnModel(new ISpawnCondition[] { condition });
        var refill = new RealtimeRefillSystem(spawnModel);
        refill.FailedAttempts = 3;

        refill.Update(ref state);

        var spawned = state.GetTile(2, 0);
        Assert.NotEqual(ElementType.None, spawned.Type);
        Assert.Equal(ElementType.Item3, spawned.Type);
    }

    [Fact]
    public void Update_FailedAttemptsZero_NoMercy()
    {
        // Same board, FailedAttempts=0 → Mercy disabled
        var state = CreateMercyBoard(moveCount: 8, targetCount: 10, currentCount: 2);

        var condition = new DefaultCondition(StubRandom.WithFixedValue(0), 6);
        var spawnModel = new ConditionBasedSpawnModel(new ISpawnCondition[] { condition });
        var refill = new RealtimeRefillSystem(spawnModel);
        refill.FailedAttempts = 0;

        refill.Update(ref state);

        var spawned = state.GetTile(2, 0);
        Assert.NotEqual(ElementType.None, spawned.Type);
        // StubRandom(0) picks Item1 (index 0), anti-streak may deflect but not to Item3
        Assert.NotEqual(ElementType.Item3, spawned.Type);
    }

    [Fact]
    public void Update_FailedAttemptsHigh_ButGoalNearComplete_NoMercy()
    {
        // FailedAttempts=5 but GoalProgress=0.9 (not < 0.9) → Mercy disabled
        var state = CreateMercyBoard(moveCount: 8, targetCount: 10, currentCount: 9);

        var condition = new DefaultCondition(StubRandom.WithFixedValue(0), 6);
        var spawnModel = new ConditionBasedSpawnModel(new ISpawnCondition[] { condition });
        var refill = new RealtimeRefillSystem(spawnModel);
        refill.FailedAttempts = 5;

        refill.Update(ref state);

        var spawned = state.GetTile(2, 0);
        Assert.NotEqual(ElementType.None, spawned.Type);
        Assert.NotEqual(ElementType.Item3, spawned.Type);
    }
}
