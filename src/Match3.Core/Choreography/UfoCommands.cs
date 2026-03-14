using System.Numerics;

namespace Match3.Core.Choreography;

/// <summary>
/// Launch a UFO tile: spin-up, takeoff towards camera, fly to target, land.
/// Player drives position + progress; View handles 3D rotation/scale/tilt.
/// </summary>
public sealed record UfoLaunchCommand : RenderCommand
{
    /// <summary>Tile ID of the UFO bomb.</summary>
    public int TileId { get; init; }

    /// <summary>Grid origin position.</summary>
    public Vector2 Origin { get; init; }

    /// <summary>Grid target position.</summary>
    public Vector2 Target { get; init; }

    /// <summary>Fraction of duration to stay at origin (spin-up phase). LaunchStayFraction for initial launch, 0 for retarget.</summary>
    public float StayFraction { get; init; } = UfoConstants.LaunchStayFraction;

    /// <summary>
    /// Optional quadratic Bezier control point for curved retarget paths.
    /// Null for initial launches (straight-line path).
    /// </summary>
    public Vector2? MomentumControl { get; init; }

    /// <summary>
    /// Cubic Bezier P1: pull toward diverge direction at takeoff.
    /// Null when diverge is disabled (short-range targets) or for retarget segments.
    /// </summary>
    public Vector2? DivergeControl { get; init; }

    /// <summary>
    /// Cubic Bezier P2: smooth approach toward target.
    /// Null when diverge is disabled or for retarget segments.
    /// </summary>
    public Vector2? ApproachControl { get; init; }
}

/// <summary>
/// Retarget an in-flight UFO to a new destination.
/// Player replaces the active UfoLaunchCommand with a new segment.
/// </summary>
public sealed record UfoRetargetCommand : RenderCommand
{
    /// <summary>Tile ID of the UFO bomb.</summary>
    public int TileId { get; init; }

    /// <summary>New target position.</summary>
    public Vector2 NewTarget { get; init; }

}
