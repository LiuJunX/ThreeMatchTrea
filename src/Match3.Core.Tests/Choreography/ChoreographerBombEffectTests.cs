using System.Linq;
using System.Numerics;
using Match3.Core.Choreography;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Xunit;

namespace Match3.Core.Tests.Choreography;

public class ChoreographerBombEffectTests
{
    private readonly Choreographer _choreographer = new();

    [Fact]
    public void BombActivated_EmitsFlashShockwaveExplosion()
    {
        var events = new GameEvent[]
        {
            new BombActivatedEvent
            {
                TileId = 1,
                Position = new Position(4, 4),
                BombType = BombType.Square5x5,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var effects = commands.OfType<ShowEffectCommand>().ToList();
        Assert.Contains(effects, e => e.EffectType == "bomb_flash");
        Assert.Contains(effects, e => e.EffectType == "bomb_shockwave");
        Assert.Contains(effects, e => e.EffectType == "bomb_explosion");

        // All start at the same time
        var startTimes = effects.Select(e => e.StartTime).Distinct().ToList();
        Assert.Single(startTimes);
    }

    [Fact]
    public void BombEffect_WaveDelay_CellLockDurationIncludesDelay()
    {
        // Wave 0 destroy at simTime=1.0, Wave 1 destroy at simTime=1.1
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 1,
                GridPosition = new Position(4, 4),
                Type = ElementType.Item1,
                Reason = DestroyReason.BombEffect,
                SimulationTime = 1.0f
            },
            new TileDestroyedEvent
            {
                TileId = 2,
                GridPosition = new Position(5, 4),
                Type = ElementType.Item2,
                Reason = DestroyReason.BombEffect,
                SimulationTime = 1.1f
            }
        };

        var commands = _choreographer.Choreograph(events);
        var locks = _choreographer.LockEntries;

        Assert.Equal(2, locks.Count);

        // Wave 0 lock: waveDelay=0 + BombDropDelay (BombEffect uses BombDropDelay)
        var lock0 = locks.First(l => l.Position.Equals(new Position(4, 4)));
        Assert.Equal(_choreographer.Config.BombDropDelay, lock0.Duration, 0.001f);

        // Wave 1 lock: waveDelay=0.1 + BombDropDelay
        var lock1 = locks.First(l => l.Position.Equals(new Position(5, 4)));
        Assert.Equal(0.1f + _choreographer.Config.BombDropDelay, lock1.Duration, 0.001f);
    }

    [Fact]
    public void BombEffect_ColumnDestroyEndTimes_TracksLatestWave()
    {
        // Two destroys in the same column at different wave times
        // A subsequent move should wait for the latest one
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 1,
                GridPosition = new Position(3, 4),
                Type = ElementType.Item1,
                Reason = DestroyReason.BombEffect,
                SimulationTime = 0f
            },
            new TileDestroyedEvent
            {
                TileId = 2,
                GridPosition = new Position(3, 5),
                Type = ElementType.Item2,
                Reason = DestroyReason.BombEffect,
                SimulationTime = 0.1f // Wave 1 (0.1s later)
            },
            new TileMovedEvent
            {
                TileId = 3,
                FromPosition = new Vector2(3, 2),
                ToPosition = new Vector2(3, 6),
                Reason = MoveReason.Gravity,
                SimulationTime = 0.2f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var destroyCmds = commands.OfType<DestroyTileCommand>().OrderBy(c => c.StartTime).ToList();
        var moveCmd = commands.OfType<MoveTileCommand>().First(c => c.From != c.To);

        // The later destroy (wave 1) ends later
        var latestDestroyEnd = destroyCmds.Max(c => c.StartTime + c.Duration);

        // Move must wait for latest destroy
        Assert.True(moveCmd.StartTime >= latestDestroyEnd,
            $"Move start {moveCmd.StartTime} should be >= latest destroy end {latestDestroyEnd}");
    }
}
