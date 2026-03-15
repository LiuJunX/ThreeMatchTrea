using System.Collections.Generic;
using System.Linq;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.Tests.TestFixtures;
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

        var bombTile = new Tile(100, ElementType.Square5x5, 4, 4);
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

        var bombTile = new Tile(100, ElementType.HorizontalRocket, 3, 3);
        state.SetTile(3, 3, bombTile);

        handler.ActivateBomb(ref state, new Position(3, 3), 5, 2.5f, _events);

        var bombEvent = _events.Events.OfType<BombActivatedEvent>().SingleOrDefault();
        Assert.NotNull(bombEvent);
        Assert.Equal(ElementType.HorizontalRocket, bombEvent.BombType);
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

        var bombTile = new Tile(100, ElementType.VerticalRocket, 3, 3);
        state.SetTile(3, 3, bombTile);

        handler.ActivateBomb(ref state, new Position(3, 3), 1, 1f, _events);

        // Bomb attribute should be cleared to prevent re-activation
        var tile = state.GetTile(3, 3);
        Assert.Equal(ElementType.None, tile.Type);
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
        Assert.Empty(_events.Events);
    }

    [Fact]
    public void ActivateBomb_WithExplosionSystem_DoesNotClearTilesDirectly()
    {
        // When ExplosionSystem is used, tiles should NOT be cleared immediately
        // (ExplosionSystem handles wave-based destruction)
        var explosion = new SpyExplosionSystem();
        var handler = CreateHandler(explosion);
        var state = CreateFilledState();

        var bombTile = new Tile(100, ElementType.Square5x5, 4, 4);
        state.SetTile(4, 4, bombTile);

        handler.ActivateBomb(ref state, new Position(4, 4), 1, 1f, _events);

        // Tiles in the blast radius should NOT be None yet (ExplosionSystem handles waves)
        // Only bomb attribute is cleared
        // Note: nearby tiles may be suspended by ExplosionSystem.CreateTargetedExplosion
        Assert.Equal(ElementType.None, state.GetTile(4, 4).Type);
        // Only the bomb tile itself emits TileDestroyedEvent (ConsumeBomb) — blast radius tiles are ExplosionSystem's job
        var destroyEvents = _events.Events.OfType<TileDestroyedEvent>().ToList();
        Assert.Single(destroyEvents);
        Assert.Equal(ElimSource.ConsumeBomb, destroyEvents[0].Reason);
        Assert.Equal(new Position(4, 4), destroyEvents[0].GridPosition);
    }

    #region Helpers

    private static BombResolution CreateHandler(IExplosionSystem? explosionSystem)
    {
        return new BombResolution(
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

        public void CreateTargetedExplosion(ref GameState state, Position origin, IEnumerable<Position> targets, float waveInterval, float acceleration, float receiveLockDuration)
        {
            CreatedExplosions.Add((origin, new HashSet<Position>(targets)));
        }

        public int Update(ref GameState state, float deltaTime, int tick, float simTime,
            IEventCollector eventCollector, List<(Position Pos, Tile Tile)>? triggeredBombs = null) => 0;

        public void Reset() { }
    }

    #endregion
}
