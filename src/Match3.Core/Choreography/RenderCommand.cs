using System.Numerics;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Choreography;

/// <summary>
/// Base class for all render commands.
/// Commands are executed by Player to update VisualState.
/// </summary>
public abstract record RenderCommand
{
    /// <summary>Start time in the timeline.</summary>
    public float StartTime { get; init; }

    /// <summary>Duration of the command (0 for instant commands).</summary>
    public float Duration { get; init; }

    /// <summary>Priority for command ordering (higher = executed later when same StartTime).</summary>
    public int Priority { get; init; }

    /// <summary>End time of the command.</summary>
    public float EndTime => StartTime + Duration;
}

#region Tile Commands

/// <summary>
/// Spawn a new tile in the visual state.
/// </summary>
public sealed record SpawnTileCommand : RenderCommand
{
    /// <summary>Unique identifier of the tile.</summary>
    public long TileId { get; init; }

    /// <summary>Type of the tile.</summary>
    public TileType Type { get; init; }

    /// <summary>Bomb type (if any).</summary>
    public BombType Bomb { get; init; }

    /// <summary>Grid position of the tile.</summary>
    public Position GridPos { get; init; }

    /// <summary>Initial spawn position (visual).</summary>
    public Vector2 SpawnPos { get; init; }
}

/// <summary>
/// Move a tile from one position to another.
/// </summary>
public sealed record MoveTileCommand : RenderCommand
{
    /// <summary>Unique identifier of the tile.</summary>
    public long TileId { get; init; }

    /// <summary>Starting position.</summary>
    public Vector2 From { get; init; }

    /// <summary>Target position.</summary>
    public Vector2 To { get; init; }

    /// <summary>Easing function for interpolation.</summary>
    public EasingType Easing { get; init; } = EasingType.OutCubic;
}

/// <summary>
/// Destroy a tile with animation.
/// </summary>
public sealed record DestroyTileCommand : RenderCommand
{
    /// <summary>Unique identifier of the tile.</summary>
    public long TileId { get; init; }

    /// <summary>Position where destruction occurs.</summary>
    public Vector2 Position { get; init; }

    /// <summary>Reason for destruction (affects visual effect).</summary>
    public DestroyReason Reason { get; init; }
}

/// <summary>
/// Swap two tiles (mutual position exchange).
/// </summary>
public sealed record SwapTilesCommand : RenderCommand
{
    /// <summary>First tile identifier.</summary>
    public long TileAId { get; init; }

    /// <summary>Second tile identifier.</summary>
    public long TileBId { get; init; }

    /// <summary>Position of first tile.</summary>
    public Vector2 PosA { get; init; }

    /// <summary>Position of second tile.</summary>
    public Vector2 PosB { get; init; }

    /// <summary>Whether this is a revert (invalid swap).</summary>
    public bool IsRevert { get; init; }

    /// <summary>Easing function for interpolation.</summary>
    public EasingType Easing { get; init; } = EasingType.OutCubic;
}

/// <summary>
/// Remove a tile from visual state (after destruction animation completes).
/// </summary>
public sealed record RemoveTileCommand : RenderCommand
{
    /// <summary>Unique identifier of the tile to remove.</summary>
    public long TileId { get; init; }
}

/// <summary>
/// Update a tile's bomb type (when bomb is created from match).
/// </summary>
public sealed record UpdateTileBombCommand : RenderCommand
{
    /// <summary>Unique identifier of the tile.</summary>
    public long TileId { get; init; }

    /// <summary>Position of the tile.</summary>
    public Position Position { get; init; }

    /// <summary>New bomb type.</summary>
    public BombType BombType { get; init; }
}

#endregion

#region Effect Commands

/// <summary>
/// Show a visual effect at a position.
/// </summary>
public sealed record ShowEffectCommand : RenderCommand
{
    /// <summary>Type of effect to show.</summary>
    public string EffectType { get; init; } = string.Empty;

    /// <summary>Position to show the effect.</summary>
    public Vector2 Position { get; init; }
}

/// <summary>
/// Show match highlight effect on matched tiles.
/// </summary>
public sealed record ShowMatchHighlightCommand : RenderCommand
{
    /// <summary>Positions of matched tiles.</summary>
    public Position[] Positions { get; init; } = [];
}

#endregion

#region Projectile Commands

/// <summary>
/// Spawn a projectile in the visual state.
/// </summary>
public sealed record SpawnProjectileCommand : RenderCommand
{
    /// <summary>Unique identifier of the projectile.</summary>
    public long ProjectileId { get; init; }

    /// <summary>Launch origin position.</summary>
    public Vector2 Origin { get; init; }

    /// <summary>Arc height for launch animation.</summary>
    public float ArcHeight { get; init; } = 1.5f;

    /// <summary>Type of projectile.</summary>
    public ProjectileType Type { get; init; }
}

/// <summary>
/// Move a projectile from one position to another.
/// </summary>
public sealed record MoveProjectileCommand : RenderCommand
{
    /// <summary>Unique identifier of the projectile.</summary>
    public long ProjectileId { get; init; }

    /// <summary>Starting position.</summary>
    public Vector2 From { get; init; }

    /// <summary>Target position.</summary>
    public Vector2 To { get; init; }
}

/// <summary>
/// Projectile impact effect.
/// </summary>
public sealed record ImpactProjectileCommand : RenderCommand
{
    /// <summary>Unique identifier of the projectile.</summary>
    public long ProjectileId { get; init; }

    /// <summary>Impact position.</summary>
    public Vector2 Position { get; init; }

    /// <summary>Effect type for impact.</summary>
    public string EffectType { get; init; } = "projectile_explosion";
}

/// <summary>
/// Remove a projectile from visual state.
/// </summary>
public sealed record RemoveProjectileCommand : RenderCommand
{
    /// <summary>Unique identifier of the projectile to remove.</summary>
    public long ProjectileId { get; init; }
}

#endregion

#region Layer Commands

/// <summary>
/// Destroy a cover layer at a position.
/// </summary>
public sealed record DestroyCoverCommand : RenderCommand
{
    /// <summary>Grid position.</summary>
    public Position GridPos { get; init; }

    /// <summary>Cover type being destroyed.</summary>
    public CoverType CoverType { get; init; }
}

/// <summary>
/// Destroy a ground layer at a position.
/// </summary>
public sealed record DestroyGroundCommand : RenderCommand
{
    /// <summary>Grid position.</summary>
    public Position GridPos { get; init; }

    /// <summary>Ground type being destroyed.</summary>
    public GroundType GroundType { get; init; }
}

#endregion
