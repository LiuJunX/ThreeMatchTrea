namespace Match3.Core.Choreography;

/// <summary>
/// Timing configuration for choreography animations.
/// </summary>
public sealed class ChoreographyConfig
{
    /// <summary>Duration for tile movement animation.</summary>
    public float MoveDuration { get; set; } = 0.15f;

    /// <summary>Duration for tile destruction animation.</summary>
    public float DestroyDuration { get; set; } = 0.2f;

    /// <summary>Duration for match highlight before destruction.</summary>
    public float MatchHighlightDuration { get; set; } = 0.1f;

    /// <summary>Duration for swap animation.</summary>
    public float SwapDuration { get; set; } = 0.15f;

    /// <summary>Duration for merge-to-bomb animation.</summary>
    public float MergeDuration { get; set; } = 0.3f;

    /// <summary>Duration for projectile launch takeoff.</summary>
    public float ProjectileTakeoffDuration { get; set; } = 0.3f;

    /// <summary>Arc height for projectile launch.</summary>
    public float ProjectileArcHeight { get; set; } = 1.5f;

    /// <summary>Duration for bomb pop-in scale animation after merge.</summary>
    public float BombPopDuration { get; set; } = 0.15f;

    /// <summary>Duration for drop acceptance delay after match destroy (cell lock).</summary>
    public float DropDelay { get; set; } = 0.12f;

    /// <summary>Duration for drop acceptance delay after bomb destroy (cell lock). Longer than match to let the explosion settle.</summary>
    public float BombDropDelay { get; set; } = 0.3f;

    /// <summary>Delay per cell for rocket trail effect staggering.</summary>
    public float RocketTrailInterval { get; set; } = 0.015f;

    /// <summary>Base delay per Chebyshev-distance unit for color bomb beam propagation.</summary>
    public float ColorBombInterval { get; set; } = 0.04f;

    /// <summary>Acceleration factor for color bomb beam propagation (each step shorter than the last).</summary>
    public float ColorBombAccel { get; set; } = 0.85f;

    /// <summary>Duration for UFO takeoff animation (vertical rise).</summary>
    public float UfoTakeoffDuration { get; set; } = 0.25f;

    /// <summary>Height the UFO rises during takeoff (grid units).</summary>
    public float UfoTakeoffHeight { get; set; } = 1.5f;

    /// <summary>UFO flight speed (grid units per second).</summary>
    public float UfoFlightSpeed { get; set; } = 8f;
}
