using Match3.Core.Models.Enums;

namespace Match3.Core.Models.Grid;

/// <summary>
/// Provides rule lookups for ground types.
/// </summary>
public static class GroundRules
{
    /// <summary>
    /// Returns the default health for a ground type.
    /// </summary>
    public static byte GetDefaultHealth(GroundType type) => type switch
    {
        GroundType.None => 0,
        GroundType.Ice => 1,
        GroundType.Jelly => 2,   // Jelly needs 2 hits by default
        GroundType.Honey => 1,
        _ => 1
    };
}
