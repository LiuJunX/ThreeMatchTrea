using System.Numerics;
using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Objectives;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.Tests.TestFixtures;
using Match3.Random;
using Xunit;
using Xunit.Abstractions;

namespace Match3.Core.Tests.Systems.Integration;

/// <summary>
/// Integration tests for Frost cover type.
///
/// Frost = Chain's match transparency (BlocksMatch=false)
///       + Honey's adjacent damage (DamagedByAdjacent=true).
/// 1 HP, static, blocks swap and movement.
/// </summary>
public class FrostIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public FrostIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Frost tile participates in match detection (BlocksMatch=false).
    /// R R [R] where [R] has Frost cover → match detected.
    /// </summary>
    [Fact]
    public void Frost_TileVisibleInMatch()
    {
        var rng = new StubRandom();
        var state = new GameState(3, 1, 6, rng);

        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        state.SetCover(new Position(2, 0), new Cover(CoverType.Frost, health: 1));

        var matchFinder = new ClassicMatchFinder(new BombGenerator());
        var matches = matchFinder.FindMatchGroups(in state);

        Assert.Single(matches);
        Assert.Equal(ElementType.Item1, matches[0].Type);
        Assert.Equal(3, matches[0].Positions.Count);
    }

    /// <summary>
    /// Direct match on Frost: cover absorbs hit, tile survives.
    /// R R [R] → match eliminates → Frost breaks, tile stays.
    /// </summary>
    [Fact]
    public void DirectMatch_Frost_CoverAbsorbs_TileSurvives()
    {
        var rng = new StubRandom();
        var state = new GameState(3, 1, 6, rng);

        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        state.SetCover(new Position(2, 0), new Cover(CoverType.Frost, health: 1));

        var objectiveSystem = new LevelObjectiveSystem();
        var coverSystem = new CoverSystem(objectiveSystem);
        var groundSystem = new GroundSystem(objectiveSystem);
        var bombRegistry = BombEffectRegistry.CreateDefault();
        var processor = new StandardMatchProcessor(new StubScoreSystem(), coverSystem, groundSystem, bombRegistry);

        var matchFinder = new ClassicMatchFinder(new BombGenerator());
        var matches = matchFinder.FindMatchGroups(in state);
        processor.ProcessMatches(ref state, matches);

        // Uncovered tiles eliminated
        Assert.Equal(ElementType.None, state.GetTile(0, 0).Type);
        Assert.Equal(ElementType.None, state.GetTile(1, 0).Type);

        // Frost tile: cover broken, tile survives
        Assert.Equal(CoverType.None, state.GetCover(new Position(2, 0)).Type);
        Assert.Equal(ElementType.Item1, state.GetTile(2, 0).Type);
    }

    /// <summary>
    /// Bomb hit on Frost: cover absorbs, tile survives.
    /// </summary>
    [Fact]
    public void DirectHit_Bomb_Frost_CoverAbsorbs()
    {
        var rng = new StubRandom();
        var state = new GameState(3, 1, 6, rng);

        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.HorizontalRocket, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item3, 2, 0));
        state.SetCover(new Position(2, 0), new Cover(CoverType.Frost, health: 1));

        var objectiveSystem = new LevelObjectiveSystem();
        var coverSystem = new CoverSystem(objectiveSystem);
        var groundSystem = new GroundSystem(objectiveSystem);
        var explosionSystem = new ExplosionSystem(coverSystem, groundSystem, objectiveSystem);
        var powerUpHandler = new BombResolution(new StubScoreSystem())
            .WithExplosionSystem(explosionSystem);
        var events = new StubEventCollector();

        powerUpHandler.ActivateBomb(ref state, new Position(1, 0), 1, 0f, events);
        for (int i = 0; i < 30 && explosionSystem.HasActiveExplosions; i++)
            explosionSystem.Update(ref state, 0.05f, 2 + i, 0.05f * i, events);

        // Cover destroyed, tile protected
        Assert.Equal(CoverType.None, state.GetCover(new Position(2, 0)).Type);
        Assert.Equal(ElementType.Item3, state.GetTile(2, 0).Type);
    }

    /// <summary>
    /// Adjacent match destroys Frost (DamagedByAdjacent=true).
    /// </summary>
    [Fact]
    public void AdjacentMatch_Frost_Destroyed()
    {
        var rng = new StubRandom();
        var state = new GameState(4, 1, 6, rng);

        // R R R [G] where [G] has Frost
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        state.SetTile(3, 0, new Tile(4, ElementType.Item2, 3, 0));
        state.SetCover(new Position(3, 0), new Cover(CoverType.Frost, health: 1));

        var objectiveSystem = new LevelObjectiveSystem();
        var coverSystem = new CoverSystem(objectiveSystem);
        var groundSystem = new GroundSystem(objectiveSystem);
        var bombRegistry = BombEffectRegistry.CreateDefault();
        var processor = new StandardMatchProcessor(new StubScoreSystem(), coverSystem, groundSystem, bombRegistry);

        var matchFinder = new ClassicMatchFinder(new BombGenerator());
        var matches = matchFinder.FindMatchGroups(in state);
        processor.ProcessMatches(ref state, matches);

        // Adjacent Frost destroyed
        Assert.Equal(CoverType.None, state.GetCover(new Position(3, 0)).Type);
        // Tile freed
        Assert.Equal(ElementType.Item2, state.GetTile(3, 0).Type);
    }

    /// <summary>
    /// Only Frost adjacent to the match breaks; distant Frost stays.
    /// </summary>
    [Fact]
    public void MultipleFrost_OnlyAdjacentBreaks()
    {
        var rng = new StubRandom();
        var state = new GameState(6, 1, 6, rng);

        // R R R G B [Y] where [Y] has Frost at pos 5 (not adjacent to match at 0-2)
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        state.SetTile(3, 0, new Tile(4, ElementType.Item2, 3, 0));
        state.SetCover(new Position(3, 0), new Cover(CoverType.Frost, health: 1));
        state.SetTile(4, 0, new Tile(5, ElementType.Item3, 4, 0));
        state.SetTile(5, 0, new Tile(6, ElementType.Item4, 5, 0));
        state.SetCover(new Position(5, 0), new Cover(CoverType.Frost, health: 1));

        var objectiveSystem = new LevelObjectiveSystem();
        var coverSystem = new CoverSystem(objectiveSystem);
        var groundSystem = new GroundSystem(objectiveSystem);
        var bombRegistry = BombEffectRegistry.CreateDefault();
        var processor = new StandardMatchProcessor(new StubScoreSystem(), coverSystem, groundSystem, bombRegistry);

        var matchFinder = new ClassicMatchFinder(new BombGenerator());
        var matches = matchFinder.FindMatchGroups(in state);
        processor.ProcessMatches(ref state, matches);

        // Frost at (3,0) is adjacent to match at (2,0) → destroyed
        Assert.Equal(CoverType.None, state.GetCover(new Position(3, 0)).Type);
        // Frost at (5,0) is NOT adjacent → intact
        Assert.Equal(CoverType.Frost, state.GetCover(new Position(5, 0)).Type);
    }

    /// <summary>
    /// Frost blocks swap: player cannot swap a frozen tile.
    /// </summary>
    [Fact]
    public void Frost_CannotSwap()
    {
        var rng = new StubRandom();
        var state = new GameState(2, 1, 6, rng);

        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item2, 1, 0));
        state.SetCover(new Position(0, 0), new Cover(CoverType.Frost, health: 1));

        Assert.False(state.CanInteract(0, 0));
    }

    /// <summary>
    /// Frost blocks falling: tile with Frost stays in place.
    /// </summary>
    [Fact]
    public void Frost_CannotFall()
    {
        var rng = new StubRandom();
        var state = new GameState(1, 2, 6, rng);

        state.SetTile(0, 0, new Tile(1, ElementType.None, 0, 0)); // empty above
        var tile = new Tile(2, ElementType.Item1, 0, 1);
        tile.Position = new Vector2(0, 1);
        state.SetTile(0, 1, tile);
        state.SetCover(new Position(0, 1), new Cover(CoverType.Frost, health: 1));

        var config = new Match3Config { GravitySpeed = 20.0f, MaxFallSpeed = 25.0f };
        var gravitySystem = new RealtimeGravitySystem(config, rng);

        for (int frame = 0; frame < 30; frame++)
            gravitySystem.Update(ref state, 1f / 60f);

        Assert.Equal(ElementType.Item1, state.GetTile(0, 1).Type);
        Assert.False(state.GetTile(0, 1).IsFalling);
    }

    /// <summary>
    /// After Frost is destroyed, tile can fall normally.
    /// </summary>
    [Fact]
    public void AdjacentMatch_Frost_TileFreed()
    {
        var rng = new StubRandom();
        var state = new GameState(1, 3, 6, rng);

        // Empty at (0,0), Frost at (0,1), Red at (0,2)
        state.SetTile(0, 0, new Tile(1, ElementType.None, 0, 0));
        var frozenTile = new Tile(2, ElementType.Item1, 0, 1);
        frozenTile.Position = new Vector2(0, 1);
        state.SetTile(0, 1, frozenTile);
        state.SetCover(new Position(0, 1), new Cover(CoverType.Frost, health: 1));
        state.SetTile(0, 2, new Tile(3, ElementType.Item2, 0, 2));

        // Verify: frozen tile cannot move
        Assert.False(state.CanMove(0, 1));

        // Destroy frost
        var coverSystem = new CoverSystem();
        var events = new BufferedEventCollector();
        coverSystem.TryDamageCover(ref state, new Position(0, 1), 1, 0.1f, events);

        // After destruction: tile can move
        Assert.Equal(CoverType.None, state.GetCover(new Position(0, 1)).Type);
        Assert.True(state.CanMove(0, 1));
    }

    /// <summary>
    /// Frost as objective: CoverDestroyedEvent has IsGoal=true when targeted.
    /// </summary>
    [Fact]
    public void Frost_Objective_IsGoal()
    {
        var rng = new StubRandom();
        var state = new GameState(3, 1, 6, rng);

        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        state.SetCover(new Position(2, 0), new Cover(CoverType.Frost, health: 1));

        var objectiveSystem = new LevelObjectiveSystem();
        var config = new LevelConfig();
        config.Objectives[0] = new LevelObjective
        {
            TargetLayer = ObjectiveTargetLayer.Cover,
            ElementType = (int)CoverType.Frost,
            TargetCount = 1
        };
        objectiveSystem.Initialize(ref state, config);

        var coverSystem = new CoverSystem(objectiveSystem);
        var events = new BufferedEventCollector();

        coverSystem.TryDamageCover(ref state, new Position(2, 0), 1, 0.1f, events);

        var evt = Assert.IsType<CoverDestroyedEvent>(events.GetEvents()[0]);
        Assert.True(evt.IsGoal);
    }
}
