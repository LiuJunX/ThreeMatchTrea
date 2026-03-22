using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Choreography;

/// <summary>
/// Spawn an obstacle visual (level start or dynamic creation like Bush spread).
/// Instant (Duration=0) for initial placement, or timed for spawn animation.
/// </summary>
public sealed record SpawnObstacleCommand : RenderCommand
{
    /// <summary>Grid position of the obstacle.</summary>
    public Position GridPos { get; init; }

    /// <summary>Type of the obstacle.</summary>
    public ObstacleType ObstacleType { get; init; }

    /// <summary>Initial stage (HP).</summary>
    public byte Stage { get; init; }

    /// <summary>Obstacle-specific state (e.g., ColorBox: color variant as ElementType).</summary>
    public byte State { get; init; }
}

/// <summary>
/// Damage an obstacle (stage decreased but not zero).
/// Player drives DamageProgress 0→1 on the ObstacleVisual.
/// </summary>
public sealed record DamageObstacleCommand : RenderCommand
{
    /// <summary>Grid position of the obstacle.</summary>
    public Position GridPos { get; init; }

    /// <summary>Type of the obstacle.</summary>
    public ObstacleType ObstacleType { get; init; }

    /// <summary>Stage AFTER this damage.</summary>
    public byte NewStage { get; init; }
}

/// <summary>
/// Destroy an obstacle (stage reached zero).
/// Player drives DeathProgress 0→1 on the ObstacleVisual.
/// </summary>
public sealed record DestroyObstacleCommand : RenderCommand
{
    /// <summary>Grid position of the obstacle.</summary>
    public Position GridPos { get; init; }

    /// <summary>Type of the destroyed obstacle.</summary>
    public ObstacleType ObstacleType { get; init; }
}

/// <summary>
/// Remove an obstacle visual from VisualState after death animation completes.
/// Instant command (Duration=0), scheduled after DestroyObstacleCommand ends.
/// </summary>
public sealed record RemoveObstacleCommand : RenderCommand
{
    /// <summary>Grid position of the obstacle to remove.</summary>
    public Position GridPos { get; init; }
}

/// <summary>
/// Activate a generator obstacle (e.g., Mailbox opens, product flies out, closes).
/// </summary>
public sealed record ActivateGeneratorCommand : RenderCommand
{
    /// <summary>Grid position of the generator.</summary>
    public Position GridPos { get; init; }

    /// <summary>Type of the generator obstacle.</summary>
    public ObstacleType ObstacleType { get; init; }

    /// <summary>Type of the spawned product.</summary>
    public ElementType ProductType { get; init; }

    /// <summary>Grid position where the product was spawned.</summary>
    public Position ProductPosition { get; init; }
}
