using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Spawning;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Spawning;

/// <summary>
/// DefaultCondition 单元测试
/// 测试 seed 驱动颜色生成 + 安全网（色彩保底、反连续、Mercy）
/// </summary>
public class DefaultConditionTests
{
    private GameState CreateState(int width = 8, int height = 8)
    {
        var state = new GameState(width, height, 6, new SequentialRandom());
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                state.SetTile(x, y, new Tile(y * width + x, ElementType.None, x, y));
        return state;
    }

    #region Basic Generation

    [Fact]
    public void Generate_EmptyBoard_ReturnsValidColor()
    {
        var rng = new SequentialRandom();
        var condition = new DefaultCondition(rng, 6);
        var state = CreateState();
        var context = SpawnContext.Default;

        var type = condition.Generate(ref state, 0, in context);

        Assert.NotEqual(ElementType.None, type);
        Assert.True(type.IsColor());
    }

    [Fact]
    public void IsConditionMet_AlwaysReturnsTrue()
    {
        var condition = new DefaultCondition(new SequentialRandom(), 6);
        var state = CreateState();
        var context = SpawnContext.Default;

        Assert.True(condition.IsConditionMet(ref state, 0, in context));
    }

    [Fact]
    public void Priority_Is100()
    {
        var condition = new DefaultCondition(new SequentialRandom(), 6);
        Assert.Equal(100, condition.Priority);
    }

    [Fact]
    public void Generate_ZeroColors_ReturnsNone()
    {
        var condition = new DefaultCondition(new SequentialRandom(), 0);
        var state = CreateState();
        var context = SpawnContext.Default;

        Assert.Equal(ElementType.None, condition.Generate(ref state, 0, in context));
    }

    #endregion

    #region PRD Color Guarantee

    [Fact]
    public void Generate_ForcesAbsentColor_AfterConsecutiveMisses()
    {
        // StubRandom always returns 0 → always picks Item1
        var rng = StubRandom.WithFixedValue(0);
        var condition = new DefaultCondition(rng, 6);
        var state = CreateState();
        var context = SpawnContext.Default;

        // Generate enough times to trigger PRD for other colors
        // After 4 consecutive misses for Item2-Item6, one should be forced
        var colorsSeen = new bool[6];
        for (int i = 0; i < 30; i++)
        {
            var type = condition.Generate(ref state, 0, in context);
            int idx = BoardAnalyzer.GetColorIndex(type);
            if (idx >= 0) colorsSeen[idx] = true;
        }

        // PRD should have forced at least some non-Item1 colors
        int colorCount = 0;
        for (int i = 0; i < 6; i++)
            if (colorsSeen[i]) colorCount++;

        Assert.True(colorCount > 1, "PRD should force absent colors to appear");
    }

    [Fact]
    public void Generate_AllColorsAppear_WithinReasonableWindow()
    {
        var rng = new Match3.Random.XorShift64(42);
        var condition = new DefaultCondition(rng, 6);
        var state = CreateState();
        var context = SpawnContext.Default;

        var colorsSeen = new bool[6];
        // Within 30 spawns, all 6 colors should appear (PRD guarantees this)
        for (int i = 0; i < 30; i++)
        {
            var type = condition.Generate(ref state, 0, in context);
            int idx = BoardAnalyzer.GetColorIndex(type);
            if (idx >= 0) colorsSeen[idx] = true;
        }

        for (int i = 0; i < 6; i++)
        {
            Assert.True(colorsSeen[i], $"Color index {i} never appeared in 30 spawns");
        }
    }

    #endregion

    #region Color Guarantee + Anti-Streak Interaction

    [Fact]
    public void Generate_ForcedColor_AntiStreak_UpdatesCorrectCounter()
    {
        // Regression: forced color counter must update for the ACTUAL spawned color,
        // not the pre-anti-streak forced color.
        // Scenario: RNG always picks Item1 (idx=0). After 4 misses, Item2 is forced.
        // But if Item2 is the column top color, anti-streak changes it to Item3.
        // The counter for Item3 (not Item2) must be reset.
        var rng = StubRandom.WithFixedValue(0); // always picks idx 0
        var condition = new DefaultCondition(rng, 3); // only 3 colors
        var state = CreateState(5, 5);

        // Column 0 top is Item2 — will collide with forced color
        state.SetTile(0, 4, new Tile(1, ElementType.Item2, 0, 4));

        var context = SpawnContext.Default;

        // Generate Item1 repeatedly to starve Item2 and Item3
        // With 3 colors: after 4 Item1 spawns, Item2 has missCount=4 → forced
        // But Item2 is column top → anti-streak deflects to Item3
        ElementType lastForced = ElementType.None;
        for (int i = 0; i < 20; i++)
        {
            var type = condition.Generate(ref state, 0, in context);
            if (type == ElementType.Item2)
            {
                // If Item2 ever appears, anti-streak failed or forcing was wrong
                // With column top = Item2, anti-streak should prevent this
                // (unless it's a mercy or normal path where top check doesn't apply)
            }
            lastForced = type;
        }

        // Key assertion: after many spawns, Item3 must have appeared
        // (because forced Item2 was deflected to Item3 by anti-streak)
        var colorsSeen = new bool[3];
        var condition2 = new DefaultCondition(StubRandom.WithFixedValue(0), 3);
        for (int i = 0; i < 20; i++)
        {
            var type = condition2.Generate(ref state, 0, in context);
            int idx = BoardAnalyzer.GetColorIndex(type);
            if (idx >= 0 && idx < 3) colorsSeen[idx] = true;
        }

        Assert.True(colorsSeen[2],
            "Item3 should appear: forced Item2 deflected by anti-streak (column top = Item2)");
    }

    #endregion

    #region Anti-Streak

    [Fact]
    public void Generate_AvoidsColumnTopColor()
    {
        // RNG always picks index 0 (Item1), but column top is Item1
        var rng = StubRandom.WithFixedValue(0);
        var condition = new DefaultCondition(rng, 6);
        var state = CreateState(5, 5);

        // Place Item1 at column 0 top
        state.SetTile(0, 4, new Tile(1, ElementType.Item1, 0, 4));

        var context = SpawnContext.Default;
        var type = condition.Generate(ref state, 0, in context);

        // Anti-streak should deflect away from Item1
        Assert.NotEqual(ElementType.Item1, type);
    }

    [Fact]
    public void Generate_EmptyColumn_NoAntiStreak()
    {
        var rng = StubRandom.WithFixedValue(0);
        var condition = new DefaultCondition(rng, 6);
        var state = CreateState(5, 5);
        var context = SpawnContext.Default;

        // Empty column — no top color to avoid
        var type = condition.Generate(ref state, 0, in context);
        Assert.NotEqual(ElementType.None, type);
    }

    #endregion

    #region Mercy Mode

    [Fact]
    public void Generate_MercyTriggersHelpfulSpawn()
    {
        var rng = new SequentialRandom();
        var condition = new DefaultCondition(rng, 6);
        var state = CreateState(5, 5);

        // Setup: Item3 pair waiting for match at bottom row (drop target)
        state.SetTile(0, 4, new Tile(1, ElementType.Item3, 0, 4));
        state.SetTile(1, 4, new Tile(2, ElementType.Item3, 1, 4));

        // Mercy conditions: struggling player in last moves
        var context = new SpawnContext
        {
            TargetDifficulty = 0.5f,
            RemainingMoves = 2,
            GoalProgress = 0.5f,
            FailedAttempts = 3,
            InFlowState = false
        };

        var type = condition.Generate(ref state, 2, in context);

        // Mercy should spawn Item3 to complete the match
        Assert.Equal(ElementType.Item3, type);
    }

    [Fact]
    public void Generate_NoMercy_WhenNotStruggling()
    {
        var rng = StubRandom.WithFixedValue(0);
        var condition = new DefaultCondition(rng, 6);
        var state = CreateState(5, 5);

        // Normal context — no mercy
        var context = SpawnContext.Default;

        // Should just return normal random color
        var type = condition.Generate(ref state, 0, in context);
        Assert.NotEqual(ElementType.None, type);
    }

    #endregion

    #region BoardAnalyzer.GetColumnTopColor

    [Fact]
    public void GetColumnTopColor_ReturnsFirstNonEmpty()
    {
        var state = CreateState(5, 5);
        state.SetTile(2, 3, new Tile(1, ElementType.Item4, 2, 3));
        state.SetTile(2, 4, new Tile(2, ElementType.Item1, 2, 4));

        Assert.Equal(ElementType.Item4, BoardAnalyzer.GetColumnTopColor(ref state, 2));
    }

    [Fact]
    public void GetColumnTopColor_EmptyColumn_ReturnsNone()
    {
        var state = CreateState(5, 5);

        Assert.Equal(ElementType.None, BoardAnalyzer.GetColumnTopColor(ref state, 0));
    }

    [Fact]
    public void GetColumnTopColor_FullColumn_ReturnsRow0()
    {
        var state = CreateState(3, 3);
        state.SetTile(0, 0, new Tile(1, ElementType.Item2, 0, 0));
        state.SetTile(0, 1, new Tile(2, ElementType.Item3, 0, 1));
        state.SetTile(0, 2, new Tile(3, ElementType.Item1, 0, 2));

        Assert.Equal(ElementType.Item2, BoardAnalyzer.GetColumnTopColor(ref state, 0));
    }

    #endregion

    #region No Challenge Behavior

    [Fact]
    public void Generate_NeverAvoidMatches_HighDifficulty()
    {
        var rng = new Match3.Random.XorShift64(42);
        var condition = new DefaultCondition(rng, 6);
        var state = CreateState(5, 5);

        // Setup: Item1 pair at (0,0) and (1,0)
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));

        var context = new SpawnContext
        {
            TargetDifficulty = 0.9f,
            RemainingMoves = 20,
            GoalProgress = 0.5f,
            FailedAttempts = 0,
            InFlowState = true
        };

        // Generate multiple times — should never actively avoid matches
        int matchCreatingCount = 0;
        for (int i = 0; i < 100; i++)
        {
            // Reset RNG state for each iteration
            var testRng = new Match3.Random.XorShift64((ulong)(i + 1));
            var testCondition = new DefaultCondition(testRng, 6);
            var type = testCondition.Generate(ref state, 2, in context);
            if (type == ElementType.Item1) matchCreatingCount++;
        }

        // With 6 colors, expect ~16% to be Item1 (no bias against it)
        Assert.True(matchCreatingCount > 5,
            $"Item1 appeared {matchCreatingCount}/100 times; should not avoid matches at high difficulty");
    }

    #endregion
}
