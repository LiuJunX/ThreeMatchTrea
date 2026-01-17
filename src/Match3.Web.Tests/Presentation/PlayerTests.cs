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
        _player.VisualState.AddTile(1, TileType.Red, BombType.None, new Position(0, 0), Vector2.Zero);

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
        _player.VisualState.AddTile(1, TileType.Red, BombType.None, new Position(0, 0), Vector2.Zero);

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
                Type = TileType.Blue,
                Bomb = BombType.None,
                GridPos = new Position(3, 0),
                SpawnPos = new Vector2(3, -1),
                StartTime = 0f,
                Duration = 0f
            }
        });

        _player.Tick(0.01f);

        var tile = _player.VisualState.GetTile(1);
        Assert.NotNull(tile);
        Assert.Equal(TileType.Blue, tile.TileType);
    }

    [Fact]
    public void Tick_RemoveTile_RemovesTileFromVisualState()
    {
        _player.VisualState.AddTile(1, TileType.Red, BombType.None, new Position(0, 0), Vector2.Zero);

        _player.Load(new RenderCommand[]
        {
            new RemoveTileCommand
            {
                TileId = 1,
                StartTime = 0.1f,
                Duration = 0f
            }
        });

        // Before remove time
        _player.Tick(0.05f);
        Assert.NotNull(_player.VisualState.GetTile(1));

        // After remove time
        _player.Tick(0.1f);
        Assert.Null(_player.VisualState.GetTile(1));
    }

    [Fact]
    public void Tick_SwapTiles_InterpolatesPositions()
    {
        _player.VisualState.AddTile(1, TileType.Red, BombType.None, new Position(0, 0), new Vector2(0, 0));
        _player.VisualState.AddTile(2, TileType.Blue, BombType.None, new Position(1, 0), new Vector2(1, 0));

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
        _player.VisualState.AddTile(1, TileType.Red, BombType.None, new Position(0, 0), Vector2.Zero);

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
        _player.VisualState.AddTile(1, TileType.Red, BombType.None, new Position(0, 0), Vector2.Zero);

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
        _player.SeekTo(0.05f); // Rewind to 50%

        // After rewind, time should be at the target
        Assert.Equal(0.05f, _player.CurrentTime, 0.001f);
    }

    [Fact]
    public void SkipToEnd_CompletesAllCommands()
    {
        _player.VisualState.AddTile(1, TileType.Red, BombType.None, new Position(0, 0), Vector2.Zero);

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
        _player.VisualState.AddTile(1, TileType.Red, BombType.None, new Position(0, 0), Vector2.Zero);

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
        _player.VisualState.AddTile(1, TileType.Red, BombType.None, new Position(0, 0), Vector2.Zero);
        _player.VisualState.AddTile(2, TileType.Blue, BombType.None, new Position(1, 0), new Vector2(1, 0));

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
}
