using System;

namespace Match3.Core.Systems.Projectiles.Targeting.Capacity;

/// <summary>
/// Determines how to combine hit capacities when multiple layers are penetrated.
/// </summary>
public interface ICapacityRule
{
    /// <summary>
    /// Calculate the combined meaningful-hit capacity for a cell.
    /// </summary>
    byte Calculate(ReadOnlySpan<HitLayer> hitLayers);
}
