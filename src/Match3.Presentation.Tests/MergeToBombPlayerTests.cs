using System.Numerics;
using Match3.Core.Choreography;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Presentation;
using Match3.Random;
using Xunit;

namespace Match3.Presentation.Tests;

/// <summary>
/// Integration tests for merge-to-bomb animation in Player + VisualState.
/// Verifies that hold commands prevent physics sync from moving tiles,
/// and that SuppressNewTileSync blocks premature tile additions.
/// </summary>
public class MergeToBombPlayerTests
{
    private readonly VisualState _visualState;
    private readonly Player _player;

    public MergeToBombPlayerTests()
    {
        _visualState = new VisualState();
        _player = new Player(_visualState);
    }

    #region Hold Command — IsBeingAnimated

    [Fact]
    public void HoldCommand_SetsIsBeingAnimated()
    {
        // Spawn tile, then hold it in place
        var commands = new RenderCommand[]
        {
            new SpawnTileCommand
            {
                TileId = 1, Type = TileType.Red, Bomb = BombType.None,
                GridPos = new Position(3, 1), SpawnPos = new Vector2(3, 1),
                StartTime = 0f, Duration = 0f
            },
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(3, 1), To = new Vector2(3, 1), // hold
                StartTime = 0f, Duration = 0.3f,
                Easing = EasingType.Linear
            }
        };

        _player.Load(commands);
        _player.Tick(0.016f);

        var tile = _visualState.GetTile(1);
        Assert.NotNull(tile);
        Assert.True(tile.IsBeingAnimated,
            "Hold command should set IsBeingAnimated=true");
    }

    [Fact]
    public void HoldCommand_PreventsSyncFromUpdatingPosition()
    {
        // Spawn tile at (3,1), hold it there
        var commands = new RenderCommand[]
        {
            new SpawnTileCommand
            {
                TileId = 1, Type = TileType.Red, Bomb = BombType.None,
                GridPos = new Position(3, 1), SpawnPos = new Vector2(3, 1),
                StartTime = 0f, Duration = 0f
            },
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(3, 1), To = new Vector2(3, 1),
                StartTime = 0f, Duration = 0.3f,
                Easing = EasingType.Linear
            }
        };

        _player.Load(commands);
        _player.Tick(0.016f);

        // Create a game state where tile 1 has moved to (3, 2) via physics
        var state = CreateGameState(8, 8);
        SetTile(ref state, 3, 2, new Tile(1, TileType.Red, 3, 2));

        _visualState.SyncFallingTilesFromGameState(in state);

        // Position should NOT be updated because IsBeingAnimated is true
        var tile = _visualState.GetTile(1);
        Assert.NotNull(tile);
        Assert.Equal(new Vector2(3, 1), tile.Position);
    }

    [Fact]
    public void HoldCommand_ReleasesAfterDuration()
    {
        var commands = new RenderCommand[]
        {
            new SpawnTileCommand
            {
                TileId = 1, Type = TileType.Red, Bomb = BombType.None,
                GridPos = new Position(3, 1), SpawnPos = new Vector2(3, 1),
                StartTime = 0f, Duration = 0f
            },
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(3, 1), To = new Vector2(3, 1),
                StartTime = 0f, Duration = 0.3f,
                Easing = EasingType.Linear
            }
        };

        _player.Load(commands);
        _player.Tick(0.35f); // past hold duration

        var tile = _visualState.GetTile(1);
        Assert.NotNull(tile);
        Assert.False(tile.IsBeingAnimated,
            "Hold command should release IsBeingAnimated after duration");
    }

    #endregion

    #region SuppressNewTileSync

    [Fact]
    public void SuppressNewTileSync_BlocksNewTileAddition()
    {
        var state = CreateGameState(8, 8);
        SetTile(ref state, 3, 2, new Tile(99, TileType.Blue, 3, 2));

        _visualState.SuppressNewTileSync = true;
        _visualState.SyncFallingTilesFromGameState(in state);

        // Tile 99 should NOT be in visual state
        Assert.Null(_visualState.GetTile(99));
    }

    [Fact]
    public void SuppressNewTileSync_False_AllowsNewTileAddition()
    {
        var state = CreateGameState(8, 8);
        SetTile(ref state, 3, 2, new Tile(99, TileType.Blue, 3, 2));

        _visualState.SuppressNewTileSync = false;
        _visualState.SyncFallingTilesFromGameState(in state);

        // Tile 99 should be added
        Assert.NotNull(_visualState.GetTile(99));
    }

    [Fact]
    public void SuppressNewTileSync_AlsoBlocksExistingTilePositionUpdates()
    {
        // Add tile to visual state first
        _visualState.AddTile(1, TileType.Red, BombType.None,
            new Position(3, 1), new Vector2(3, 1));

        // Game state has tile at different position (physics moved it)
        var state = CreateGameState(8, 8);
        var tile = new Tile(1, TileType.Red, new Vector2(3, 1.5f));
        SetTile(ref state, 3, 1, tile);

        _visualState.SuppressNewTileSync = true;
        _visualState.SyncFallingTilesFromGameState(in state);

        // Existing tile position should NOT be updated (suppressed during merge)
        var visual = _visualState.GetTile(1);
        Assert.NotNull(visual);
        Assert.Equal(new Vector2(3, 1), visual.Position);
    }

    [Fact]
    public void SuppressNewTileSync_False_UpdatesExistingTilePositions()
    {
        // Add tile to visual state first
        _visualState.AddTile(1, TileType.Red, BombType.None,
            new Position(3, 1), new Vector2(3, 1));

        // Game state has tile at different position
        var state = CreateGameState(8, 8);
        var tile = new Tile(1, TileType.Red, new Vector2(3, 1.5f));
        SetTile(ref state, 3, 1, tile);

        _visualState.SuppressNewTileSync = false;
        _visualState.SyncFallingTilesFromGameState(in state);

        // Position should be updated when not suppressed
        var visual = _visualState.GetTile(1);
        Assert.NotNull(visual);
        Assert.Equal(new Vector2(3, 1.5f), visual.Position);
    }

    [Fact]
    public void SuppressNewTileSync_SpawnTileCommandStillWorks()
    {
        // Even with suppress=true, Player's SpawnTileCommand should add the tile
        var commands = new RenderCommand[]
        {
            new SpawnTileCommand
            {
                TileId = 99, Type = TileType.Blue, Bomb = BombType.None,
                GridPos = new Position(3, 0), SpawnPos = new Vector2(3, -1),
                StartTime = 0.3f, Duration = 0f
            }
        };

        _player.Load(commands);
        _visualState.SuppressNewTileSync = true;

        // Before spawn time: tile not in visual state
        _player.Tick(0.1f);
        Assert.Null(_visualState.GetTile(99));

        // After spawn time: tile added by Player, not by sync
        _player.Tick(0.25f); // total 0.35f
        Assert.NotNull(_visualState.GetTile(99));
    }

    #endregion

    #region Full Merge Sequence

    [Fact]
    public void FullMergeSequence_GravityTilesStayDuringMerge()
    {
        float mergeDuration = 0.3f;

        // Setup: spawn tiles that will be involved
        // Tile 20 = bomb origin at (2,2)
        // Tile 31 = gravity tile at (3,1) that should fall to (3,2)
        var setupCommands = new RenderCommand[]
        {
            // Bomb origin tile
            new SpawnTileCommand
            {
                TileId = 20, Type = TileType.Red, Bomb = BombType.None,
                GridPos = new Position(2, 2), SpawnPos = new Vector2(2, 2),
                StartTime = 0f, Duration = 0f
            },
            // Gravity tile above destroyed position
            new SpawnTileCommand
            {
                TileId = 31, Type = TileType.Green, Bomb = BombType.None,
                GridPos = new Position(3, 1), SpawnPos = new Vector2(3, 1),
                StartTime = 0f, Duration = 0f
            }
        };
        _player.Load(setupCommands);
        _player.Tick(0.001f); // process spawns

        // Now append merge sequence commands
        float baseTime = _player.CurrentTime;
        var mergeCommands = new RenderCommand[]
        {
            // Hold bomb origin tile
            new MoveTileCommand
            {
                TileId = 20,
                From = new Vector2(2, 2), To = new Vector2(2, 2),
                StartTime = baseTime, Duration = mergeDuration,
                Easing = EasingType.Linear
            },
            // Hold gravity tile at current position during merge
            new MoveTileCommand
            {
                TileId = 31,
                From = new Vector2(3, 1), To = new Vector2(3, 1),
                StartTime = baseTime, Duration = mergeDuration,
                Easing = EasingType.Linear
            },
            // Gravity move after merge
            new MoveTileCommand
            {
                TileId = 31,
                From = new Vector2(3, 1), To = new Vector2(3, 2),
                StartTime = baseTime + mergeDuration, Duration = 0.15f,
                Easing = EasingType.OutCubic
            },
            // Remove old bomb tile, spawn new one
            new RemoveTileCommand
            {
                TileId = 20, StartTime = baseTime + mergeDuration,
                Duration = 0, Priority = 5
            },
            new SpawnTileCommand
            {
                TileId = 200, Type = TileType.Red, Bomb = BombType.Horizontal,
                GridPos = new Position(2, 2), SpawnPos = new Vector2(2, 2),
                StartTime = baseTime + mergeDuration, Duration = 0, Priority = 6
            }
        };
        _player.Append(mergeCommands);

        // --- During merge (T = 0.1s into merge) ---
        _player.Tick(0.1f);

        // Gravity tile should still be at original position
        var gravityTile = _visualState.GetTile(31);
        Assert.NotNull(gravityTile);
        Assert.Equal(new Vector2(3, 1), gravityTile.Position);
        Assert.True(gravityTile.IsBeingAnimated, "Gravity tile should be held during merge");

        // Bomb origin should still be at position
        var bombOrigin = _visualState.GetTile(20);
        Assert.NotNull(bombOrigin);
        Assert.True(bombOrigin.IsBeingAnimated, "Bomb origin should be held during merge");

        // New bomb tile should NOT exist yet
        Assert.Null(_visualState.GetTile(200));

        // --- Simulate physics sync during merge ---
        // Create game state where tile 31 has moved to (3, 1.8)
        var state = CreateGameState(8, 8);
        SetTile(ref state, 3, 1, new Tile(31, TileType.Green, new Vector2(3, 1.8f)));
        SetTile(ref state, 2, 2, new Tile(200, TileType.Red, 2, 2, BombType.Horizontal));

        _visualState.SuppressNewTileSync = _player.HasActiveAnimations;
        _visualState.SyncFallingTilesFromGameState(in state);

        // Gravity tile position should NOT be updated (IsBeingAnimated)
        gravityTile = _visualState.GetTile(31);
        Assert.Equal(new Vector2(3, 1), gravityTile!.Position);

        // New bomb tile (200) should NOT be added (SuppressNewTileSync)
        Assert.Null(_visualState.GetTile(200));

        // --- After merge (T = 0.35s, merge done at 0.3s) ---
        _player.Tick(0.25f); // total ~0.35s

        // New bomb tile should exist now (SpawnTileCommand fired)
        var newBomb = _visualState.GetTile(200);
        Assert.NotNull(newBomb);
        Assert.Equal(BombType.Horizontal, newBomb.BombType);

        // Old bomb tile should be removed
        Assert.Null(_visualState.GetTile(20));

        // Gravity tile should be moving (no longer held)
        gravityTile = _visualState.GetTile(31);
        Assert.NotNull(gravityTile);
        Assert.True(gravityTile.IsBeingAnimated, "Gravity tile should be in move animation");
    }

    #endregion

    #region Fire-and-Forget Commands — HasActiveAnimations

    [Fact]
    public void ShowEffectCommand_DoesNotBlockHasActiveAnimations()
    {
        var commands = new RenderCommand[]
        {
            new ShowEffectCommand
            {
                EffectType = "bomb_created",
                Position = new Vector2(2, 2),
                StartTime = 0f,
                Duration = 0.3f
            }
        };

        _player.Load(commands);
        _player.Tick(0.01f);

        // Effect command fired, but should NOT block HasActiveAnimations
        Assert.False(_player.HasActiveAnimations,
            "ShowEffectCommand should be fire-and-forget, not blocking HasActiveAnimations");

        // Effect should still exist in visual state
        Assert.Equal(1, _visualState.Effects.Count);
    }

    [Fact]
    public void ShowMatchHighlightCommand_DoesNotBlockHasActiveAnimations()
    {
        var commands = new RenderCommand[]
        {
            new ShowMatchHighlightCommand
            {
                Positions = new[] { new Position(1, 2), new Position(2, 2), new Position(3, 2) },
                StartTime = 0f,
                Duration = 0.1f
            }
        };

        _player.Load(commands);
        _player.Tick(0.01f);

        Assert.False(_player.HasActiveAnimations,
            "ShowMatchHighlightCommand should be fire-and-forget, not blocking HasActiveAnimations");

        // Effects should still be created
        Assert.Equal(3, _visualState.Effects.Count);
    }

    [Fact]
    public void MoveTileCommand_DoesBlockHasActiveAnimations()
    {
        var commands = new RenderCommand[]
        {
            new SpawnTileCommand
            {
                TileId = 1, Type = TileType.Red, Bomb = BombType.None,
                GridPos = new Position(2, 2), SpawnPos = new Vector2(2, 2),
                StartTime = 0f, Duration = 0f
            },
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(2, 2), To = new Vector2(3, 2),
                StartTime = 0f, Duration = 0.3f,
                Easing = EasingType.Linear
            }
        };

        _player.Load(commands);
        _player.Tick(0.01f);

        // Regular animation command SHOULD block HasActiveAnimations
        Assert.True(_player.HasActiveAnimations,
            "MoveTileCommand with duration should block HasActiveAnimations");
    }

    [Fact]
    public void MixedCommands_OnlyNonFireAndForgetBlockAnimations()
    {
        // Fire-and-forget effect + regular move: only the move blocks
        var commands = new RenderCommand[]
        {
            new SpawnTileCommand
            {
                TileId = 1, Type = TileType.Red, Bomb = BombType.None,
                GridPos = new Position(2, 2), SpawnPos = new Vector2(2, 2),
                StartTime = 0f, Duration = 0f
            },
            new ShowEffectCommand
            {
                EffectType = "bomb_created",
                Position = new Vector2(2, 2),
                StartTime = 0f,
                Duration = 0.5f
            },
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(2, 2), To = new Vector2(3, 2),
                StartTime = 0f, Duration = 0.2f,
                Easing = EasingType.Linear
            }
        };

        _player.Load(commands);
        _player.Tick(0.01f);

        Assert.True(_player.HasActiveAnimations, "Move is still active");

        // Advance past move duration but effect is still running
        _player.Tick(0.25f);

        // Move finished, effect still alive in VisualState but should NOT block
        Assert.False(_player.HasActiveAnimations,
            "After move finishes, effect alone should not block HasActiveAnimations");
    }

    #endregion

    #region Helpers

    private static GameState CreateGameState(int width, int height)
    {
        return new GameState(width, height, 6, new DefaultRandom(42));
    }

    private static void SetTile(ref GameState state, int x, int y, Tile tile)
    {
        state.SetTile(x, y, tile);
    }

    #endregion
}
