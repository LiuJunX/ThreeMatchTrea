namespace Match3.Core.Choreography;

/// <summary>
/// Timing configuration for choreography animations.
/// </summary>
public sealed class ChoreographyConfig
{
    /// <summary>Duration for tile movement animation.</summary>
    public float MoveDuration { get; set; } = 0.15f;

    /// <summary>Duration for tile destruction animation.</summary>
    public float DestroyDuration { get; set; } = 0.15f;

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

    /// <summary>Delay per cell for rocket trail effect staggering.</summary>
    public float RocketTrailInterval { get; set; } = 0.015f;

    /// <summary>Flight speed for color bomb beam projectiles (grid units per second).</summary>
    public float ColorBombBeamSpeed { get; set; } = 24f;

    /// <summary>Duration of color bomb charge-up phase (spin + hop before beams launch).</summary>
    public float ColorBombChargeDuration { get; set; } = 0.25f;

    /// <summary>Hop offset (grid units, negative = up) during color bomb charge-up.</summary>
    public float ColorBombHopOffset { get; set; } = -0.3f;

    /// <summary>Scale multiplier during color bomb charge-up pulse.</summary>
    public float ColorBombChargeScale { get; set; } = 1.2f;

    /// <summary>Spin rate for color bomb: degrees per second during performance.</summary>
    public float ColorBombSpinRate { get; set; } = 4320f;

    /// <summary>Duration for color bomb shrink-to-nothing after beams land.</summary>
    public float ColorBombShrinkDuration { get; set; } = 0.15f;

    /// <summary>Maximum hold duration for color bomb tile between charge-up and batch destruction.
    /// Must exceed the longest possible session duration (targets × interval × re-scans + flight).</summary>
    public float ColorBombHoldDuration { get; set; } = 60f;

    /// <summary>Delay between consecutive color bomb beam launches (seconds). Beams fire
    /// farthest-first and all arrive simultaneously — this controls the visual rhythm.</summary>
    public float ColorBombBeamStagger { get; set; } = 0.08f;

    /// <summary>Minimum flight duration for the closest color bomb beam (seconds).
    /// Prevents near-instant teleportation of close-range beams.</summary>
    public float ColorBombMinFlightDuration { get; set; } = 0.08f;

    /// <summary>Scale oscillation amplitude for beam-hit shake (0.08 = ±8%).</summary>
    public float ColorBombHitShakeAmplitude { get; set; } = 0.08f;

    /// <summary>Oscillation frequency for beam-hit shake (Hz).</summary>
    public float ColorBombHitShakeFrequency { get; set; } = 4f;

    /// <summary>Fixed overhead for UFO launch sequence: spin-up + launch + landing (seconds).</summary>
    public float UfoLaunchOverhead { get; set; } = UfoConstants.LaunchOverhead;

    /// <summary>UFO flight speed during cruise phase (grid units per second).</summary>
    public float UfoFlightSpeed { get; set; } = UfoConstants.FlightSpeed;

    // --- Double Color Bomb Phases ---

    /// <summary>Duration for the converge phase (two bombs merge toward midpoint).</summary>
    public float DoubleColorConvergeDuration { get; set; } = 0.3f;

    /// <summary>Duration for the fusion flash at the merge point.</summary>
    public float DoubleColorFusionDuration { get; set; } = 0.2f;

    /// <summary>Base interval per Chebyshev distance ring during the wipe wave.</summary>
    public float DoubleColorWipeInterval { get; set; } = DoubleColorBombConstants.WipeInterval;

    /// <summary>Acceleration for wipe wave (each ring faster than the last).</summary>
    public float DoubleColorWipeAccel { get; set; } = DoubleColorBombConstants.WipeAcceleration;

    // --- Shuffle Animation ---

    /// <summary>Duration for gather phase (tiles shrink + rotate to disappear).</summary>
    public float ShuffleGatherDuration { get; set; } = 0.2f;

    /// <summary>Target scale during gather phase (0 = fully shrunk).</summary>
    public float ShuffleGatherScale { get; set; } = 0f;

    /// <summary>Rotation degrees during gather phase.</summary>
    public float ShuffleGatherRotation { get; set; } = 180f;

    /// <summary>Pause between gather and scatter phases (type swap happens here).</summary>
    public float ShuffleMidpointPause { get; set; } = 0.1f;

    /// <summary>Duration for scatter phase (tiles scale back up + rotate to appear).</summary>
    public float ShuffleScatterDuration { get; set; } = 0.25f;

    /// <summary>Maximum stagger delay per tile (seconds) based on distance from center.</summary>
    public float ShuffleStaggerMax { get; set; } = 0.08f;
}
