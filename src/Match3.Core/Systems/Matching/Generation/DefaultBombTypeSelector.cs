using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;

namespace Match3.Core.Systems.Matching.Generation;

/// <summary>
/// Default type selection based on shape's pre-calculated type and weight.
/// The actual type determination happens during shape detection.
/// </summary>
public sealed class DefaultBombTypeSelector : IBombTypeSelector
{
    /// <inheritdoc />
    public BombType SelectBombType(DetectedShape shape)
    {
        // Type is already determined during detection by ShapeDetector rules
        return shape.Type;
    }

    /// <inheritdoc />
    public int GetWeight(DetectedShape shape)
    {
        // Weight is pre-calculated during detection based on BombDefinitions
        return shape.Weight;
    }
}
