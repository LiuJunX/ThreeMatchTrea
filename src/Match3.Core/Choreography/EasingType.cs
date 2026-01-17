namespace Match3.Core.Choreography;

/// <summary>
/// Easing functions for animation interpolation.
/// </summary>
public enum EasingType
{
    /// <summary>Linear interpolation (no easing).</summary>
    Linear,

    /// <summary>Quadratic ease-out (decelerates).</summary>
    OutQuadratic,

    /// <summary>Cubic ease-out (decelerates more smoothly).</summary>
    OutCubic,

    /// <summary>Cubic ease-in-out (accelerates then decelerates).</summary>
    InOutCubic,

    /// <summary>Bounce effect at the end.</summary>
    OutBounce
}
