using System;

namespace Match3.Core.Systems.Projectiles.Targeting;

/// <summary>
/// Multi-dimensional score for UFO target evaluation.
/// Purely temporary — created and discarded within a single SelectTarget call.
/// Comparison: Tier > Urgency > TotalValue.
/// </summary>
public struct CellScore : IComparable<CellScore>
{
    /// <summary>Priority tier: 0=fallback, 1=normal, 2=cover, 3=objective, 4=urgent.</summary>
    public byte Tier;

    /// <summary>Urgency [0-255]: 255=last hit (HP=1), 0=none.</summary>
    public byte Urgency;

    /// <summary>Accumulated base value from penetrated layers. ushort to prevent overflow.</summary>
    public ushort BaseValue;

    /// <summary>Objective bonus [0-255].</summary>
    public byte TargetBonus;

    /// <summary>Indirect value [-128..127]. Reserved for future (paint/path). First version=0.</summary>
    public sbyte Synergy;

    public int TotalValue => BaseValue + TargetBonus + Synergy;

    public int CompareTo(CellScore other)
    {
        if (Tier != other.Tier) return Tier.CompareTo(other.Tier);
        if (Urgency != other.Urgency) return Urgency.CompareTo(other.Urgency);
        return TotalValue.CompareTo(other.TotalValue);
    }
}
