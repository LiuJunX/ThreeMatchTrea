using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Presentation;

/// <summary>
/// Visual state for rendering. Tracks interpolated positions, scales, and effects.
/// Decoupled from GameState - can be behind or ahead for smooth animations.
/// </summary>
public sealed class VisualState : IVisualState
{
    private readonly Dictionary<long, TileVisual> _tiles = new();
    private readonly Dictionary<long, ProjectileVisual> _projectiles = new();
    private readonly List<VisualEffect> _effects = new();

    /// <summary>
    /// All tile visuals indexed by tile ID.
    /// </summary>
    public IReadOnlyDictionary<long, TileVisual> Tiles => _tiles;

    /// <summary>
    /// All projectile visuals indexed by projectile ID.
    /// </summary>
    public IReadOnlyDictionary<long, ProjectileVisual> Projectiles => _projectiles;

    /// <summary>
    /// All active visual effects.
    /// </summary>
    public IReadOnlyList<VisualEffect> Effects => _effects;

    /// <summary>
    /// Grid width.
    /// </summary>
    public int Width { get; private set; }

    /// <summary>
    /// Grid height.
    /// </summary>
    public int Height { get; private set; }

    /// <summary>
    /// Synchronize visual state from game state.
    /// Call this to reset visual state to match simulation.
    /// </summary>
    public void SyncFromGameState(in GameState state)
    {
        Width = state.Width;
        Height = state.Height;

        _tiles.Clear();

        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var tile = state.GetTile(x, y);
                if (tile.Type == TileType.None) continue;

                _tiles[tile.Id] = new TileVisual
                {
                    Id = tile.Id,
                    Position = new Vector2(x, y),
                    Scale = Vector2.One,
                    Alpha = 1f,
                    IsVisible = true,
                    TileType = tile.Type,
                    BombType = tile.Bomb,
                    GridPosition = new Position(x, y)
                };
            }
        }
    }

    /// <summary>
    /// Sync falling tile positions from game state.
    /// Call this each frame to update positions of tiles being moved by physics.
    /// This handles gravity-based movement that doesn't go through the event system.
    /// </summary>
    public void SyncFallingTilesFromGameState(in GameState state)
    {
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var tile = state.GetTile(x, y);
                if (tile.Type == TileType.None) continue;

                // Only sync tiles that are falling (being moved by physics)
                // or tiles that don't exist in visual state yet
                if (_tiles.TryGetValue(tile.Id, out var visual))
                {
                    // Update position from physics simulation
                    visual.Position = tile.Position;
                    visual.GridPosition = new Position(x, y);
                }
                else
                {
                    // Tile exists in game state but not in visual state - add it
                    _tiles[tile.Id] = new TileVisual
                    {
                        Id = tile.Id,
                        Position = tile.Position,
                        Scale = Vector2.One,
                        Alpha = 1f,
                        IsVisible = true,
                        TileType = tile.Type,
                        BombType = tile.Bomb,
                        GridPosition = new Position(x, y)
                    };
                }
            }
        }

        // Remove tiles that no longer exist in game state
        var tilesToRemove = new List<long>();
        foreach (var kvp in _tiles)
        {
            bool found = false;
            for (int y = 0; y < state.Height && !found; y++)
            {
                for (int x = 0; x < state.Width && !found; x++)
                {
                    var tile = state.GetTile(x, y);
                    if (tile.Id == kvp.Key && tile.Type != TileType.None)
                    {
                        found = true;
                    }
                }
            }
            if (!found)
            {
                tilesToRemove.Add(kvp.Key);
            }
        }
        foreach (var id in tilesToRemove)
        {
            _tiles.Remove(id);
        }
    }

    /// <summary>
    /// Add a new tile visual (for spawned tiles).
    /// </summary>
    public void AddTile(long tileId, TileType type, BombType bomb, Position gridPos, Vector2 spawnPos)
    {
        _tiles[tileId] = new TileVisual
        {
            Id = tileId,
            Position = spawnPos,
            Scale = Vector2.One,
            Alpha = 1f,
            IsVisible = true,
            TileType = type,
            BombType = bomb,
            GridPosition = gridPos
        };
    }

    /// <summary>
    /// Remove a tile visual.
    /// </summary>
    public void RemoveTile(long tileId)
    {
        _tiles.Remove(tileId);
    }

    /// <summary>
    /// Add a new projectile visual.
    /// </summary>
    public void AddProjectile(long projectileId, Vector2 position)
    {
        _projectiles[projectileId] = new ProjectileVisual
        {
            Id = projectileId,
            Position = position,
            IsVisible = true
        };
    }

    /// <summary>
    /// Remove a projectile visual.
    /// </summary>
    public void RemoveProjectile(long projectileId)
    {
        _projectiles.Remove(projectileId);
    }

    /// <inheritdoc />
    public void SetTilePosition(long tileId, Vector2 position)
    {
        if (_tiles.TryGetValue(tileId, out var tile))
        {
            tile.Position = position;
        }
    }

    /// <inheritdoc />
    public void SetTileScale(long tileId, Vector2 scale)
    {
        if (_tiles.TryGetValue(tileId, out var tile))
        {
            tile.Scale = scale;
        }
    }

    /// <inheritdoc />
    public void SetTileAlpha(long tileId, float alpha)
    {
        if (_tiles.TryGetValue(tileId, out var tile))
        {
            tile.Alpha = alpha;
        }
    }

    /// <inheritdoc />
    public void SetTileVisible(long tileId, bool visible)
    {
        if (_tiles.TryGetValue(tileId, out var tile))
        {
            tile.IsVisible = visible;
        }
    }

    /// <inheritdoc />
    public void SetProjectilePosition(long projectileId, Vector2 position)
    {
        if (_projectiles.TryGetValue(projectileId, out var proj))
        {
            proj.Position = position;
        }
    }

    /// <inheritdoc />
    public void SetProjectileVisible(long projectileId, bool visible)
    {
        if (_projectiles.TryGetValue(projectileId, out var proj))
        {
            proj.IsVisible = visible;
        }
    }

    /// <inheritdoc />
    public void AddEffect(string effectType, Vector2 position, float duration)
    {
        _effects.Add(new VisualEffect
        {
            EffectType = effectType,
            Position = position,
            Duration = duration,
            ElapsedTime = 0
        });
    }

    /// <summary>
    /// Update effects and remove completed ones.
    /// </summary>
    public void UpdateEffects(float deltaTime)
    {
        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            _effects[i].ElapsedTime += deltaTime;
            if (_effects[i].ElapsedTime >= _effects[i].Duration)
            {
                _effects.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Clear all effects.
    /// </summary>
    public void ClearEffects()
    {
        _effects.Clear();
    }

    /// <summary>
    /// Get tile visual by ID.
    /// </summary>
    public TileVisual? GetTile(long tileId)
    {
        return _tiles.TryGetValue(tileId, out var tile) ? tile : null;
    }

    /// <summary>
    /// Get projectile visual by ID.
    /// </summary>
    public ProjectileVisual? GetProjectile(long projectileId)
    {
        return _projectiles.TryGetValue(projectileId, out var proj) ? proj : null;
    }
}

/// <summary>
/// Visual representation of a tile.
/// </summary>
public sealed class TileVisual
{
    /// <summary>Tile ID.</summary>
    public long Id { get; init; }

    /// <summary>Current visual position (may differ from grid position during animation).</summary>
    public Vector2 Position { get; set; }

    /// <summary>Current scale (for animations).</summary>
    public Vector2 Scale { get; set; } = Vector2.One;

    /// <summary>Current alpha (for fade animations).</summary>
    public float Alpha { get; set; } = 1f;

    /// <summary>Whether the tile is visible.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Type of tile.</summary>
    public TileType TileType { get; init; }

    /// <summary>Bomb type (if any).</summary>
    public BombType BombType { get; init; }

    /// <summary>Grid position.</summary>
    public Position GridPosition { get; set; }
}

/// <summary>
/// Visual representation of a projectile.
/// </summary>
public sealed class ProjectileVisual
{
    /// <summary>Projectile ID.</summary>
    public long Id { get; init; }

    /// <summary>Current visual position.</summary>
    public Vector2 Position { get; set; }

    /// <summary>Whether the projectile is visible.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Rotation angle for visual effects.</summary>
    public float Rotation { get; set; }
}

/// <summary>
/// Visual effect (explosion, sparkle, etc).
/// </summary>
public sealed class VisualEffect
{
    /// <summary>Type of effect.</summary>
    public string EffectType { get; init; } = string.Empty;

    /// <summary>Position of the effect.</summary>
    public Vector2 Position { get; set; }

    /// <summary>Total duration.</summary>
    public float Duration { get; init; }

    /// <summary>Elapsed time.</summary>
    public float ElapsedTime { get; set; }

    /// <summary>Progress (0-1).</summary>
    public float Progress => Duration > 0 ? ElapsedTime / Duration : 1f;
}
