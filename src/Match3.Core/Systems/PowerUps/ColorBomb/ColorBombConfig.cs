namespace Match3.Core.Systems.PowerUps.ColorBomb;

/// <summary>
/// Configuration for the ColorBomb session system.
/// </summary>
public sealed class ColorBombConfig
{
    /// <summary>Interval between consecutive beam launches (seconds).</summary>
    public float BeamInterval { get; set; } = 0.12f;

    /// <summary>Beam flight speed (grid units per second).</summary>
    public float BeamSpeed { get; set; } = 24f;

    /// <summary>Minimum beam flight duration to prevent near-instant hits.</summary>
    public float MinFlightDuration { get; set; } = 0.08f;

    /// <summary>Maximum number of re-scans after all beams in a batch are fired.</summary>
    public int MaxReScans { get; set; } = 3;
}
