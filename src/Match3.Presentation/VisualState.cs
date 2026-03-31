using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Presentation;

/// <summary>
/// Visual state for rendering. Tracks interpolated positions, scales, and effects.
/// Decoupled from GameState - can be behind or ahead for smooth animations.
/// </summary>
public sealed class VisualState : IVisualState
{
    private readonly Dictionary<int, TileVisual> _tiles = new();
    private readonly Dictionary<int, ProjectileVisual> _projectiles = new();
    private readonly Dictionary<Position, ObstacleVisual> _obstacles = new();
    private readonly Dictionary<Position, GroundVisual> _grounds = new();
    private readonly Dictionary<Position, CoverVisual> _covers = new();
    private readonly List<VisualEffect> _effects = new();
    private readonly HashSet<int> _aliveTileIds = new();
    private readonly List<int> _tilesToRemove = new();

    /// <summary>
    /// All tile visuals indexed by tile ID.
    /// </summary>
    public IReadOnlyDictionary<int, TileVisual> Tiles => _tiles;

    /// <summary>
    /// All projectile visuals indexed by projectile ID.
    /// </summary>
    public IReadOnlyDictionary<int, ProjectileVisual> Projectiles => _projectiles;

    /// <summary>
    /// All obstacle visuals indexed by grid position.
    /// </summary>
    public IReadOnlyDictionary<Position, ObstacleVisual> Obstacles => _obstacles;

    /// <summary>
    /// All ground visuals indexed by grid position.
    /// </summary>
    public IReadOnlyDictionary<Position, GroundVisual> Grounds => _grounds;

    /// <summary>
    /// All cover visuals indexed by grid position.
    /// </summary>
    public IReadOnlyDictionary<Position, CoverVisual> Covers => _covers;

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
        _obstacles.Clear();
        _grounds.Clear();
        _covers.Clear();

        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var tile = state.GetTile(x, y);
                if (tile.Type != ElementType.None)
                {
                    _tiles[tile.Id] = new TileVisual
                    {
                        Id = tile.Id,
                        Position = new Vector2(x, y),
                        Scale = Vector2.One,
                        Alpha = 1f,
                        IsVisible = true,
                        TileType = tile.Type,
                        CurrentStage = tile.Stage,
                        GridPosition = new Position(x, y)
                    };
                }

                var obstacle = state.GetObstacle(x, y);
                if (obstacle.Type != ObstacleType.None)
                {
                    var pos = new Position(x, y);
                    _obstacles[pos] = new ObstacleVisual
                    {
                        GridPosition = pos,
                        Type = obstacle.Type,
                        CurrentStage = obstacle.Stage,
                        State = obstacle.State,
                    };
                }

                var ground = state.GetGround(x, y);
                if (ground.Type != GroundType.None)
                {
                    var pos = new Position(x, y);
                    _grounds[pos] = new GroundVisual
                    {
                        GridPosition = pos,
                        Type = ground.Type,
                        CurrentHealth = ground.Health,
                    };
                }

                var cover = state.GetCover(x, y);
                if (cover.Type != CoverType.None)
                {
                    var pos = new Position(x, y);
                    _covers[pos] = new CoverVisual
                    {
                        GridPosition = pos,
                        Type = cover.Type,
                        CurrentHealth = cover.Health,
                    };
                }
            }
        }
    }

    /// <summary>
    /// Sync falling tile positions from game state (no interpolation, legacy).
    /// </summary>
    public void SyncFallingTilesFromGameState(in GameState state)
    {
        SyncFallingTilesFromGameState(in state, in state, 0f);
    }

    /// <summary>
    /// Sync falling tile positions with interpolation between two game states.
    /// Call this each frame to update positions of tiles being moved by physics.
    /// This handles gravity-based movement that doesn't go through the event system.
    /// Only syncs tiles with IsFalling=true to avoid overwriting animation positions.
    /// </summary>
    /// <param name="currentState">The last consumed tick state</param>
    /// <param name="nextState">The pre-computed next tick state (from ring buffer)</param>
    /// <param name="alpha">Interpolation factor 0-1 between current and next</param>
    public void SyncFallingTilesFromGameState(in GameState currentState, in GameState nextState, float alpha)
    {
        _aliveTileIds.Clear();

        for (int y = 0; y < currentState.Height; y++)
        {
            for (int x = 0; x < currentState.Width; x++)
            {
                var tile = currentState.GetTile(x, y);
                if (tile.Type == ElementType.None) continue;

                _aliveTileIds.Add(tile.Id);

                if (_tiles.TryGetValue(tile.Id, out var visual))
                {
                    // Skip tiles being controlled by Player animation.
                    if (visual.IsBeingAnimated)
                    {
                        continue;
                    }

                    // Interpolate position: check same grid position in nextState first (fast path),
                    // then fall back to ID scan only if needed
                    var nextTile = FindTileInNextState(in nextState, tile.Id, x, y);
                    if (nextTile.Type != ElementType.None)
                    {
                        // Tile exists in both states — interpolate
                        visual.Position = new Vector2(
                            tile.Position.X + (nextTile.Position.X - tile.Position.X) * alpha,
                            tile.Position.Y + (nextTile.Position.Y - tile.Position.Y) * alpha
                        );
                    }
                    else
                    {
                        // Tile doesn't exist in next state (will be consumed/destroyed)
                        // Use current position — the destroy animation will take over
                        visual.Position = tile.Position;
                    }
                    visual.GridPosition = new Position(x, y);
                }
                else
                {
                    // Tile exists in game state but not in visual state - add it.
                    _tiles[tile.Id] = new TileVisual
                    {
                        Id = tile.Id,
                        Position = tile.Position,
                        Scale = Vector2.One,
                        Alpha = 1f,
                        IsVisible = true,
                        TileType = tile.Type,
                        CurrentStage = tile.Stage,
                        GridPosition = new Position(x, y)
                    };
                }
            }
        }

        // Remove tiles that no longer exist in game state (O(N) via HashSet lookup)
        // Skip tiles being animated (e.g. destroy animation) - they'll be removed
        // by the Player's RemoveTileCommand when the animation completes.
        _tilesToRemove.Clear();
        foreach (var kvp in _tiles)
        {
            if (!_aliveTileIds.Contains(kvp.Key) && !kvp.Value.IsBeingAnimated)
                _tilesToRemove.Add(kvp.Key);
        }
        foreach (var id in _tilesToRemove)
        {
            _tiles.Remove(id);
        }
    }

    /// <summary>
    /// Find a tile in nextState by ID. Fast path: check same (x,y) and neighbors first
    /// (tile usually stays at same position or moves down by 1). Falls back to full scan.
    /// </summary>
    private static Tile FindTileInNextState(in GameState state, int tileId, int hintX, int hintY)
    {
        // Fast path 1: same position (most common — tile didn't move this tick)
        var t = state.GetTile(hintX, hintY);
        if (t.Id == tileId) return t;

        // Fast path 2: one row below (tile fell by gravity)
        if (hintY + 1 < state.Height)
        {
            t = state.GetTile(hintX, hintY + 1);
            if (t.Id == tileId) return t;
        }

        // Fast path 3: diagonal neighbors (side-slide fill)
        if (hintY + 1 < state.Height)
        {
            if (hintX - 1 >= 0)
            {
                t = state.GetTile(hintX - 1, hintY + 1);
                if (t.Id == tileId) return t;
            }
            if (hintX + 1 < state.Width)
            {
                t = state.GetTile(hintX + 1, hintY + 1);
                if (t.Id == tileId) return t;
            }
        }

        // Slow fallback: full scan (rare — tile teleported or was shuffled)
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                t = state.GetTile(x, y);
                if (t.Id == tileId) return t;
            }
        }
        return default; // Type = None — tile was destroyed in next state
    }

    /// <summary>
    /// Add a new tile visual (for spawned tiles).
    /// </summary>
    public void AddTile(int tileId, ElementType type, Position gridPos, Vector2 spawnPos)
    {
        _tiles[tileId] = new TileVisual
        {
            Id = tileId,
            Position = spawnPos,
            Scale = Vector2.One,
            Alpha = 1f,
            IsVisible = true,
            TileType = type,
            GridPosition = gridPos
        };
    }

    /// <summary>
    /// Remove a tile visual.
    /// </summary>
    public void RemoveTile(int tileId)
    {
        _tiles.Remove(tileId);
    }

    /// <summary>
    /// Add a new projectile visual.
    /// </summary>
    public void AddProjectile(int projectileId, Vector2 position,
        ProjectileType type = ProjectileType.Ufo, byte colorIndex = 0)
    {
        _projectiles[projectileId] = new ProjectileVisual
        {
            Id = projectileId,
            Position = position,
            IsVisible = true,
            Type = type,
            ColorIndex = colorIndex
        };
    }

    /// <summary>
    /// Remove a projectile visual.
    /// </summary>
    public void RemoveProjectile(int projectileId)
    {
        _projectiles.Remove(projectileId);
    }

    /// <summary>
    /// Add a new obstacle visual.
    /// </summary>
    public void AddObstacle(Position pos, ObstacleType type, byte stage, byte state = 0)
    {
        _obstacles[pos] = new ObstacleVisual
        {
            GridPosition = pos,
            Type = type,
            CurrentStage = stage,
            State = state,
        };
    }

    /// <summary>
    /// Remove an obstacle visual.
    /// </summary>
    public void RemoveObstacle(Position pos)
    {
        _obstacles.Remove(pos);
    }

    /// <summary>
    /// Get obstacle visual by position. Returns null if not found.
    /// </summary>
    public ObstacleVisual? GetObstacle(Position pos)
    {
        return _obstacles.TryGetValue(pos, out var obs) ? obs : null;
    }

    /// <summary>
    /// Add a new ground visual.
    /// </summary>
    public void AddGround(Position pos, GroundType type, byte health)
    {
        _grounds[pos] = new GroundVisual
        {
            GridPosition = pos,
            Type = type,
            CurrentHealth = health,
        };
    }

    /// <summary>
    /// Remove a ground visual.
    /// </summary>
    public void RemoveGround(Position pos)
    {
        _grounds.Remove(pos);
    }

    /// <summary>
    /// Get ground visual by position. Returns null if not found.
    /// </summary>
    public GroundVisual? GetGround(Position pos)
    {
        return _grounds.TryGetValue(pos, out var gnd) ? gnd : null;
    }

    /// <summary>
    /// Add a new cover visual.
    /// </summary>
    public void AddCover(Position pos, CoverType type, byte health)
    {
        _covers[pos] = new CoverVisual
        {
            GridPosition = pos,
            Type = type,
            CurrentHealth = health,
        };
    }

    /// <summary>
    /// Remove a cover visual.
    /// </summary>
    public void RemoveCover(Position pos)
    {
        _covers.Remove(pos);
    }

    /// <summary>
    /// Get cover visual by position. Returns null if not found.
    /// </summary>
    public CoverVisual? GetCover(Position pos)
    {
        return _covers.TryGetValue(pos, out var cv) ? cv : null;
    }

    /// <inheritdoc />
    public void SetTilePosition(int tileId, Vector2 position)
    {
        if (_tiles.TryGetValue(tileId, out var tile))
        {
            tile.Position = position;
        }
    }

    /// <inheritdoc />
    public void SetTileScale(int tileId, Vector2 scale)
    {
        if (_tiles.TryGetValue(tileId, out var tile))
        {
            tile.Scale = scale;
        }
    }

    /// <inheritdoc />
    public void SetTileAlpha(int tileId, float alpha)
    {
        if (_tiles.TryGetValue(tileId, out var tile))
        {
            tile.Alpha = alpha;
        }
    }

    /// <inheritdoc />
    public void SetTileVisible(int tileId, bool visible)
    {
        if (_tiles.TryGetValue(tileId, out var tile))
        {
            tile.IsVisible = visible;
        }
    }

    /// <inheritdoc />
    public void SetTileRotation(int tileId, float rotation)
    {
        if (_tiles.TryGetValue(tileId, out var tile))
        {
            tile.Rotation = rotation;
        }
    }

    /// <inheritdoc />
    public void SetProjectilePosition(int projectileId, Vector2 position)
    {
        if (_projectiles.TryGetValue(projectileId, out var proj))
        {
            proj.Position = position;
        }
    }

    /// <inheritdoc />
    public void SetProjectileVisible(int projectileId, bool visible)
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
    public TileVisual? GetTile(int tileId)
    {
        return _tiles.TryGetValue(tileId, out var tile) ? tile : null;
    }

    /// <summary>
    /// Get projectile visual by ID.
    /// </summary>
    public ProjectileVisual? GetProjectile(int projectileId)
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
    public int Id { get; init; }

    /// <summary>Current visual position (may differ from grid position during animation).</summary>
    public Vector2 Position { get; set; }

    /// <summary>Current scale (for animations).</summary>
    public Vector2 Scale { get; set; } = Vector2.One;

    /// <summary>Current alpha (for fade animations).</summary>
    public float Alpha { get; set; } = 1f;

    /// <summary>Current rotation angle in degrees (for spin animations).</summary>
    public float Rotation { get; set; }

    /// <summary>Whether the tile is visible.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Type of tile (color or bomb).</summary>
    public ElementType TileType { get; set; }

    /// <summary>Current stage/HP. Updated by Player on DamageTileCommand start.</summary>
    public byte CurrentStage { get; set; } = 1;

    /// <summary>
    /// Damage animation progress (0→1). Driven by Player during DamageTileCommand.
    /// Reset to 0 at damage start, reaches 1 at end. View uses this for hit reaction.
    /// </summary>
    public float DamageProgress { get; set; }

    /// <summary>Grid position.</summary>
    public Position GridPosition { get; set; }

    /// <summary>
    /// Reference count of animations controlling this tile's position.
    /// When > 0, physics sync should not overwrite the position.
    /// Using ref count instead of bool to handle overlapping animations correctly.
    /// </summary>
    public int AnimationRefCount { get; set; }

    /// <summary>
    /// Whether the tile's position is being controlled by any animation.
    /// </summary>
    public bool IsBeingAnimated => AnimationRefCount > 0;

    /// <summary>
    /// UFO flight progress (0→1). Negative means not in flight.
    /// View uses this + Duration to compute 3D rotation/scale/tilt.
    /// </summary>
    public float UfoFlightProgress { get; set; } = -1f;

    /// <summary>
    /// Total UFO flight duration in seconds (for View to derive elapsed time).
    /// </summary>
    public float UfoFlightDuration { get; set; }

    /// <summary>
    /// Set to true by Player when a retarget replaces the active flight segment.
    /// View reads and clears this flag to trigger arc-blend smoothing.
    /// </summary>
    public bool UfoRetargetFlag { get; set; }

    /// <summary>
    /// Increment animation reference count (call when animation starts).
    /// </summary>
    public void AddAnimationRef() => AnimationRefCount++;

    /// <summary>
    /// Decrement animation reference count (call when animation completes).
    /// </summary>
    public void ReleaseAnimationRef()
    {
        AnimationRefCount--;
        if (AnimationRefCount < 0)
            AnimationRefCount = 0; // Safety: never go negative
    }
}

/// <summary>
/// Visual representation of a projectile.
/// </summary>
public sealed class ProjectileVisual
{
    /// <summary>Projectile ID.</summary>
    public int Id { get; init; }

    /// <summary>Current visual position.</summary>
    public Vector2 Position { get; set; }

    /// <summary>Whether the projectile is visible.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Rotation angle for visual effects.</summary>
    public float Rotation { get; set; }

    /// <summary>Projectile type (determines visual appearance).</summary>
    public ProjectileType Type { get; init; }

    /// <summary>Color index for multi-color projectiles (0-5 maps to Item1-Item6).</summary>
    public byte ColorIndex { get; init; }
}

/// <summary>
/// Visual representation of an obstacle.
/// </summary>
public sealed class ObstacleVisual
{
    /// <summary>Grid position (immutable — obstacles don't move).</summary>
    public Position GridPosition { get; init; }

    /// <summary>Type of obstacle.</summary>
    public ObstacleType Type { get; init; }

    /// <summary>Current stage (HP). Updated by Player on DamageObstacleCommand start.</summary>
    public byte CurrentStage { get; set; }

    /// <summary>
    /// Obstacle-specific state (e.g., ColorBox: color variant as ElementType,
    /// PotionBottle: remaining sub-bottle bitmask). Mutable for PotionBottle updates.
    /// </summary>
    public byte State { get; set; }

    /// <summary>
    /// Damage animation progress (0→1). Driven by Player during DamageObstacleCommand.
    /// Reset to 0 at damage start, reaches 1 at end. View uses this for hit reaction.
    /// </summary>
    public float DamageProgress { get; set; }

    /// <summary>
    /// Death animation progress (0→1). Driven by Player during DestroyObstacleCommand.
    /// </summary>
    public float DeathProgress { get; set; }

    /// <summary>Whether the destroy animation is playing.</summary>
    public bool IsDestroying { get; set; }

    /// <summary>Whether the obstacle is visible.</summary>
    public bool IsVisible { get; set; } = true;
}

/// <summary>
/// Visual representation of a ground element (Ice, Grass, Leaves).
/// </summary>
public sealed class GroundVisual
{
    /// <summary>Grid position (immutable — ground doesn't move).</summary>
    public Position GridPosition { get; init; }

    /// <summary>Type of ground.</summary>
    public GroundType Type { get; init; }

    /// <summary>Current health. Updated by Player on DamageGroundCommand start.</summary>
    public byte CurrentHealth { get; set; }

    /// <summary>
    /// Damage animation progress (0→1). Driven by Player during DamageGroundCommand.
    /// Reset to 0 at damage start, reaches 1 at end. View uses this for hit reaction.
    /// </summary>
    public float DamageProgress { get; set; }

    /// <summary>
    /// Destroy animation progress (0→1). Driven by Player during DestroyGroundCommand.
    /// </summary>
    public float DestroyProgress { get; set; }

    /// <summary>Whether the destroy animation is playing.</summary>
    public bool IsDestroying { get; set; }

    /// <summary>Whether the ground is visible.</summary>
    public bool IsVisible { get; set; } = true;
}

/// <summary>
/// Visual representation of a cover element (Cage, Chain, Bubble, Honey, Frost).
/// </summary>
public sealed class CoverVisual
{
    /// <summary>Grid position (immutable — static covers don't move).</summary>
    public Position GridPosition { get; init; }

    /// <summary>Type of cover.</summary>
    public CoverType Type { get; init; }

    /// <summary>Current health. Updated by Player on damage.</summary>
    public byte CurrentHealth { get; set; }

    /// <summary>
    /// Destroy animation progress (0→1). Driven by Player during DestroyCoverCommand.
    /// </summary>
    public float DestroyProgress { get; set; }

    /// <summary>Whether the destroy animation is playing.</summary>
    public bool IsDestroying { get; set; }

    /// <summary>Whether the cover is visible.</summary>
    public bool IsVisible { get; set; } = true;
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
