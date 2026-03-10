using System;
using System.Numerics;

namespace Match3.Core;

/// <summary>
/// Shared timing constants for UFO (propeller bomb) flight.
/// Referenced by both UfoProjectile (simulation) and ChoreographyConfig (visual).
/// </summary>
public static class UfoConstants
{
    /// <summary>Fixed overhead for spin-up + launch + landing (seconds).</summary>
    public const float LaunchOverhead = 0.6f;

    /// <summary>Flight speed during cruise phase (grid units per second).</summary>
    public const float FlightSpeed = 3.9f;

    /// <summary>
    /// Lock-in time before impact (seconds). Once remaining flight time drops
    /// below this threshold, the UFO commits to its current target and will not
    /// retarget — even if the target cell becomes empty. This prevents jarring
    /// last-moment redirects and gives the visual layer enough frames (~18 at 60fps)
    /// to animate a smooth landing.
    /// </summary>
    public const float LockInTime = 0.3f;

    /// <summary>Base momentum distance when retargeting perpendicular (grid units).</summary>
    public const float MomentumBase = 0.8f;

    /// <summary>Extra momentum distance added when retargeting in opposite direction.</summary>
    public const float MomentumReverseBonus = 0.7f;

    /// <summary>
    /// Computes how far the Bezier control point extends along the old flight direction.
    /// Same direction → small (nearly straight); opposite → large (wide U-turn curve).
    /// </summary>
    public static float MomentumStrength(Vector2 oldDir, Vector2 newDir)
    {
        float oldLen = oldDir.Length();
        float newLen = newDir.Length();
        if (oldLen < 1e-4f || newLen < 1e-4f) return 0f;

        float dot = Vector2.Dot(oldDir / oldLen, newDir / newLen); // [-1, 1]
        // dot=1 (same dir) → 0 (no curve needed)
        // dot=0 (perpendicular) → MomentumBase
        // dot=-1 (opposite) → MomentumBase + MomentumReverseBonus
        return MomentumBase * (1f - dot) * 0.5f + MomentumReverseBonus * Math.Max(0f, -dot);
    }
}
