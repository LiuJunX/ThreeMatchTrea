namespace Match3.Core.Systems.PowerUps;

/// <summary>
/// Timing configuration for explosion wave propagation.
/// Collects magic numbers previously scattered across
/// <see cref="ExplosionSystem"/> and <see cref="PowerUpHandler"/>.
/// </summary>
public sealed class ExplosionConfig
{
    /// <summary>Default wave interval for generic explosions (seconds).</summary>
    public float DefaultWaveInterval { get; set; } = 0.1f;

    /// <summary>Wave interval for rocket explosions (seconds). Rockets spread faster than area bombs.</summary>
    public float RocketWaveInterval { get; set; } = 0.04f;

    /// <summary>Acceleration multiplier for rocket explosion waves (each ring faster than the last).</summary>
    public float RocketAcceleration { get; set; } = 0.8f;

    /// <summary>Wave interval for area bomb explosions (seconds).</summary>
    public float AreaBombWaveInterval { get; set; } = 0.05f;

    /// <summary>Acceleration multiplier for area bomb explosion waves.</summary>
    public float AreaBombAcceleration { get; set; } = 0.85f;
}
