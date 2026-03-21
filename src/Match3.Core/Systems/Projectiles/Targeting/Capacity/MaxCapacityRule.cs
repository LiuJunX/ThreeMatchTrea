using System;

namespace Match3.Core.Systems.Projectiles.Targeting.Capacity;

/// <summary>
/// When multiple layers are penetrated, take the maximum capacity.
/// </summary>
public sealed class MaxCapacityRule : ICapacityRule
{
    public static readonly MaxCapacityRule Instance = new();

    public byte Calculate(ReadOnlySpan<HitLayer> hitLayers)
    {
        byte max = 0;
        foreach (ref readonly var layer in hitLayers)
        {
            if (layer.HitCapacity > max)
                max = layer.HitCapacity;
        }
        return max;
    }
}
