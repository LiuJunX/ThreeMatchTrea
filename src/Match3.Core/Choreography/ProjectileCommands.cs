using System.Numerics;
using Match3.Core.Events.Enums;

namespace Match3.Core.Choreography;

/// <summary>
/// Spawn a projectile in the visual state.
/// </summary>
public sealed record SpawnProjectileCommand : RenderCommand
{
    /// <summary>Unique identifier of the projectile.</summary>
    public int ProjectileId { get; init; }

    /// <summary>Launch origin position.</summary>
    public Vector2 Origin { get; init; }

    /// <summary>Arc height for launch animation.</summary>
    public float ArcHeight { get; init; } = 1.5f;

    /// <summary>Type of projectile.</summary>
    public ProjectileType Type { get; init; }

    /// <summary>Color index for multi-color projectiles (0-5 maps to Item1-Item6).</summary>
    public byte ColorIndex { get; init; }
}

/// <summary>
/// Move a projectile from one position to another.
/// </summary>
public sealed record MoveProjectileCommand : RenderCommand
{
    /// <summary>Unique identifier of the projectile.</summary>
    public int ProjectileId { get; init; }

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
    public int ProjectileId { get; init; }

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
    public int ProjectileId { get; init; }
}
