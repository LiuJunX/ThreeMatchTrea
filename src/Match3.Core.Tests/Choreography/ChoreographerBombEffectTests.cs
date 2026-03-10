using System.Collections.Generic;
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
                BombType = ElementType.Square5x5,
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



    #region ColorBomb Session Events (multi-tick)

    [Fact]
    public void SessionStart_EmitsChargeUpCommands()
    {
        var events = new GameEvent[]
        {
            new ColorBombSessionStartEvent
            {
                TileId = 42,
                Position = new Position(3, 3),
                TargetColor = ElementType.Item1,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        // Scale pulse (1 → 1.2)
        var scales = commands.OfType<ScaleTileCommand>().Where(c => c.TileId == 42).ToList();
        Assert.Contains(scales, s => s.ToScale.X > 1f);

        // Hop upward (MoveTileCommand with Y < origin.Y)
        var moves = commands.OfType<MoveTileCommand>().Where(c => c.TileId == 42).ToList();
        Assert.Contains(moves, m => m.To.Y < m.From.Y);

        // Hold command (From == To) to keep tile alive
        Assert.Contains(moves, m => m.From == m.To);

        // Spin (RotateTileCommand)
        var rotate = commands.OfType<RotateTileCommand>().Single(c => c.TileId == 42);
        Assert.True(rotate.ToAngle > 360f);
        Assert.Equal(EasingType.InQuadratic, rotate.Easing);

        // Glow effect
        var effects = commands.OfType<ShowEffectCommand>().ToList();
        Assert.Contains(effects, e => e.EffectType == "bomb_flash");
    }

    [Fact]
    public void BeamLaunched_EmitsProjectileCommands()
    {
        // First register a session so the Choreographer tracks it
        var sessionStart = new ColorBombSessionStartEvent
        {
            TileId = 42,
            Position = new Position(3, 3),
            TargetColor = ElementType.Item1,
            SimulationTime = 0f
        };
        _choreographer.Choreograph(new GameEvent[] { sessionStart });

        // Now beam launch in next batch
        var events = new GameEvent[]
        {
            new ColorBombBeamLaunchedEvent
            {
                BombTileId = 42,
                Origin = new Vector2(3, 3),
                TargetPosition = new Position(7, 3),
                TargetTileId = 10,
                FlightDuration = 0.2f,
                BeamIndex = 0,
                SimulationTime = 0.25f
            }
        };

        var commands = _choreographer.Choreograph(events, baseTime: 0.25f);

        // Spawn projectile
        var spawn = commands.OfType<SpawnProjectileCommand>().Single();
        Assert.True(spawn.ProjectileId < 0); // negative beam ID
        Assert.Equal(ProjectileType.ColorBombBeam, spawn.Type);

        // Move projectile to target
        var move = commands.OfType<MoveProjectileCommand>().Single();
        Assert.Equal(7f, move.To.X, 0.01f);
        Assert.Equal(3f, move.To.Y, 0.01f);
        Assert.Equal(0.2f, move.Duration, 0.01f);

        // Impact at target
        var impact = commands.OfType<ImpactProjectileCommand>().Single();
        Assert.Equal(7f, impact.Position.X, 0.01f);

        // Hit effect
        var hitEffects = commands.OfType<ShowEffectCommand>()
            .Where(e => e.EffectType == "color_bomb_hit").ToList();
        Assert.Single(hitEffects);

        // Remove projectile after impact
        var remove = commands.OfType<RemoveProjectileCommand>().Single();
        Assert.True(remove.StartTime >= impact.StartTime);

        // Continuous shake on target tile from beam impact
        var shake = commands.OfType<ShakeTileCommand>().Single(c => c.TileId == 10);
        Assert.Equal(_choreographer.Config.ColorBombHitShakeAmplitude, shake.Amplitude, 0.01f);
        Assert.Equal(_choreographer.Config.ColorBombHitShakeFrequency, shake.Frequency, 0.01f);
        Assert.Equal(impact.StartTime, shake.StartTime, 0.01f);
        Assert.True(shake.Duration > 0);
    }

    [Fact]
    public void BeamLaunched_OriginUsesHoppedPosition()
    {
        // Register session
        _choreographer.Choreograph(new GameEvent[]
        {
            new ColorBombSessionStartEvent
            {
                TileId = 42,
                Position = new Position(3, 3),
                TargetColor = ElementType.Item1,
                SimulationTime = 0f
            }
        });

        // Beam launch
        var commands = _choreographer.Choreograph(new GameEvent[]
        {
            new ColorBombBeamLaunchedEvent
            {
                BombTileId = 42,
                Origin = new Vector2(3, 3), // raw origin
                TargetPosition = new Position(6, 3),
                TargetTileId = 10,
                FlightDuration = 0.15f,
                BeamIndex = 0,
                SimulationTime = 0.25f
            }
        }, baseTime: 0.25f);

        var move = commands.OfType<MoveProjectileCommand>().Single();
        // Beam should start from hopped position (3, 3 + HopOffset = 3, 2.7)
        float expectedY = 3f + _choreographer.Config.ColorBombHopOffset;
        Assert.Equal(expectedY, move.From.Y, 0.01f);
    }

    [Fact]
    public void BatchDestroy_OverridesBeamHitTimes_SynchronizedDestruction()
    {
        // Setup: session start → 2 beams at different times → batch destroy → tile destroyed events
        _choreographer.Choreograph(new GameEvent[]
        {
            new ColorBombSessionStartEvent
            {
                TileId = 42,
                Position = new Position(3, 3),
                TargetColor = ElementType.Item1,
                SimulationTime = 0f
            }
        });

        // Two beams with different flight durations
        _choreographer.Choreograph(new GameEvent[]
        {
            new ColorBombBeamLaunchedEvent
            {
                BombTileId = 42, Origin = new Vector2(3, 3),
                TargetPosition = new Position(4, 3), TargetTileId = 10,
                FlightDuration = 0.1f, BeamIndex = 0, SimulationTime = 0.25f
            },
            new ColorBombBeamLaunchedEvent
            {
                BombTileId = 42, Origin = new Vector2(3, 3),
                TargetPosition = new Position(7, 3), TargetTileId = 11,
                FlightDuration = 0.3f, BeamIndex = 1, SimulationTime = 0.37f
            }
        }, baseTime: 0.25f);

        // Batch destroy + tile destroyed events in same batch
        var destroyBatch = new GameEvent[]
        {
            new ColorBombBatchDestroyEvent
            {
                BombTileId = 42,
                BombPosition = new Position(3, 3),
                DestroyedPositions = new List<Position> { new(4, 3), new(7, 3) },
                DestroyedTileIds = new List<int> { 10, 11 },
                SimulationTime = 1.0f
            },
            new TileDestroyedEvent
            {
                TileId = 10, GridPosition = new Position(4, 3),
                Type = ElementType.Item1, Reason = DestroyReason.BombEffect,
                SimulationTime = 1.0f
            },
            new TileDestroyedEvent
            {
                TileId = 11, GridPosition = new Position(7, 3),
                Type = ElementType.Item1, Reason = DestroyReason.BombEffect,
                SimulationTime = 1.0f
            }
        };
        var commands = _choreographer.Choreograph(destroyBatch, baseTime: 1.0f);

        // Both tiles should be destroyed at the same visual time (synchronized)
        var destroys = commands.OfType<DestroyTileCommand>().ToList();
        Assert.Equal(2, destroys.Count);
        Assert.Equal(destroys[0].StartTime, destroys[1].StartTime, 0.001f);

        // Bomb should be shrunk and removed
        var bombShrink = commands.OfType<ScaleTileCommand>().SingleOrDefault(c => c.TileId == 42);
        Assert.NotNull(bombShrink);
        Assert.Equal(Vector2.Zero, bombShrink.ToScale);

        var bombRemove = commands.OfType<RemoveTileCommand>().SingleOrDefault(c => c.TileId == 42);
        Assert.NotNull(bombRemove);
        Assert.True(bombRemove.StartTime >= bombShrink.StartTime + bombShrink.Duration);
    }

    [Fact]
    public void BatchDestroy_NoSessionInfo_EmitsFallbackRemove()
    {
        // BatchDestroyEvent without prior SessionStartEvent (edge case: game restart)
        var events = new GameEvent[]
        {
            new ColorBombBatchDestroyEvent
            {
                BombTileId = 999,
                BombPosition = new Position(0, 0),
                DestroyedPositions = new List<Position>(),
                DestroyedTileIds = new List<int>(),
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        // Should emit fallback RemoveTileCommand
        var remove = commands.OfType<RemoveTileCommand>().SingleOrDefault(c => c.TileId == 999);
        Assert.NotNull(remove);
    }

    [Fact]
    public void TilesSwapped_ClearsSessionTracking()
    {
        // Register a session
        _choreographer.Choreograph(new GameEvent[]
        {
            new ColorBombSessionStartEvent
            {
                TileId = 42,
                Position = new Position(3, 3),
                TargetColor = ElementType.Item1,
                SimulationTime = 0f
            }
        });

        // Swap event (new move boundary) clears tracking
        _choreographer.Choreograph(new GameEvent[]
        {
            new TilesSwappedEvent
            {
                TileAId = 1, TileBId = 2,
                PositionA = new Position(0, 0), PositionB = new Position(1, 0),
                SimulationTime = 1.0f
            }
        }, baseTime: 1.0f);

        // BatchDestroy after swap should use fallback (session info cleared)
        var commands = _choreographer.Choreograph(new GameEvent[]
        {
            new ColorBombBatchDestroyEvent
            {
                BombTileId = 42,
                BombPosition = new Position(3, 3),
                DestroyedPositions = new List<Position>(),
                DestroyedTileIds = new List<int>(),
                SimulationTime = 2.0f
            }
        }, baseTime: 2.0f);

        // Fallback remove (no shrink animation since session info is gone)
        var remove = commands.OfType<RemoveTileCommand>().SingleOrDefault(c => c.TileId == 42);
        Assert.NotNull(remove);
        // No ScaleTileCommand since session info was cleared
        var scale = commands.OfType<ScaleTileCommand>().Where(c => c.TileId == 42).ToList();
        Assert.Empty(scale);
    }

    [Fact]
    public void BeamTarget_ShakeCoversBetweenHitAndDestroy()
    {
        // Single-tick: BombActivated + TileDestroyed in same batch
        // The beam target should get a ShakeTileCommand spanning the hitPause gap
        var events = new GameEvent[]
        {
            new BombActivatedEvent
            {
                TileId = 42,
                Position = new Position(3, 3),
                BombType = ElementType.ColorBomb,
                AffectedPositions = new List<Position> { new(3, 3), new(5, 3) },
                SimulationTime = 0f
            },
            new TileDestroyedEvent
            {
                TileId = 10,
                GridPosition = new Position(5, 3),
                Type = ElementType.Item1,
                Reason = DestroyReason.BombEffect,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        // Shake on the target tile
        var shake = commands.OfType<ShakeTileCommand>().Single(c => c.TileId == 10);
        Assert.Equal(_choreographer.Config.ColorBombHitShakeAmplitude, shake.Amplitude, 0.01f);
        Assert.Equal(_choreographer.Config.ColorBombHitShakeFrequency, shake.Frequency, 0.01f);
        Assert.True(shake.Duration > 0);

        // Destroy starts after shake ends
        var destroy = commands.OfType<DestroyTileCommand>().Single(c => c.TileId == 10);
        Assert.True(destroy.StartTime >= shake.StartTime + shake.Duration - 0.001f,
            $"Destroy start {destroy.StartTime} should be >= shake end {shake.StartTime + shake.Duration}");
    }

    #endregion
}
