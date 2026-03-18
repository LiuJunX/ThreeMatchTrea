using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Choreography;

/// <summary>
/// Spawn a ground visual (level start or death-effect spread like Bush → Grass).
/// Instant (Duration=0) for initial placement, or timed for spawn animation.
/// </summary>
public sealed record SpawnGroundCommand : RenderCommand
{
    /// <summary>Grid position of the ground.</summary>
    public Position GridPos { get; init; }

    /// <summary>Type of the ground.</summary>
    public GroundType GroundType { get; init; }

    /// <summary>Initial health.</summary>
    public byte Health { get; init; }
}

/// <summary>
/// Damage a ground element (health decreased but not zero).
/// Player drives DamageProgress 0→1 on the GroundVisual.
/// </summary>
public sealed record DamageGroundCommand : RenderCommand
{
    /// <summary>Grid position of the ground.</summary>
    public Position GridPos { get; init; }

    /// <summary>Type of the ground.</summary>
    public GroundType GroundType { get; init; }

    /// <summary>Health AFTER this damage.</summary>
    public byte NewHealth { get; init; }
}

/// <summary>
/// Remove a ground visual from VisualState after death animation completes.
/// Instant command (Duration=0), scheduled after DestroyGroundCommand ends.
/// </summary>
public sealed record RemoveGroundCommand : RenderCommand
{
    /// <summary>Grid position of the ground to remove.</summary>
    public Position GridPos { get; init; }
}
