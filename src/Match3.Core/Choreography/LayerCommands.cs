using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Choreography;

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
/// Remove a cover visual from VisualState after death animation completes.
/// Instant command (Duration=0), scheduled after DestroyCoverCommand ends.
/// </summary>
public sealed record RemoveCoverCommand : RenderCommand
{
    /// <summary>Grid position of the cover to remove.</summary>
    public Position GridPos { get; init; }
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
