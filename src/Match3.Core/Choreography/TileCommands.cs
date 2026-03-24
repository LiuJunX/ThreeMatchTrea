using System.Numerics;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Choreography;

/// <summary>
/// Spawn a new tile in the visual state.
/// </summary>
public sealed record SpawnTileCommand : RenderCommand
{
    /// <summary>Unique identifier of the tile.</summary>
    public int TileId { get; init; }

    /// <summary>Type of the tile.</summary>
    public ElementType Type { get; init; }

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
    public int TileId { get; init; }

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
    public int TileId { get; init; }

    /// <summary>Position where destruction occurs.</summary>
    public Vector2 Position { get; init; }

    /// <summary>Source of elimination (affects visual effect).</summary>
    public ElimSource Reason { get; init; }
}

/// <summary>
/// Swap two tiles (mutual position exchange).
/// </summary>
public sealed record SwapTilesCommand : RenderCommand
{
    /// <summary>First tile identifier.</summary>
    public int TileAId { get; init; }

    /// <summary>Second tile identifier.</summary>
    public int TileBId { get; init; }

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
/// Scale a tile from one size to another.
/// </summary>
public sealed record ScaleTileCommand : RenderCommand
{
    /// <summary>Unique identifier of the tile.</summary>
    public int TileId { get; init; }

    /// <summary>Starting scale.</summary>
    public Vector2 FromScale { get; init; }

    /// <summary>Target scale.</summary>
    public Vector2 ToScale { get; init; }

    /// <summary>Easing function for interpolation.</summary>
    public EasingType Easing { get; init; } = EasingType.OutCubic;
}

/// <summary>
/// Rotate a tile from one angle to another (degrees).
/// </summary>
public sealed record RotateTileCommand : RenderCommand
{
    /// <summary>Unique identifier of the tile.</summary>
    public int TileId { get; init; }

    /// <summary>Starting angle in degrees.</summary>
    public float FromAngle { get; init; }

    /// <summary>Target angle in degrees.</summary>
    public float ToAngle { get; init; }

    /// <summary>Easing function for interpolation.</summary>
    public EasingType Easing { get; init; } = EasingType.Linear;
}

/// <summary>
/// Continuous scale oscillation on a tile (tremor / shake effect).
/// Player drives scale = 1 + Amplitude * sin(elapsed * Frequency * 2π).
/// </summary>
public sealed record ShakeTileCommand : RenderCommand
{
    /// <summary>Unique identifier of the tile.</summary>
    public int TileId { get; init; }

    /// <summary>Peak deviation from scale 1.0 (e.g. 0.08 = oscillates between 0.92 and 1.08).</summary>
    public float Amplitude { get; init; }

    /// <summary>Oscillations per second.</summary>
    public float Frequency { get; init; }
}

/// <summary>
/// Damage a multi-stage tile (stage decreased but not zero).
/// Player drives DamageProgress 0→1 on the TileVisual.
/// Analogous to <see cref="DamageObstacleCommand"/> for obstacles.
/// </summary>
public sealed record DamageTileCommand : RenderCommand
{
    /// <summary>Unique identifier of the tile.</summary>
    public int TileId { get; init; }

    /// <summary>Grid position of the tile.</summary>
    public Position GridPos { get; init; }

    /// <summary>Type of the tile.</summary>
    public ElementType TileType { get; init; }

    /// <summary>Stage AFTER this damage.</summary>
    public byte NewStage { get; init; }
}

/// <summary>
/// Remove a tile from visual state (after destruction animation completes).
/// </summary>
public sealed record RemoveTileCommand : RenderCommand
{
    /// <summary>Unique identifier of the tile to remove.</summary>
    public int TileId { get; init; }
}

/// <summary>
/// Update a tile's type (when tile is shuffled).
/// </summary>
public sealed record UpdateTileTypeCommand : RenderCommand
{
    /// <summary>Unique identifier of the tile.</summary>
    public int TileId { get; init; }

    /// <summary>Position of the tile.</summary>
    public Position Position { get; init; }

    /// <summary>New tile type.</summary>
    public ElementType TileType { get; init; }
}
