using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;

namespace Match3.Core.Systems.Matching.Generation;

/// <summary>
/// Determines the bomb type based on detected shape.
/// Single Responsibility: Type decision only.
/// </summary>
public interface IBombTypeSelector
{
    /// <summary>
    /// Select bomb type for a detected shape.
    /// </summary>
    /// <param name="shape">The detected shape with cells.</param>
    /// <returns>The bomb type to generate, or None for simple match.</returns>
    BombType SelectBombType(DetectedShape shape);

    /// <summary>
    /// Get the weight/priority of a shape for partition optimization.
    /// </summary>
    int GetWeight(DetectedShape shape);
}
