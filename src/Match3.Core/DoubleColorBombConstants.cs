namespace Match3.Core;

/// <summary>
/// Shared timing constants for double ColorBomb combo wave destruction.
/// Referenced by both PowerUpHandler (simulation) and ChoreographyConfig (visual).
/// </summary>
public static class DoubleColorBombConstants
{
    /// <summary>Base interval per Chebyshev distance ring during the wipe wave (seconds).</summary>
    public const float WipeInterval = 0.04f;

    /// <summary>Acceleration multiplier for wipe wave (each ring faster than the last).</summary>
    public const float WipeAcceleration = 0.85f;
}
