using System.Numerics;
using Match3.Core.Choreography;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Presentation;
using Xunit;

namespace Match3.Web.Tests.Presentation;

public class PlayerTests
{
    private readonly Player _player;

    public PlayerTests()
    {
        _player = new Player();
    }

    [Fact]
    public void Load_ClearsExistingCommands()
    {
        _player.Load(new RenderCommand[]
        {
            new MoveTileCommand { TileId = 1, From = Vector2.Zero, To = Vector2.One, Duration = 0.1f }
        });

        _player.Load(Array.Empty<RenderCommand>());

        Assert.False(_player.HasActiveAnimations);
    }

    [Fact]
    public void Tick_MoveTile_InterpolatesPosition()
    {
        // Setup: add a tile first
        _player.VisualState.AddTile(1, ElementType.Item1, new Position(0, 0), Vector2.Zero);

        _player.Load(new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(0, 0),
                To = new Vector2(0, 1),
                Duration = 0.1f,
                StartTime = 0f
            }
        });

        // Tick to 50%
        _player.Tick(0.05f);

        var tile = _player.VisualState.GetTile(1);
        Assert.NotNull(tile);
        // Position should be somewhere between 0 and 1 (not exact 0.5 due to easing)
        Assert.True(tile.Position.Y > 0f);
        Assert.True(tile.Position.Y < 1f);
    }

    [Fact]
    public void Tick_MoveTile_CompletesAtEnd()
    {
        _player.VisualState.AddTile(1, ElementType.Item1, new Position(0, 0), Vector2.Zero);

        _player.Load(new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(0, 0),
                To = new Vector2(0, 1),
                Duration = 0.1f,
                StartTime = 0f
            }
        });

        _player.Tick(0.15f); // Past the end

        var tile = _player.VisualState.GetTile(1);
        Assert.NotNull(tile);
        Assert.Equal(1f, tile.Position.Y, 0.001f);
    }

    [Fact]
    public void Tick_SpawnTile_AddsTileToVisualState()
    {
        _player.Load(new RenderCommand[]
        {
            new SpawnTileCommand
            {
                TileId = 1,
                Type = ElementType.Item3,
                GridPos = new Position(3, 0),
                SpawnPos = new Vector2(3, -1),
                StartTime = 0f,
                Duration = 0f
            }
        });

        _player.Tick(0.01f);

        var tile = _player.VisualState.GetTile(1);
        Assert.NotNull(tile);
        Assert.Equal(ElementType.Item3, tile.TileType);
    }

    [Fact]
    public void Tick_RemoveTile_RemovesTileFromVisualState()
    {
        _player.VisualState.AddTile(1, ElementType.Item1, new Position(0, 0), Vector2.Zero);

        // Keep an active animation so fast-forward doesn't skip ahead
        _player.VisualState.AddTile(2, ElementType.Item3, new Position(1, 0), new Vector2(1, 0));
        _player.Load(new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 2,
                From = new Vector2(1, 0),
                To = new Vector2(1, 1),
                StartTime = 0f,
                Duration = 0.2f
            },
            new RemoveTileCommand
            {
                TileId = 1,
                StartTime = 0.1f,
                Duration = 0f
            }
        });

        // Before remove time - tile should still exist
        _player.Tick(0.05f);
        Assert.NotNull(_player.VisualState.GetTile(1));

        // After remove time
        _player.Tick(0.1f);
        Assert.Null(_player.VisualState.GetTile(1));
    }

    [Fact]
    public void Tick_SwapTiles_InterpolatesPositions()
    {
        _player.VisualState.AddTile(1, ElementType.Item1, new Position(0, 0), new Vector2(0, 0));
        _player.VisualState.AddTile(2, ElementType.Item3, new Position(1, 0), new Vector2(1, 0));

        _player.Load(new RenderCommand[]
        {
            new SwapTilesCommand
            {
                TileAId = 1,
                TileBId = 2,
                PosA = new Vector2(0, 0),
                PosB = new Vector2(1, 0),
                Duration = 0.1f,
                StartTime = 0f
            }
        });

        _player.Tick(0.1f); // Complete

        var tileA = _player.VisualState.GetTile(1);
        var tileB = _player.VisualState.GetTile(2);

        Assert.NotNull(tileA);
        Assert.NotNull(tileB);
        Assert.Equal(1f, tileA.Position.X, 0.001f); // A moved to B's position
        Assert.Equal(0f, tileB.Position.X, 0.001f); // B moved to A's position
    }

    [Fact]
    public void Tick_DestroyTile_FadesOutTile()
    {
        _player.VisualState.AddTile(1, ElementType.Item1, new Position(0, 0), Vector2.Zero);

        _player.Load(new RenderCommand[]
        {
            new DestroyTileCommand
            {
                TileId = 1,
                Position = Vector2.Zero,
                Reason = DestroyReason.Match,
                Duration = 0.2f,
                StartTime = 0f
            }
        });

        _player.Tick(0.1f); // 50%

        var tile = _player.VisualState.GetTile(1);
        Assert.NotNull(tile);
        Assert.True(tile.Alpha < 1f); // Should be fading
    }

    [Fact]
    public void Tick_ShowEffect_AddsEffectToVisualState()
    {
        _player.Load(new RenderCommand[]
        {
            new ShowEffectCommand
            {
                EffectType = "explosion",
                Position = new Vector2(3, 4),
                Duration = 0.3f,
                StartTime = 0f
            }
        });

        _player.Tick(0.01f);

        Assert.Single(_player.VisualState.Effects);
        Assert.Equal("explosion", _player.VisualState.Effects[0].EffectType);
    }

    [Fact]
    public void Tick_Projectile_SpawnAndMove()
    {
        _player.Load(new RenderCommand[]
        {
            new SpawnProjectileCommand
            {
                ProjectileId = 100,
                Origin = new Vector2(3, 4),
                ArcHeight = 1.5f,
                Type = ProjectileType.Ufo,
                StartTime = 0f,
                Duration = 0.3f
            },
            new MoveProjectileCommand
            {
                ProjectileId = 100,
                From = new Vector2(3, 4),
                To = new Vector2(5, 6),
                StartTime = 0.3f,
                Duration = 0.2f
            }
        });

        _player.Tick(0.01f);

        var proj = _player.VisualState.GetProjectile(100);
        Assert.NotNull(proj);
    }

    [Fact]
    public void SeekTo_RewindFromStart()
    {
        _player.VisualState.AddTile(1, ElementType.Item1, new Position(0, 0), Vector2.Zero);

        _player.Load(new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(0, 0),
                To = new Vector2(0, 1),
                Duration = 1.0f,  // Long enough so time-reset doesn't trigger
                StartTime = 0f
            }
        });

        _player.Tick(0.5f); // Partway through
        _player.SeekTo(0.25f); // Rewind to 25%

        // After rewind, time should be at the target
        Assert.Equal(0.25f, _player.CurrentTime, 0.02f);
    }

    [Fact]
    public void SkipToEnd_CompletesAllCommands()
    {
        _player.VisualState.AddTile(1, ElementType.Item1, new Position(0, 0), Vector2.Zero);

        _player.Load(new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(0, 0),
                To = new Vector2(0, 1),
                Duration = 0.1f,
                StartTime = 0f
            }
        });

        _player.SkipToEnd();

        var tile = _player.VisualState.GetTile(1);
        Assert.NotNull(tile);
        Assert.Equal(1f, tile.Position.Y, 0.001f);
        Assert.False(_player.HasActiveAnimations);
    }

    [Fact]
    public void HasActiveAnimations_TrueWhileCommandsRunning()
    {
        _player.VisualState.AddTile(1, ElementType.Item1, new Position(0, 0), Vector2.Zero);

        _player.Load(new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(0, 0),
                To = new Vector2(0, 1),
                Duration = 0.1f,
                StartTime = 0f
            }
        });

        Assert.True(_player.HasActiveAnimations);

        _player.Tick(0.05f);
        Assert.True(_player.HasActiveAnimations);

        _player.Tick(0.1f); // Past the end
        Assert.False(_player.HasActiveAnimations);
    }

    [Fact]
    public void Clear_RemovesAllCommands()
    {
        _player.Load(new RenderCommand[]
        {
            new MoveTileCommand { TileId = 1, Duration = 1f, StartTime = 0f }
        });

        _player.Clear();

        Assert.False(_player.HasActiveAnimations);
    }

    [Fact]
    public void Append_AddsCommandsToExistingSequence()
    {
        _player.VisualState.AddTile(1, ElementType.Item1, new Position(0, 0), Vector2.Zero);
        _player.VisualState.AddTile(2, ElementType.Item3, new Position(1, 0), new Vector2(1, 0));

        _player.Load(new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(0, 0),
                To = new Vector2(0, 1),
                Duration = 0.1f,
                StartTime = 0f
            }
        });

        _player.Append(new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 2,
                From = new Vector2(1, 0),
                To = new Vector2(1, 1),
                Duration = 0.1f,
                StartTime = 0.1f
            }
        });

        _player.Tick(0.2f); // Past both

        var tile1 = _player.VisualState.GetTile(1);
        var tile2 = _player.VisualState.GetTile(2);
        Assert.Equal(1f, tile1?.Position.Y ?? 0f, 0.001f);
        Assert.Equal(1f, tile2?.Position.Y ?? 0f, 0.001f);
    }

    #region Edge Cases

    [Fact]
    public void Tick_NegativeDeltaTime_Ignored()
    {
        _player.VisualState.AddTile(1, ElementType.Item1, new Position(0, 0), Vector2.Zero);

        _player.Load(new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(0, 0),
                To = new Vector2(0, 1),
                Duration = 0.1f,
                StartTime = 0f
            }
        });

        _player.Tick(0.05f);
        float timeAfterFirstTick = _player.CurrentTime;

        _player.Tick(-0.02f);  // Negative delta

        Assert.Equal(timeAfterFirstTick, _player.CurrentTime);  // Time should not change
    }

    [Fact]
    public void Tick_ZeroDeltaTime_Ignored()
    {
        _player.Load(new RenderCommand[]
        {
            new MoveTileCommand { TileId = 1, Duration = 0.1f, StartTime = 0f }
        });

        _player.Tick(0f);

        Assert.Equal(0f, _player.CurrentTime);
    }

    [Fact]
    public void Tick_LargeDeltaTime_SkipsToEnd()
    {
        _player.VisualState.AddTile(1, ElementType.Item1, new Position(0, 0), Vector2.Zero);

        _player.Load(new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(0, 0),
                To = new Vector2(0, 1),
                Duration = 0.1f,
                StartTime = 0f
            }
        });

        _player.Tick(10.0f);  // Very large delta

        var tile = _player.VisualState.GetTile(1);
        Assert.NotNull(tile);
        Assert.Equal(1f, tile.Position.Y, 0.001f);  // Should be at final position
        Assert.False(_player.HasActiveAnimations);
    }

    [Fact]
    public void Tick_MultipleMovesSameTile_ExecutesSequentially()
    {
        _player.VisualState.AddTile(1, ElementType.Item1, new Position(0, 0), Vector2.Zero);

        _player.Load(new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(0, 0),
                To = new Vector2(0, 1),
                Duration = 0.1f,
                StartTime = 0f
            },
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(0, 1),
                To = new Vector2(0, 2),
                Duration = 0.1f,
                StartTime = 0.1f
            }
        });

        // First move done
        _player.Tick(0.1f);
        var tile = _player.VisualState.GetTile(1);
        Assert.Equal(1f, tile?.Position.Y ?? 0f, 0.001f);

        // Second move done
        _player.Tick(0.1f);
        tile = _player.VisualState.GetTile(1);
        Assert.Equal(2f, tile?.Position.Y ?? 0f, 0.001f);
    }

    [Fact]
    public void Tick_OverlappingCommands_BothExecute()
    {
        _player.VisualState.AddTile(1, ElementType.Item1, new Position(0, 0), Vector2.Zero);
        _player.VisualState.AddTile(2, ElementType.Item3, new Position(1, 0), new Vector2(1, 0));

        _player.Load(new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(0, 0),
                To = new Vector2(0, 1),
                Duration = 0.2f,
                StartTime = 0f
            },
            new MoveTileCommand
            {
                TileId = 2,
                From = new Vector2(1, 0),
                To = new Vector2(1, 1),
                Duration = 0.2f,
                StartTime = 0.05f  // Overlaps with first command
            }
        });

        _player.Tick(0.25f);

        var tile1 = _player.VisualState.GetTile(1);
        var tile2 = _player.VisualState.GetTile(2);
        Assert.Equal(1f, tile1?.Position.Y ?? 0f, 0.001f);
        Assert.Equal(1f, tile2?.Position.Y ?? 0f, 0.001f);
    }

    [Fact]
    public void Tick_CommandWithZeroDuration_ExecutesInstantly()
    {
        _player.Load(new RenderCommand[]
        {
            new SpawnTileCommand
            {
                TileId = 1,
                Type = ElementType.Item1,

                GridPos = new Position(0, 0),
                SpawnPos = Vector2.Zero,
                StartTime = 0f,
                Duration = 0f
            }
        });

        // After start time - should spawn instantly
        _player.Tick(0.01f);
        Assert.NotNull(_player.VisualState.GetTile(1));
    }

    [Fact]
    public void Append_DuringAnimation_ContinuesCorrectly()
    {
        _player.VisualState.AddTile(1, ElementType.Item1, new Position(0, 0), Vector2.Zero);
        _player.VisualState.AddTile(2, ElementType.Item3, new Position(1, 0), new Vector2(1, 0));

        _player.Load(new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(0, 0),
                To = new Vector2(0, 1),
                Duration = 0.2f,
                StartTime = 0f
            }
        });

        // Start first animation
        _player.Tick(0.1f);

        // Append new command while animation is in progress
        _player.Append(new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 2,
                From = new Vector2(1, 0),
                To = new Vector2(1, 1),
                Duration = 0.1f,
                StartTime = 0.15f
            }
        });

        // Complete both animations
        _player.Tick(0.2f);

        var tile1 = _player.VisualState.GetTile(1);
        var tile2 = _player.VisualState.GetTile(2);
        Assert.Equal(1f, tile1?.Position.Y ?? 0f, 0.001f);
        Assert.Equal(1f, tile2?.Position.Y ?? 0f, 0.001f);
    }

    [Fact]
    public void Append_CommandsWithEarlierStartTime_AreSkipped()
    {
        // Commands with StartTime before current time are skipped (cannot go back in time)
        _player.VisualState.AddTile(1, ElementType.Item1, new Position(0, 0), Vector2.Zero);
        _player.VisualState.AddTile(2, ElementType.Item3, new Position(1, 0), new Vector2(1, 0));

        _player.Load(new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(0, 0),
                To = new Vector2(0, 1),
                Duration = 0.1f,
                StartTime = 0.2f
            }
        });

        _player.Tick(0.1f);  // Time = 0.1

        // Append command with start time 0.0 (before current time of 0.1)
        _player.Append(new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 2,
                From = new Vector2(1, 0),
                To = new Vector2(1, 1),
                Duration = 0.1f,
                StartTime = 0f  // Before current time
            }
        });

        // Tick past both potential end times
        _player.Tick(0.25f);

        // Tile 1 should have moved (started at 0.2, ended at 0.3)
        var tile1 = _player.VisualState.GetTile(1);
        Assert.Equal(1f, tile1?.Position.Y ?? 0f, 0.001f);

        // Tile 2 should NOT have moved because its StartTime was before current time when appended
        var tile2 = _player.VisualState.GetTile(2);
        Assert.Equal(0f, tile2?.Position.Y ?? 0f, 0.001f);
    }

    [Fact]
    public void Append_CommandsWithCurrentOrFutureStartTime_Execute()
    {
        _player.VisualState.AddTile(1, ElementType.Item1, new Position(0, 0), Vector2.Zero);
        _player.VisualState.AddTile(2, ElementType.Item3, new Position(1, 0), new Vector2(1, 0));

        _player.Load(new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(0, 0),
                To = new Vector2(0, 1),
                Duration = 0.1f,
                StartTime = 0f
            }
        });

        _player.Tick(0.05f);  // Time = 0.05

        // Append command with start time at or after current time
        _player.Append(new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 2,
                From = new Vector2(1, 0),
                To = new Vector2(1, 1),
                Duration = 0.1f,
                StartTime = 0.05f  // At current time
            }
        });

        _player.Tick(0.2f);

        var tile1 = _player.VisualState.GetTile(1);
        var tile2 = _player.VisualState.GetTile(2);
        Assert.Equal(1f, tile1?.Position.Y ?? 0f, 0.001f);
        Assert.Equal(1f, tile2?.Position.Y ?? 0f, 0.001f);
    }

    #endregion

    #region UpdateTileTypeCommand Tests

    [Fact]
    public void Tick_UpdateTileType_ChangesType()
    {
        _player.VisualState.AddTile(1, ElementType.Item1, new Position(3, 4), new Vector2(3, 4));

        _player.Load(new RenderCommand[]
        {
            new UpdateTileTypeCommand
            {
                TileId = 1,
                TileType = ElementType.HorizontalRocket,
                StartTime = 0f,
                Duration = 0f
            }
        });

        _player.Tick(0.01f);

        var tile = _player.VisualState.GetTile(1);
        Assert.NotNull(tile);
        Assert.Equal(ElementType.HorizontalRocket, tile.TileType);
    }

    [Fact]
    public void Tick_UpdateTileType_NonExistentTile_NoError()
    {
        _player.Load(new RenderCommand[]
        {
            new UpdateTileTypeCommand
            {
                TileId = 999,  // Non-existent
                TileType = ElementType.HorizontalRocket,
                StartTime = 0f,
                Duration = 0f
            }
        });

        // Should not throw
        var exception = Record.Exception(() => _player.Tick(0.01f));
        Assert.Null(exception);
    }

    #endregion

    #region Projectile Command Tests

    [Fact]
    public void Tick_ImpactProjectile_FadesOutAndHides()
    {
        _player.VisualState.AddProjectile(100, new Vector2(3, 4));

        _player.Load(new RenderCommand[]
        {
            new ImpactProjectileCommand
            {
                ProjectileId = 100,
                Position = new Vector2(5, 6),
                EffectType = "explosion",
                StartTime = 0f,
                Duration = 0.2f
            }
        });

        // At 60% through - should be hidden (t > 0.5)
        _player.Tick(0.12f);

        var proj = _player.VisualState.GetProjectile(100);
        Assert.NotNull(proj);
        Assert.False(proj.IsVisible);
    }

    [Fact]
    public void Tick_RemoveProjectile_RemovesFromVisualState()
    {
        _player.VisualState.AddProjectile(100, new Vector2(3, 4));

        // Keep an active animation so fast-forward doesn't skip ahead
        _player.VisualState.AddTile(1, ElementType.Item1, new Position(0, 0), Vector2.Zero);
        _player.Load(new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 1,
                From = Vector2.Zero,
                To = Vector2.One,
                StartTime = 0f,
                Duration = 0.2f
            },
            new RemoveProjectileCommand
            {
                ProjectileId = 100,
                StartTime = 0.1f,
                Duration = 0f
            }
        });

        // Before remove time
        _player.Tick(0.05f);
        Assert.NotNull(_player.VisualState.GetProjectile(100));

        // After remove time
        _player.Tick(0.1f);
        Assert.Null(_player.VisualState.GetProjectile(100));
    }

    [Fact]
    public void Tick_ProjectileFullLifecycle_SpawnMoveImpactRemove()
    {
        _player.Load(new RenderCommand[]
        {
            new SpawnProjectileCommand
            {
                ProjectileId = 100,
                Origin = new Vector2(0, 0),
                ArcHeight = 1.5f,
                Type = ProjectileType.Ufo,
                StartTime = 0f,
                Duration = 0.2f
            },
            new MoveProjectileCommand
            {
                ProjectileId = 100,
                From = new Vector2(0, 0),
                To = new Vector2(3, 4),
                StartTime = 0.2f,
                Duration = 0.3f
            },
            new ImpactProjectileCommand
            {
                ProjectileId = 100,
                Position = new Vector2(3, 4),
                EffectType = "explosion",
                StartTime = 0.5f,
                Duration = 0.2f
            },
            new RemoveProjectileCommand
            {
                ProjectileId = 100,
                StartTime = 0.7f,
                Duration = 0f
            }
        });

        // After spawn
        _player.Tick(0.1f);
        Assert.NotNull(_player.VisualState.GetProjectile(100));

        // After move
        _player.Tick(0.4f);
        var proj = _player.VisualState.GetProjectile(100);
        Assert.NotNull(proj);
        Assert.Equal(3f, proj.Position.X, 0.001f);
        Assert.Equal(4f, proj.Position.Y, 0.001f);

        // After remove
        _player.Tick(0.3f);
        Assert.Null(_player.VisualState.GetProjectile(100));
    }

    #endregion

    #region ShowMatchHighlightCommand Tests

    [Fact]
    public void Tick_ShowMatchHighlight_AddsMultipleEffects()
    {
        _player.Load(new RenderCommand[]
        {
            new ShowMatchHighlightCommand
            {
                Positions = new[]
                {
                    new Position(0, 0),
                    new Position(1, 0),
                    new Position(2, 0)
                },
                StartTime = 0f,
                Duration = 0.1f
            }
        });

        _player.Tick(0.01f);

        // Should add 3 highlight effects
        Assert.Equal(3, _player.VisualState.Effects.Count);
        Assert.All(_player.VisualState.Effects, e => Assert.Equal("match_highlight", e.EffectType));
    }

    #endregion

    #region Easing Tests

    [Fact]
    public void Tick_MoveTile_OutCubicEasing_NotLinear()
    {
        _player.VisualState.AddTile(1, ElementType.Item1, new Position(0, 0), Vector2.Zero);

        _player.Load(new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(0, 0),
                To = new Vector2(0, 1),
                Duration = 1.0f,
                StartTime = 0f,
                Easing = EasingType.OutCubic
            }
        });

        _player.Tick(0.5f);  // 50% time

        var tile = _player.VisualState.GetTile(1);
        Assert.NotNull(tile);
        // OutCubic at 50% should be more than 50% distance (easing out)
        Assert.True(tile.Position.Y > 0.5f, $"Expected > 0.5, got {tile.Position.Y}");
    }

    [Fact]
    public void Tick_MoveTile_LinearEasing_IsLinear()
    {
        _player.VisualState.AddTile(1, ElementType.Item1, new Position(0, 0), Vector2.Zero);

        _player.Load(new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 1,
                From = new Vector2(0, 0),
                To = new Vector2(0, 1),
                Duration = 1.0f,
                StartTime = 0f,
                Easing = EasingType.Linear
            }
        });

        _player.Tick(0.5f);

        var tile = _player.VisualState.GetTile(1);
        Assert.NotNull(tile);
        Assert.Equal(0.5f, tile.Position.Y, 0.001f);
    }

    #endregion

    #region SyncFromGameState Tests

    [Fact]
    public void SyncFromGameState_ClearsCommandsAndResets()
    {
        _player.VisualState.AddTile(1, ElementType.Item1, new Position(0, 0), Vector2.Zero);

        _player.Load(new RenderCommand[]
        {
            new MoveTileCommand
            {
                TileId = 1,
                From = Vector2.Zero,
                To = Vector2.One,
                Duration = 1f,
                StartTime = 0f
            }
        });

        _player.Tick(0.5f);

        // Create a minimal game state for sync
        var state = CreateEmptyGameState();
        _player.SyncFromGameState(in state);

        Assert.Equal(0f, _player.CurrentTime);
        Assert.False(_player.HasActiveAnimations);
    }

    private static GameState CreateEmptyGameState()
    {
        return new GameState(8, 8, 6, new Match3.Random.DefaultRandom(12345));
    }

    #endregion

    #region UFO Launch & Retarget Tests

    [Fact]
    public void Tick_UfoLaunch_MovesToTarget()
    {
        _player.VisualState.AddTile(1, ElementType.Ufo, new Position(0, 0), Vector2.Zero);

        _player.Load(new RenderCommand[]
        {
            new UfoLaunchCommand
            {
                TileId = 1,
                Origin = new Vector2(0, 0),
                Target = new Vector2(4, 0),
                StayFraction = 0.35f,
                StartTime = 0f,
                Duration = 2f
            }
        });

        // At 100% → should be at target
        _player.Tick(2.1f);

        var tile = _player.VisualState.GetTile(1);
        Assert.NotNull(tile);
        Assert.Equal(4f, tile.Position.X, 0.01f);
        Assert.Equal(0f, tile.Position.Y, 0.01f);
    }

    [Fact]
    public void Tick_UfoLaunch_StaysDuringSpinUp()
    {
        _player.VisualState.AddTile(1, ElementType.Ufo, new Position(0, 0), Vector2.Zero);

        _player.Load(new RenderCommand[]
        {
            new UfoLaunchCommand
            {
                TileId = 1,
                Origin = new Vector2(0, 0),
                Target = new Vector2(10, 0),
                StayFraction = 0.35f,
                StartTime = 0f,
                Duration = 2f
            }
        });

        // At 20% (within StayFraction=0.35) → should still be at origin
        _player.Tick(0.4f);

        var tile = _player.VisualState.GetTile(1);
        Assert.NotNull(tile);
        Assert.Equal(0f, tile.Position.X, 0.1f);
    }

    [Fact]
    public void UfoRetarget_PositionIsContinuous()
    {
        _player.VisualState.AddTile(1, ElementType.Ufo, new Position(0, 0), Vector2.Zero);

        _player.Load(new RenderCommand[]
        {
            new UfoLaunchCommand
            {
                TileId = 1,
                Origin = new Vector2(0, 0),
                Target = new Vector2(6, 0),
                StayFraction = 0.35f,
                StartTime = 0f,
                Duration = 2f
            }
        });

        // Advance to 70% of flight
        _player.Tick(1.4f);
        var tile = _player.VisualState.GetTile(1);
        Assert.NotNull(tile);
        var posBeforeRetarget = tile.Position;

        // Retarget to new position
        _player.Load(new RenderCommand[]
        {
            new UfoRetargetCommand
            {
                TileId = 1,
                NewTarget = new Vector2(0, 6),
                NewDuration = 1f, // Ignored by Player — it recomputes from visual distance
                StartTime = 1.4f,
                Duration = 0
            }
        });

        // Tick a tiny amount to process the retarget
        _player.Tick(0.001f);

        // Position should be continuous (no jump)
        var posAfterRetarget = tile.Position;
        float distance = Vector2.Distance(posBeforeRetarget, posAfterRetarget);
        Assert.True(distance < 0.1f, $"Position jumped {distance} on retarget");
    }

    [Fact]
    public void UfoRetarget_NewSegmentHasZeroStayFraction()
    {
        _player.VisualState.AddTile(1, ElementType.Ufo, new Position(0, 0), Vector2.Zero);

        _player.Load(new RenderCommand[]
        {
            new UfoLaunchCommand
            {
                TileId = 1,
                Origin = new Vector2(0, 0),
                Target = new Vector2(4, 0),
                StayFraction = 0.35f,
                StartTime = 0f,
                Duration = 2f
            }
        });

        // Advance past spin-up
        _player.Tick(1.0f);

        // Retarget
        _player.Append(new RenderCommand[]
        {
            new UfoRetargetCommand
            {
                TileId = 1,
                NewTarget = new Vector2(0, 4),
                NewDuration = 1f,
                StartTime = 1.0f,
                Duration = 0
            }
        });

        _player.Tick(0.001f);

        // Advance slightly — tile should start moving immediately (no spin-up delay)
        var tile = _player.VisualState.GetTile(1);
        Assert.NotNull(tile);
        var posAfterRetarget = tile.Position;

        _player.Tick(0.5f);
        var posAfterSomeTime = tile.Position;

        // Should have moved (StayFraction=0 means no delay)
        float moved = Vector2.Distance(posAfterRetarget, posAfterSomeTime);
        Assert.True(moved > 0.1f, $"UFO should have moved after retarget, but only moved {moved}");
    }

    [Fact]
    public void UfoRetarget_RecomputesDurationFromVisualDistance()
    {
        _player.VisualState.AddTile(1, ElementType.Ufo, new Position(0, 0), Vector2.Zero);

        _player.Load(new RenderCommand[]
        {
            new UfoLaunchCommand
            {
                TileId = 1,
                Origin = new Vector2(0, 0),
                Target = new Vector2(6, 0),
                StayFraction = 0f, // No spin-up for easier calculation
                StartTime = 0f,
                Duration = 3f // 6 units / 2 speed = 3s
            }
        });

        // Advance to ~50%
        _player.Tick(1.5f);
        var tile = _player.VisualState.GetTile(1);
        Assert.NotNull(tile);

        // Retarget to nearby position — duration should be short
        _player.Append(new RenderCommand[]
        {
            new UfoRetargetCommand
            {
                TileId = 1,
                NewTarget = tile.Position + new Vector2(1, 0), // 1 unit away
                NewDuration = 999f, // Player should ignore this and compute its own
                StartTime = 1.5f,
                Duration = 0
            }
        });

        _player.Tick(0.001f);

        // Flight should complete quickly (1 unit / 2 speed = 0.5s)
        _player.Tick(0.6f); // More than enough time
        Assert.False(_player.HasActiveAnimations, "UFO flight should have completed");
    }

    [Fact]
    public void UfoLaunch_SetsFlightProgressAndDuration()
    {
        _player.VisualState.AddTile(1, ElementType.Ufo, new Position(0, 0), Vector2.Zero);

        _player.Load(new RenderCommand[]
        {
            new UfoLaunchCommand
            {
                TileId = 1,
                Origin = new Vector2(0, 0),
                Target = new Vector2(4, 0),
                StartTime = 0f,
                Duration = 2f
            }
        });

        _player.Tick(1.0f); // 50%

        var tile = _player.VisualState.GetTile(1);
        Assert.NotNull(tile);
        Assert.Equal(0.5f, tile.UfoFlightProgress, 0.01f);
        Assert.Equal(2f, tile.UfoFlightDuration, 0.01f);
    }

    [Fact]
    public void UfoRetarget_WithNoActiveUfo_IsIgnored()
    {
        // No UFO launched — retarget should be silently ignored
        _player.VisualState.AddTile(1, ElementType.Ufo, new Position(0, 0), Vector2.Zero);

        _player.Load(new RenderCommand[]
        {
            new UfoRetargetCommand
            {
                TileId = 1,
                NewTarget = new Vector2(5, 5),
                NewDuration = 1f,
                StartTime = 0f,
                Duration = 0
            }
        });

        // Should not throw
        _player.Tick(0.1f);
    }

    #endregion
}
