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

    /// <summary>Duration for drop acceptance delay after match destroy (cell lock).</summary>
    public float DropDelay { get; set; } = 0.12f;
}
