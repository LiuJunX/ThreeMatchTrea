using System;
using System.Linq;
using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Objectives;
using Match3.Core.Systems.Obstacles;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Obstacles;

/// <summary>
/// Tests for PotionBottle obstacle: 4 colored sub-bottles (bitmask state),
/// each independently destroyed by matching color. Power-up/ColorBomb acts as wildcard.
/// </summary>
public class PotionBottleTests
{
    private const byte FullState = 0b00001111; // all 4 sub-bottles present

    private static GameState CreateState(int width = 5, int height = 5)
    {
        return new GameState(width, height, 5, new StubRandom());
    }

    private static ObstacleSystem CreateSystem(ILevelObjectiveSystem? objectiveSystem = null)
    {
        return new ObstacleSystem(objectiveSystem);
    }

    #region CanHit — direct hit rules

    [Fact]
    public void CanHit_Match_Blocked()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.PotionBottle, 4, FullState));
        var system = CreateSystem();

        var result = system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Match), 0, 0f, NullEventCollector.Instance);

        Assert.Equal(ObstacleHitResult.Blocked, result);
        Assert.True(state.HasObstacle(2, 2));
        Assert.Equal(4, state.GetObstacle(2, 2).Stage);
    }

    [Theory]
    [InlineData(ElimSource.Bomb)]
    [InlineData(ElimSource.Projectile)]
    [InlineData(ElimSource.ChainReaction)]
    [InlineData(ElimSource.ColorBomb)]
    [InlineData(ElimSource.SideItem)]
    [InlineData(ElimSource.ConsumeBomb)]
    public void CanHit_AllPowerUpSources_True(ElimSource source)
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.PotionBottle, 4, FullState));
        var system = CreateSystem();

        var result = system.TryHit(ref state, new Position(2, 2),
            new ElimContext(source), 0, 0f, NullEventCollector.Instance);

        Assert.NotEqual(ObstacleHitResult.Blocked, result);
    }

    #endregion

    #region CanReactAdjacent — color matching

    [Fact]
    public void CanReactAdjacent_MatchingColor_True()
    {
        var state = CreateState();
        // PotionBottle at (2,2), Red tile eliminated at (1,2)
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.PotionBottle, 4, FullState));
        var system = CreateSystem();
        var events = new BufferedEventCollector();

        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[1];
        eliminated[0] = new EliminatedTileInfo(
            new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match);

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, events);

        // Should react: Red sub-bottle broken, Stage 4→3
        Assert.Equal(3, state.GetObstacle(2, 2).Stage);
    }

    [Fact]
    public void CanReactAdjacent_WrongColor_False()
    {
        var state = CreateState();
        // Only Red sub-bottle present (bit 0)
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.PotionBottle, 1, 0b00000001));
        var system = CreateSystem();

        // Blue tile eliminated adjacent — no matching sub-bottle for Blue?
        // Actually bit 0 = Red, so Blue (bit 2) is not present → no reaction
        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[1];
        eliminated[0] = new EliminatedTileInfo(
            new Position(1, 2), new Tile(1, ElementType.Item3, 1, 2), ElimSource.Match);

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        Assert.Equal(1, state.GetObstacle(2, 2).Stage); // unchanged
    }

    [Fact]
    public void CanReactAdjacent_MissingSubBottle_False()
    {
        var state = CreateState();
        // Red sub-bottle already broken: state = 0b00001110 (Green+Blue+Yellow)
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.PotionBottle, 3, 0b00001110));
        var system = CreateSystem();

        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[1];
        eliminated[0] = new EliminatedTileInfo(
            new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match);

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        Assert.Equal(3, state.GetObstacle(2, 2).Stage); // unchanged
        Assert.Equal(0b00001110, state.GetObstacle(2, 2).State); // unchanged
    }

    [Fact]
    public void CanReactAdjacent_ColorBombWildcard_True()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.PotionBottle, 4, FullState));
        var system = CreateSystem();

        // ColorBomb tile consumed adjacent → triggerType = ColorBomb → wildcard
        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[1];
        eliminated[0] = new EliminatedTileInfo(
            new Position(1, 2), new Tile(1, ElementType.ColorBomb, 1, 2), ElimSource.ConsumeBomb);

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        Assert.Equal(3, state.GetObstacle(2, 2).Stage); // one sub-bottle broken
    }

    #endregion

    #region Damage mechanics — bitmask logic

    [Fact]
    public void RedMatch_BreaksRedSubBottle()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.PotionBottle, 4, FullState));
        var system = CreateSystem();
        var events = new BufferedEventCollector();

        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[1];
        eliminated[0] = new EliminatedTileInfo(
            new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match);

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, events);

        var obs = state.GetObstacle(2, 2);
        Assert.Equal(3, obs.Stage);
        Assert.Equal(0b00001110, obs.State); // bit 0 (Red) cleared
    }

    [Fact]
    public void BlueMatch_BreaksBlueSubBottle()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.PotionBottle, 4, FullState));
        var system = CreateSystem();

        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[1];
        eliminated[0] = new EliminatedTileInfo(
            new Position(1, 2), new Tile(1, ElementType.Item3, 1, 2), ElimSource.Match);

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        var obs = state.GetObstacle(2, 2);
        Assert.Equal(3, obs.Stage);
        Assert.Equal(0b00001011, obs.State); // bit 2 (Blue) cleared
    }

    [Fact]
    public void MultipleDifferentColors_SameBatch_OnlyFirstReacts()
    {
        // Dedup behavior: same batch, same obstacle position → only one damage
        // This is correct Royal Match behavior (one hit per obstacle per batch)
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.PotionBottle, 4, FullState));
        var system = CreateSystem();

        // Two different colored tiles eliminated adjacent to PotionBottle in same batch
        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[2];
        eliminated[0] = new EliminatedTileInfo(
            new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match); // Red
        eliminated[1] = new EliminatedTileInfo(
            new Position(3, 2), new Tile(2, ElementType.Item3, 3, 2), ElimSource.Match); // Blue

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        // Only one sub-bottle should be broken (first color processed)
        Assert.Equal(3, state.GetObstacle(2, 2).Stage);
    }

    [Fact]
    public void PowerUp_BreaksFirstSubBottle()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.PotionBottle, 4, FullState));
        var system = CreateSystem();

        // Bomb direct hit → ElementType.None → breaks lowest bit (Red)
        system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        var obs = state.GetObstacle(2, 2);
        Assert.Equal(3, obs.Stage);
        Assert.Equal(0b00001110, obs.State); // bit 0 (Red) cleared — lowest bit
    }

    [Fact]
    public void PowerUp_BreaksLowestRemainingBit()
    {
        var state = CreateState();
        // Red already broken: 0b00001110 (Green+Blue+Yellow)
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.PotionBottle, 3, 0b00001110));
        var system = CreateSystem();

        system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, NullEventCollector.Instance);

        var obs = state.GetObstacle(2, 2);
        Assert.Equal(2, obs.Stage);
        Assert.Equal(0b00001100, obs.State); // bit 1 (Green) cleared — now lowest
    }

    [Fact]
    public void SameColorMatch_TwiceNoEffect()
    {
        var state = CreateState();
        // Red already broken: 0b00001110
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.PotionBottle, 3, 0b00001110));
        var system = CreateSystem();

        // Red match adjacent — but Red sub-bottle already gone
        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[1];
        eliminated[0] = new EliminatedTileInfo(
            new Position(1, 2), new Tile(1, ElementType.Item1, 1, 2), ElimSource.Match);

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        Assert.Equal(3, state.GetObstacle(2, 2).Stage); // no change
        Assert.Equal(0b00001110, state.GetObstacle(2, 2).State); // no change
    }

    [Fact]
    public void ColorBomb_BreaksOneSubBottle()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.PotionBottle, 4, FullState));
        var system = CreateSystem();

        // ColorBomb tile consumed adjacent
        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[1];
        eliminated[0] = new EliminatedTileInfo(
            new Position(1, 2), new Tile(1, ElementType.ColorBomb, 1, 2), ElimSource.ConsumeBomb);

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        var obs = state.GetObstacle(2, 2);
        Assert.Equal(3, obs.Stage); // one sub-bottle broken
        // Lowest bit (Red) should be cleared
        Assert.Equal(0b00001110, obs.State);
    }

    #endregion

    #region Destruction — all sub-bottles broken

    [Fact]
    public void AllSubBottlesBroken_Destroyed()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.PotionBottle, 4, FullState));
        var system = CreateSystem();
        var events = new BufferedEventCollector();

        // Break all 4 sub-bottles via 4 different color matches
        var colors = new[] { ElementType.Item1, ElementType.Item2, ElementType.Item3, ElementType.Item4 };
        foreach (var color in colors)
        {
            if (!state.HasObstacle(2, 2)) break;

            Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[1];
            eliminated[0] = new EliminatedTileInfo(
                new Position(1, 2), new Tile(1, color, 1, 2), ElimSource.Match);

            system.NotifyBatchElimination(ref state, eliminated, 0, 0f, events);
        }

        Assert.False(state.HasObstacle(2, 2));
        Assert.Single(events.GetEvents().OfType<ObstacleDestroyedEvent>());
        Assert.Equal(3, events.GetEvents().OfType<ObstacleDamagedEvent>().Count());
    }

    [Fact]
    public void NoDeathEffect()
    {
        var state = CreateState();
        // Last sub-bottle: only Yellow remaining
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.PotionBottle, 1, 0b00001000));
        var system = CreateSystem();

        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[1];
        eliminated[0] = new EliminatedTileInfo(
            new Position(1, 2), new Tile(1, ElementType.Item4, 1, 2), ElimSource.Match);

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, NullEventCollector.Instance);

        Assert.False(state.HasObstacle(2, 2));
        // No ground, tile, or other side effects at the position
        Assert.False(state.GetGround(2, 2).HasGround);
    }

    #endregion

    #region Default stage / state

    [Fact]
    public void GetDefaultStage_Is4()
    {
        Assert.Equal(4, ObstacleRules.GetDefaultStage(ObstacleType.PotionBottle));
    }

    [Fact]
    public void GetDefaultState_Is0x0F()
    {
        Assert.Equal(0b00001111, ObstacleRules.GetDefaultState(ObstacleType.PotionBottle));
    }

    [Fact]
    public void GetDefaultState_OtherTypes_Zero()
    {
        Assert.Equal(0, ObstacleRules.GetDefaultState(ObstacleType.Box));
        Assert.Equal(0, ObstacleRules.GetDefaultState(ObstacleType.ColorBox));
    }

    [Fact]
    public void BoardInitializer_SyncsStageFromState()
    {
        // Custom 2-sub-bottle config: only Red + Blue (bits 0,2)
        var config = new LevelConfig(5, 5);
        int idx = 2 * 5 + 2; // (2, 2)
        config.Obstacles[idx] = ObstacleType.PotionBottle;
        config.ObstacleStates[idx] = 0b00000101; // Red + Blue only

        var state = CreateState();
        var initializer = new BoardInitializer(
            new StubTileGenerator(ElementType.Item1, ElementType.Item3, ElementType.Item2));
        initializer.Initialize(ref state, config);

        var obs = state.GetObstacle(2, 2);
        Assert.Equal(ObstacleType.PotionBottle, obs.Type);
        Assert.Equal(0b00000101, obs.State);
        Assert.Equal(2, obs.Stage); // popcount(0b101) = 2, NOT the default 4
    }

    #endregion

    #region Event verification

    [Fact]
    public void DamagedEvent_CarriesNewState()
    {
        var state = CreateState();
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.PotionBottle, 4, FullState));
        var system = CreateSystem();
        var events = new BufferedEventCollector();

        // Break Green sub-bottle (bit 1)
        Span<EliminatedTileInfo> eliminated = stackalloc EliminatedTileInfo[1];
        eliminated[0] = new EliminatedTileInfo(
            new Position(1, 2), new Tile(1, ElementType.Item2, 1, 2), ElimSource.Match);

        system.NotifyBatchElimination(ref state, eliminated, 0, 0f, events);

        var dmgEvt = events.GetEvents().OfType<ObstacleDamagedEvent>().Single();
        Assert.Equal(ObstacleType.PotionBottle, dmgEvt.Type);
        Assert.Equal(3, dmgEvt.RemainingStage);
        Assert.Equal(0b00001101, dmgEvt.NewState); // Green (bit 1) cleared
    }

    #endregion

    #region Objective tracking

    [Fact]
    public void Objective_IsGoal()
    {
        var state = CreateState();
        // Only 1 sub-bottle remaining → destroy on hit
        state.SetObstacle(2, 2, new Obstacle(ObstacleType.PotionBottle, 1, 0b00000001));

        state.ObjectiveProgress[0] = new ObjectiveProgress
        {
            TargetLayer = ObjectiveTargetLayer.Obstacle,
            ElementType = (int)ObstacleType.PotionBottle,
            TargetCount = 1,
            CurrentCount = 0
        };

        var objectiveSystem = new LevelObjectiveSystem();
        var system = CreateSystem(objectiveSystem);
        var events = new BufferedEventCollector();

        // Break last sub-bottle via power-up
        system.TryHit(ref state, new Position(2, 2),
            new ElimContext(ElimSource.Bomb), 0, 0f, events);

        Assert.False(state.HasObstacle(2, 2));
        var destroyEvt = events.GetEvents().OfType<ObstacleDestroyedEvent>().Single();
        Assert.True(destroyEvt.IsGoal);
    }

    #endregion
}
