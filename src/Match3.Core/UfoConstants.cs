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
    public const float FlightSpeed = 3f;

    /// <summary>
    /// Lock-in time before impact (seconds). Once remaining flight time drops
    /// below this threshold, the UFO commits to its current target and will not
    /// retarget — even if the target cell becomes empty. This prevents jarring
    /// last-moment redirects and gives the visual layer enough frames (~18 at 60fps)
    /// to animate a smooth landing.
    /// </summary>
    public const float LockInTime = 0.3f;
}
