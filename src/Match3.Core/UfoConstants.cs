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
    public const float LaunchOverhead = 0.69f;

    /// <summary>Flight speed during cruise phase (grid units per second).</summary>
    public const float FlightSpeed = 3.315f;

    /// <summary>
    /// Lock-in time before impact (seconds). Set to 0 to allow retargeting at any
    /// moment — the visual layer uses a retarget-blend to smooth last-moment redirects
    /// instead of preventing them.
    /// </summary>
    public const float LockInTime = 0f;

    /// <summary>Base momentum distance when retargeting perpendicular (grid units).</summary>
    public const float MomentumBase = 0.8f;

    /// <summary>Extra momentum distance added when retargeting in opposite direction.</summary>
    public const float MomentumReverseBonus = 0.7f;

    /// <summary>Duration multiplier for retarget flight (slower = more "struggling" feel).</summary>
    public const float RetargetDurationMultiplier = 1.5f;

    // --- Diverge (random takeoff angle) ---

    /// <summary>Minimum diverge angle relative to target direction (degrees).</summary>
    public const float DivergeMinAngle = 30f;

    /// <summary>Maximum diverge angle relative to target direction (degrees).</summary>
    public const float DivergeMaxAngle = 75f;

    /// <summary>Cubic Bezier P1 distance from origin along diverge direction (grid units).</summary>
    public const float DivergeStrength = 3.0f;

    /// <summary>Cubic Bezier P2 distance from target along approach direction (grid units).</summary>
    public const float ApproachStrength = 1.5f;

    /// <summary>Minimum origin-to-target distance to enable diverge (grid units).</summary>
    public const float DivergeMinDistance = 2.0f;

    /// <summary>Stay fraction for initial launch (spin-up phase before XY movement begins).</summary>
    public const float LaunchStayFraction = 0.15f;

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

    /// <summary>
    /// Evaluate a cubic Bezier curve at parameter t.
    /// </summary>
    public static Vector2 CubicBezier(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        float inv = 1f - t;
        return inv * inv * inv * p0
             + 3f * inv * inv * t * p1
             + 3f * inv * t * t * p2
             + t * t * t * p3;
    }

    /// <summary>
    /// Compute the tangent (first derivative) of a cubic Bezier at parameter t.
    /// Used for retarget momentum direction calculation.
    /// </summary>
    public static Vector2 CubicBezierTangent(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        float inv = 1f - t;
        return 3f * inv * inv * (p1 - p0)
             + 6f * inv * t * (p2 - p1)
             + 3f * t * t * (p3 - p2);
    }

    /// <summary>
    /// Deterministic pseudo-random float in [0, 1) from an integer seed.
    /// Used for diverge angle generation without requiring a Random instance.
    /// </summary>
    public static float HashFloat(int seed)
    {
        // Wang hash
        seed = ((seed >> 16) ^ seed) * unchecked((int)0x45d9f3b);
        seed = ((seed >> 16) ^ seed) * unchecked((int)0x45d9f3b);
        seed = (seed >> 16) ^ seed;
        return (seed & 0x7FFFFFFF) / (float)0x7FFFFFFF;
    }
}
