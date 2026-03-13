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
/// Destroy a ground layer at a position.
/// </summary>
public sealed record DestroyGroundCommand : RenderCommand
{
    /// <summary>Grid position.</summary>
    public Position GridPos { get; init; }

    /// <summary>Ground type being destroyed.</summary>
    public GroundType GroundType { get; init; }
}
