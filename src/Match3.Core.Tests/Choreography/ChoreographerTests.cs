using System.Numerics;
using Match3.Core.Choreography;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Xunit;

namespace Match3.Core.Tests.Choreography;

public class ChoreographerTests
{
    private readonly Choreographer _choreographer = new();

    [Fact]
    public void Choreograph_TileDestroy_GeneratesDestroyAndEffect()
    {
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 1,
                GridPosition = new Position(3, 4),
                Type = TileType.Red,
                Reason = DestroyReason.Match,
                SimulationTime = 0.5f
            }
        };

        var commands = _choreographer.Choreograph(events);

        Assert.Contains(commands, c => c is DestroyTileCommand);
        Assert.Contains(commands, c => c is ShowEffectCommand);
        Assert.Contains(commands, c => c is RemoveTileCommand);
    }

    [Fact]
    public void Choreograph_TileMove_GeneratesMoveCommand()
    {
        var events = new GameEvent[]
        {
            new TileMovedEvent
            {
                TileId = 1,
                FromPosition = new Vector2(3, 3),
                ToPosition = new Vector2(3, 4),
                Reason = MoveReason.Gravity,
                SimulationTime = 0.5f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var moveCmd = Assert.Single(commands.OfType<MoveTileCommand>());
        Assert.Equal(1, moveCmd.TileId);
        Assert.Equal(new Vector2(3, 3), moveCmd.From);
        Assert.Equal(new Vector2(3, 4), moveCmd.To);
        Assert.Equal(_choreographer.MoveDuration, moveCmd.Duration);
    }

    [Fact]
    public void Choreograph_TileSpawn_GeneratesSpawnAndMove()
    {
        var events = new GameEvent[]
        {
            new TileSpawnedEvent
            {
                TileId = 1,
                GridPosition = new Position(3, 0),
                Type = TileType.Red,
                Bomb = BombType.None,
                SpawnPosition = new Vector2(3, -1),
                SimulationTime = 0.5f
            }
        };

        var commands = _choreographer.Choreograph(events);

        Assert.Contains(commands, c => c is SpawnTileCommand);
        Assert.Contains(commands, c => c is MoveTileCommand);
    }

    [Fact]
    public void Choreograph_TilesSwapped_GeneratesSwapCommand()
    {
        var events = new GameEvent[]
        {
            new TilesSwappedEvent
            {
                TileAId = 1,
                TileBId = 2,
                PositionA = new Position(3, 4),
                PositionB = new Position(4, 4),
                IsRevert = false,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var swapCmd = Assert.Single(commands.OfType<SwapTilesCommand>());
        Assert.Equal(1, swapCmd.TileAId);
        Assert.Equal(2, swapCmd.TileBId);
        Assert.Equal(new Vector2(3, 4), swapCmd.PosA);
        Assert.Equal(new Vector2(4, 4), swapCmd.PosB);
    }

    [Fact]
    public void Choreograph_MatchDetected_GeneratesHighlight()
    {
        var events = new GameEvent[]
        {
            new MatchDetectedEvent
            {
                Type = TileType.Red,
                Positions = new[] { new Position(0, 0), new Position(1, 0), new Position(2, 0) },
                TileCount = 3,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var highlightCmd = Assert.Single(commands.OfType<ShowMatchHighlightCommand>());
        Assert.Equal(3, highlightCmd.Positions.Length);
    }

    [Fact]
    public void Choreograph_BombCreated_GeneratesUpdateAndEffect()
    {
        var events = new GameEvent[]
        {
            new BombCreatedEvent
            {
                TileId = 1,
                Position = new Position(3, 4),
                BombType = BombType.Horizontal,
                BaseType = TileType.Red,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        Assert.Contains(commands, c => c is UpdateTileBombCommand);
        Assert.Contains(commands, c => c is ShowEffectCommand { EffectType: "bomb_created" });
    }

    [Fact]
    public void Choreograph_ProjectileLaunched_GeneratesSpawnCommand()
    {
        var events = new GameEvent[]
        {
            new ProjectileLaunchedEvent
            {
                ProjectileId = 100,
                Type = ProjectileType.Ufo,
                Origin = new Vector2(3, 4),
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var spawnCmd = Assert.Single(commands.OfType<SpawnProjectileCommand>());
        Assert.Equal(100, spawnCmd.ProjectileId);
        Assert.Equal(new Vector2(3, 4), spawnCmd.Origin);
    }

    [Fact]
    public void Choreograph_ProjectileImpact_GeneratesImpactAndRemove()
    {
        var events = new GameEvent[]
        {
            new ProjectileImpactEvent
            {
                ProjectileId = 100,
                ImpactPosition = new Position(5, 6),
                SimulationTime = 1f
            }
        };

        var commands = _choreographer.Choreograph(events);

        Assert.Contains(commands, c => c is ImpactProjectileCommand { ProjectileId: 100 });
        Assert.Contains(commands, c => c is RemoveProjectileCommand { ProjectileId: 100 });
    }

    [Fact]
    public void Choreograph_WithBaseTime_OffsetsAllCommands()
    {
        const float baseTime = 2.0f;
        var events = new GameEvent[]
        {
            new TileMovedEvent
            {
                TileId = 1,
                FromPosition = new Vector2(3, 3),
                ToPosition = new Vector2(3, 4),
                SimulationTime = 0.5f
            }
        };

        var commands = _choreographer.Choreograph(events, baseTime);

        var moveCmd = Assert.Single(commands.OfType<MoveTileCommand>());
        Assert.True(moveCmd.StartTime >= baseTime);
    }

    [Fact]
    public void Choreograph_EmptyEvents_ReturnsEmptyList()
    {
        var commands = _choreographer.Choreograph(Array.Empty<GameEvent>());

        Assert.Empty(commands);
    }
}
