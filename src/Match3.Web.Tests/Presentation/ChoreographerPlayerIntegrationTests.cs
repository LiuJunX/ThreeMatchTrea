using System.Numerics;
using Match3.Core.Choreography;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Presentation;
using Xunit;

namespace Match3.Web.Tests.Presentation;

/// <summary>
/// Integration tests for the full flow: GameEvent → Choreographer → RenderCommand → Player → VisualState
/// </summary>
public class ChoreographerPlayerIntegrationTests
{
    private readonly Choreographer _choreographer = new();
    private readonly Player _player = new();

    #region Single Event Flow

    [Fact]
    public void TileMove_FullFlow_UpdatesVisualState()
    {
        // Arrange: Add tile to visual state
        _player.VisualState.AddTile(1, TileType.Red, BombType.None, new Position(3, 3), new Vector2(3, 3));

        // Act: Create event, choreograph, load, tick
        var events = new GameEvent[]
        {
            new TileMovedEvent
            {
                TileId = 1,
                FromPosition = new Vector2(3, 3),
                ToPosition = new Vector2(3, 5),
                Reason = MoveReason.Gravity,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);
        _player.Load(commands);
        _player.Tick(_choreographer.MoveDuration + 0.01f);

        // Assert
        var tile = _player.VisualState.GetTile(1);
        Assert.NotNull(tile);
        Assert.Equal(3f, tile.Position.X, 0.001f);
        Assert.Equal(5f, tile.Position.Y, 0.001f);
    }

    [Fact]
    public void TileDestroy_FullFlow_RemovesTileFromVisualState()
    {
        // Arrange
        _player.VisualState.AddTile(1, TileType.Red, BombType.None, new Position(3, 4), new Vector2(3, 4));

        // Act
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 1,
                GridPosition = new Position(3, 4),
                Type = TileType.Red,
                Reason = DestroyReason.Match,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);
        _player.Load(commands);
        _player.SkipToEnd();

        // Assert: Tile should be removed after destroy + remove commands
        Assert.Null(_player.VisualState.GetTile(1));
    }

    [Fact]
    public void TileSpawn_FullFlow_AddsTileToVisualState()
    {
        // Act
        var events = new GameEvent[]
        {
            new TileSpawnedEvent
            {
                TileId = 1,
                GridPosition = new Position(3, 0),
                Type = TileType.Blue,
                Bomb = BombType.None,
                SpawnPosition = new Vector2(3, -1),
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);
        _player.Load(commands);
        _player.SkipToEnd();

        // Assert: Tile is created at SpawnPosition
        // Note: Physical movement to GridPosition is handled by physics system, not Choreographer
        var tile = _player.VisualState.GetTile(1);
        Assert.NotNull(tile);
        Assert.Equal(TileType.Blue, tile.TileType);
        Assert.Equal(3f, tile.Position.X, 0.001f);
        Assert.Equal(-1f, tile.Position.Y, 0.001f);  // At SpawnPosition, physics controls falling
    }

    [Fact]
    public void TileSwap_FullFlow_SwapsTilePositions()
    {
        // Arrange
        _player.VisualState.AddTile(1, TileType.Red, BombType.None, new Position(3, 4), new Vector2(3, 4));
        _player.VisualState.AddTile(2, TileType.Blue, BombType.None, new Position(4, 4), new Vector2(4, 4));

        // Act
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
        _player.Load(commands);
        _player.SkipToEnd();

        // Assert
        var tileA = _player.VisualState.GetTile(1);
        var tileB = _player.VisualState.GetTile(2);
        Assert.NotNull(tileA);
        Assert.NotNull(tileB);
        Assert.Equal(4f, tileA.Position.X, 0.001f);  // A moved to B's position
        Assert.Equal(3f, tileB.Position.X, 0.001f);  // B moved to A's position
    }

    #endregion

    #region Cascade Scenarios

    [Fact]
    public void DestroyThenMove_FullFlow_ProperSequencing()
    {
        // Arrange: Tile at (3,3) will move to (3,4) after tile at (3,4) is destroyed
        _player.VisualState.AddTile(1, TileType.Red, BombType.None, new Position(3, 4), new Vector2(3, 4));
        _player.VisualState.AddTile(2, TileType.Blue, BombType.None, new Position(3, 3), new Vector2(3, 3));

        // Act
        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 1,
                GridPosition = new Position(3, 4),
                Type = TileType.Red,
                Reason = DestroyReason.Match,
                SimulationTime = 0f
            },
            new TileMovedEvent
            {
                TileId = 2,
                FromPosition = new Vector2(3, 3),
                ToPosition = new Vector2(3, 4),
                Reason = MoveReason.Gravity,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);
        _player.Load(commands);

        // Verify sequencing: destroy should complete before move starts
        var destroyCmd = commands.OfType<DestroyTileCommand>().First();
        var moveCmd = commands.OfType<MoveTileCommand>().First();
        Assert.True(moveCmd.StartTime >= destroyCmd.StartTime + destroyCmd.Duration);

        _player.SkipToEnd();

        // Assert: Tile 1 removed, Tile 2 at new position
        Assert.Null(_player.VisualState.GetTile(1));
        var tile2 = _player.VisualState.GetTile(2);
        Assert.NotNull(tile2);
        Assert.Equal(4f, tile2.Position.Y, 0.001f);
    }

    [Fact]
    public void MultipleDestroysAndMoves_FullFlow_AllTilesEndUpCorrectly()
    {
        // Arrange: 3 tiles in column 3
        _player.VisualState.AddTile(1, TileType.Red, BombType.None, new Position(3, 5), new Vector2(3, 5));  // Will be destroyed
        _player.VisualState.AddTile(2, TileType.Blue, BombType.None, new Position(3, 4), new Vector2(3, 4)); // Will move to 5
        _player.VisualState.AddTile(3, TileType.Green, BombType.None, new Position(3, 3), new Vector2(3, 3)); // Will move to 4

        var events = new GameEvent[]
        {
            new TileDestroyedEvent
            {
                TileId = 1,
                GridPosition = new Position(3, 5),
                Type = TileType.Red,
                Reason = DestroyReason.Match,
                SimulationTime = 0f
            },
            new TileMovedEvent
            {
                TileId = 2,
                FromPosition = new Vector2(3, 4),
                ToPosition = new Vector2(3, 5),
                Reason = MoveReason.Gravity,
                SimulationTime = 0.1f
            },
            new TileMovedEvent
            {
                TileId = 3,
                FromPosition = new Vector2(3, 3),
                ToPosition = new Vector2(3, 4),
                Reason = MoveReason.Gravity,
                SimulationTime = 0.1f
            }
        };

        var commands = _choreographer.Choreograph(events);
        _player.Load(commands);
        _player.SkipToEnd();

        // Assert
        Assert.Null(_player.VisualState.GetTile(1));
        var tile2 = _player.VisualState.GetTile(2);
        var tile3 = _player.VisualState.GetTile(3);
        Assert.NotNull(tile2);
        Assert.NotNull(tile3);
        Assert.Equal(5f, tile2.Position.Y, 0.001f);
        Assert.Equal(4f, tile3.Position.Y, 0.001f);
    }

    [Fact]
    public void DestroySpawnAndMove_FullFlow_RefillScenario()
    {
        // Arrange: Simulate a typical match-destroy-refill cycle
        _player.VisualState.AddTile(1, TileType.Red, BombType.None, new Position(3, 4), new Vector2(3, 4));

        var events = new GameEvent[]
        {
            // Destroy existing tile
            new TileDestroyedEvent
            {
                TileId = 1,
                GridPosition = new Position(3, 4),
                Type = TileType.Red,
                Reason = DestroyReason.Match,
                SimulationTime = 0f
            },
            // Spawn new tile from above
            new TileSpawnedEvent
            {
                TileId = 2,
                GridPosition = new Position(3, 4),
                Type = TileType.Blue,
                Bomb = BombType.None,
                SpawnPosition = new Vector2(3, -1),
                SimulationTime = 0.3f
            }
        };

        var commands = _choreographer.Choreograph(events);
        _player.Load(commands);
        _player.SkipToEnd();

        // Assert
        Assert.Null(_player.VisualState.GetTile(1));
        var tile2 = _player.VisualState.GetTile(2);
        Assert.NotNull(tile2);
        Assert.Equal(TileType.Blue, tile2.TileType);
        // Tile spawns at SpawnPosition; physics system controls falling to GridPosition
        Assert.Equal(-1f, tile2.Position.Y, 0.001f);
    }

    #endregion

    #region Bomb Scenarios

    [Fact]
    public void BombCreation_FullFlow_UpdatesTileBombType()
    {
        // Arrange
        _player.VisualState.AddTile(1, TileType.Red, BombType.None, new Position(3, 4), new Vector2(3, 4));

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
        _player.Load(commands);
        _player.SkipToEnd();

        // Assert
        var tile = _player.VisualState.GetTile(1);
        Assert.NotNull(tile);
        Assert.Equal(BombType.Horizontal, tile.BombType);
    }

    [Fact]
    public void BombActivation_FullFlow_AddsExplosionEffect()
    {
        var events = new GameEvent[]
        {
            new BombActivatedEvent
            {
                TileId = 1,
                Position = new Position(3, 4),
                BombType = BombType.Horizontal,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);
        _player.Load(commands);
        _player.Tick(0.01f);

        // Assert: Should have explosion effect
        Assert.Contains(_player.VisualState.Effects, e => e.EffectType == "bomb_explosion");
    }

    #endregion

    #region Projectile Scenarios

    [Fact]
    public void ProjectileFullLifecycle_FullFlow_SpawnMoveImpactRemove()
    {
        var events = new GameEvent[]
        {
            new ProjectileLaunchedEvent
            {
                ProjectileId = 100,
                Type = ProjectileType.Ufo,
                Origin = new Vector2(3, 4),
                SimulationTime = 0f
            },
            new ProjectileMovedEvent
            {
                ProjectileId = 100,
                FromPosition = new Vector2(3, 4),
                ToPosition = new Vector2(6, 7),
                Velocity = new Vector2(3, 3),  // Speed ~4.24
                SimulationTime = 0.3f
            },
            new ProjectileImpactEvent
            {
                ProjectileId = 100,
                ImpactPosition = new Position(6, 7),
                SimulationTime = 1.5f
            }
        };

        var commands = _choreographer.Choreograph(events);
        _player.Load(commands);

        // After launch
        _player.Tick(0.1f);
        Assert.NotNull(_player.VisualState.GetProjectile(100));

        // Skip to end - projectile should be removed
        _player.SkipToEnd();
        Assert.Null(_player.VisualState.GetProjectile(100));
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public void MatchAndCascade_FullFlow_CompleteScenario()
    {
        // Arrange: 3x3 area with a horizontal match in middle row
        // Row 2: tiles 1,2,3 (will be matched)
        // Row 1: tiles 4,5,6 (will fall to row 2)
        // Row 0: will spawn new tiles
        for (int x = 0; x < 3; x++)
        {
            _player.VisualState.AddTile(x + 1, TileType.Red, BombType.None,
                new Position(x, 2), new Vector2(x, 2));
            _player.VisualState.AddTile(x + 4, TileType.Blue, BombType.None,
                new Position(x, 1), new Vector2(x, 1));
        }

        var events = new List<GameEvent>();

        // Match detected
        events.Add(new MatchDetectedEvent
        {
            Type = TileType.Red,
            Positions = new[] { new Position(0, 2), new Position(1, 2), new Position(2, 2) },
            TileCount = 3,
            SimulationTime = 0f
        });

        // Destroy matched tiles
        for (int x = 0; x < 3; x++)
        {
            events.Add(new TileDestroyedEvent
            {
                TileId = x + 1,
                GridPosition = new Position(x, 2),
                Type = TileType.Red,
                Reason = DestroyReason.Match,
                SimulationTime = 0.05f
            });
        }

        // Move tiles down
        for (int x = 0; x < 3; x++)
        {
            events.Add(new TileMovedEvent
            {
                TileId = x + 4,
                FromPosition = new Vector2(x, 1),
                ToPosition = new Vector2(x, 2),
                Reason = MoveReason.Gravity,
                SimulationTime = 0.3f
            });
        }

        // Spawn new tiles
        for (int x = 0; x < 3; x++)
        {
            events.Add(new TileSpawnedEvent
            {
                TileId = x + 7,
                GridPosition = new Position(x, 1),
                Type = TileType.Green,
                Bomb = BombType.None,
                SpawnPosition = new Vector2(x, -1),
                SimulationTime = 0.5f
            });
        }

        var commands = _choreographer.Choreograph(events);
        _player.Load(commands);
        _player.SkipToEnd();

        // Assert: Original tiles 1,2,3 removed
        for (int i = 1; i <= 3; i++)
        {
            Assert.Null(_player.VisualState.GetTile(i));
        }

        // Tiles 4,5,6 at row 2
        for (int x = 0; x < 3; x++)
        {
            var tile = _player.VisualState.GetTile(x + 4);
            Assert.NotNull(tile);
            Assert.Equal(2f, tile.Position.Y, 0.001f);
        }

        // New tiles 7,8,9 spawned at SpawnPosition (physics controls falling)
        for (int x = 0; x < 3; x++)
        {
            var tile = _player.VisualState.GetTile(x + 7);
            Assert.NotNull(tile);
            Assert.Equal(TileType.Green, tile.TileType);
            Assert.Equal(-1f, tile.Position.Y, 0.001f);  // At SpawnPosition
        }
    }

    [Fact]
    public void AppendEventsWhilePlaying_FullFlow_ContinuesSeamlessly()
    {
        // Initial setup
        _player.VisualState.AddTile(1, TileType.Red, BombType.None, new Position(3, 3), new Vector2(3, 3));

        // First batch of events
        var events1 = new GameEvent[]
        {
            new TileMovedEvent
            {
                TileId = 1,
                FromPosition = new Vector2(3, 3),
                ToPosition = new Vector2(3, 4),
                Reason = MoveReason.Gravity,
                SimulationTime = 0f
            }
        };

        var commands1 = _choreographer.Choreograph(events1);
        _player.Load(commands1);

        // Play partway through
        _player.Tick(_choreographer.MoveDuration / 2);

        // Append more events
        _player.VisualState.AddTile(2, TileType.Blue, BombType.None, new Position(4, 3), new Vector2(4, 3));
        var events2 = new GameEvent[]
        {
            new TileMovedEvent
            {
                TileId = 2,
                FromPosition = new Vector2(4, 3),
                ToPosition = new Vector2(4, 5),
                Reason = MoveReason.Gravity,
                SimulationTime = _player.CurrentTime
            }
        };

        var commands2 = _choreographer.Choreograph(events2, _player.CurrentTime);
        _player.Append(commands2);

        // Complete all animations
        _player.SkipToEnd();

        // Assert both tiles reached their destinations
        var tile1 = _player.VisualState.GetTile(1);
        var tile2 = _player.VisualState.GetTile(2);
        Assert.NotNull(tile1);
        Assert.NotNull(tile2);
        Assert.Equal(4f, tile1.Position.Y, 0.001f);
        Assert.Equal(5f, tile2.Position.Y, 0.001f);
    }

    #endregion
}
