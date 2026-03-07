using System.Collections.Generic;
using System.Linq;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.PowerUps;

public class PowerUpHandlerExplosionTests
{
    private readonly StubEventCollector _events = new();

    [Fact]
    public void ActivateBomb_WithExplosionSystem_CreatesTargetedExplosion()
    {
        var explosion = new SpyExplosionSystem();
        var handler = CreateHandler(explosion);
        var state = CreateFilledState();

        var bombTile = new Tile(100, ElementType.Item1, 4, 4) { Bomb = BombType.Square5x5 };
        state.SetTile(4, 4, bombTile);

        handler.ActivateBomb(ref state, new Position(4, 4), 1, 1f, _events);

        // ExplosionSystem should have been called
        Assert.Single(explosion.CreatedExplosions);
        var (origin, targets) = explosion.CreatedExplosions[0];
        Assert.Equal(new Position(4, 4), origin);
        Assert.Contains(new Position(4, 4), targets); // origin included
    }

    [Fact]
    public void ActivateBomb_WithExplosionSystem_EmitsBombActivatedEvent()
    {
        var explosion = new SpyExplosionSystem();
        var handler = CreateHandler(explosion);
        var state = CreateFilledState();

        var bombTile = new Tile(100, ElementType.Item1, 3, 3) { Bomb = BombType.Horizontal };
        state.SetTile(3, 3, bombTile);

        handler.ActivateBomb(ref state, new Position(3, 3), 5, 2.5f, _events);

        var bombEvent = _events.EmittedEvents.OfType<BombActivatedEvent>().SingleOrDefault();
        Assert.NotNull(bombEvent);
        Assert.Equal(BombType.Horizontal, bombEvent.BombType);
        Assert.Equal(new Position(3, 3), bombEvent.Position);
        Assert.Equal(5, bombEvent.Tick);
        Assert.False(bombEvent.IsChainReaction);
    }

    [Fact]
    public void ActivateBomb_WithExplosionSystem_ClearsBombAttribute()
    {
        var explosion = new SpyExplosionSystem();
        var handler = CreateHandler(explosion);
        var state = CreateFilledState();

        var bombTile = new Tile(100, ElementType.Item1, 3, 3) { Bomb = BombType.Vertical };
        state.SetTile(3, 3, bombTile);

        handler.ActivateBomb(ref state, new Position(3, 3), 1, 1f, _events);

        // Bomb attribute should be cleared to prevent re-activation
        var tile = state.GetTile(3, 3);
        Assert.Equal(BombType.None, tile.Bomb);
    }

    [Fact]
    public void ActivateBomb_WithoutExplosionSystem_FallsBackToInstantClear()
    {
        // No explosion system (null) — backward compatible path
        var handler = CreateHandler(explosionSystem: null);
        var state = CreateFilledState();

        var bombTile = new Tile(100, ElementType.Item1, 3, 3) { Bomb = BombType.Horizontal };
        state.SetTile(3, 3, bombTile);

        handler.ActivateBomb(ref state, new Position(3, 3), 1, 1f, _events);

        // Entire row should be cleared immediately
        for (int x = 0; x < 8; x++)
        {
            Assert.Equal(ElementType.None, state.GetTile(x, 3).Type);
        }
    }

    [Fact]
    public void ActivateBomb_NoBomb_DoesNothing()
    {
        var explosion = new SpyExplosionSystem();
        var handler = CreateHandler(explosion);
        var state = CreateFilledState();

        // Normal tile, no bomb
        handler.ActivateBomb(ref state, new Position(3, 3), 1, 1f, _events);

        Assert.Empty(explosion.CreatedExplosions);
        Assert.Empty(_events.EmittedEvents);
    }

    [Fact]
    public void ActivateBomb_WithExplosionSystem_DoesNotClearTilesDirectly()
    {
        // When ExplosionSystem is used, tiles should NOT be cleared immediately
        // (ExplosionSystem handles wave-based destruction)
        var explosion = new SpyExplosionSystem();
        var handler = CreateHandler(explosion);
        var state = CreateFilledState();

        var bombTile = new Tile(100, ElementType.Item1, 4, 4) { Bomb = BombType.Square5x5 };
        state.SetTile(4, 4, bombTile);

        handler.ActivateBomb(ref state, new Position(4, 4), 1, 1f, _events);

        // Tiles in the blast radius should NOT be None yet (ExplosionSystem handles waves)
        // Only bomb attribute is cleared
        // Note: nearby tiles may be suspended by ExplosionSystem.CreateTargetedExplosion
        Assert.Equal(BombType.None, state.GetTile(4, 4).Bomb);
        // No TileDestroyedEvent should be emitted (that's ExplosionSystem's job)
        Assert.DoesNotContain(_events.EmittedEvents, e => e is TileDestroyedEvent);
    }

    #region Helpers

    private static PowerUpHandler CreateHandler(IExplosionSystem? explosionSystem)
    {
        return new PowerUpHandler(
            new StubScoreSystem(),
            new BombComboHandler(),
            BombEffectRegistry.CreateDefault(),
            new CoverSystem(),
            new GroundSystem(),
            explosionSystem);
    }

    private static GameState CreateFilledState(int width = 8, int height = 8)
    {
        var state = new GameState(width, height, 6, new StubRandom());
        var types = new[] { ElementType.Item1, ElementType.Item2, ElementType.Item3, ElementType.Item4 };
        int id = 1;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                state.SetTile(x, y, new Tile(id++, types[(x + y) % types.Length], x, y));
            }
        }
        return state;
    }

    private class StubRandom : IRandom
    {
        public float NextFloat() => 0f;
        public int Next(int max) => 0;
        public int Next(int min, int max) => min;
        public void SetState(ulong state) { }
        public ulong GetState() => 0;
    }

    private class StubScoreSystem : IScoreSystem
    {
        public int CalculateMatchScore(Match3.Core.Models.Gameplay.MatchGroup match) => 10;
        public int CalculateSpecialMoveScore(ElementType t1, BombType b1, ElementType t2, BombType b2) => 100;
    }

    private class StubEventCollector : IEventCollector
    {
        public List<GameEvent> EmittedEvents { get; } = new();
        public bool IsEnabled => true;
        public void Emit(GameEvent evt) => EmittedEvents.Add(evt);
        public void EmitBatch(IEnumerable<GameEvent> events) => EmittedEvents.AddRange(events);
    }

    /// <summary>
    /// Spy that records CreateTargetedExplosion calls without suspending tiles.
    /// </summary>
    private class SpyExplosionSystem : IExplosionSystem
    {
        public List<(Position Origin, HashSet<Position> Targets)> CreatedExplosions { get; } = new();

        public bool HasActiveExplosions => false;

        public void CreateExplosion(ref GameState state, Position origin, int radius) { }

        public void CreateTargetedExplosion(ref GameState state, Position origin, IEnumerable<Position> targets)
        {
            CreatedExplosions.Add((origin, new HashSet<Position>(targets)));
        }

        public void CreateTargetedExplosion(ref GameState state, Position origin, IEnumerable<Position> targets, float waveInterval)
        {
            CreatedExplosions.Add((origin, new HashSet<Position>(targets)));
        }

        public void CreateTargetedExplosion(ref GameState state, Position origin, IEnumerable<Position> targets, float waveInterval, float acceleration)
        {
            CreatedExplosions.Add((origin, new HashSet<Position>(targets)));
        }

        public void Update(ref GameState state, float deltaTime, int tick, float simTime,
            IEventCollector eventCollector, List<Position> triggeredBombs) { }

        public void Reset() { }
    }

    #endregion
}
