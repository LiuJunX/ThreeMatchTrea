using Match3.Core.Models.Enums;

namespace Match3.Core.Systems.Projectiles.Targeting;

/// <summary>
/// Immutable configuration for UFO targeting. Shared across all simulations.
/// </summary>
public sealed class UfoTargetConfig
{
    /// <summary>Base value for cover layers (Cage, Chain, Honey, Frost).</summary>
    public ushort CoverBaseValue { get; init; } = 50;

    /// <summary>Base value for obstacle layers (Box, Bush, Safe, Cupboard).</summary>
    public ushort ObstacleBaseValue { get; init; } = 60;

    /// <summary>Extra value for Safe (UFO is one of few ways to hit it).</summary>
    public ushort SafeExtraValue { get; init; } = 30;

    /// <summary>Base value for normal color tiles (Item1-Item6).</summary>
    public ushort TileColorBaseValue { get; init; } = 10;

    /// <summary>Base value for bomb tiles.</summary>
    public ushort TileBombBaseValue { get; init; } = 5;

    /// <summary>Base value for collectible tiles (Bird, Pearl, Plate, Envelope).</summary>
    public ushort TileCollectibleBaseValue { get; init; } = 20;

    /// <summary>Base value for unmatchable tiles.</summary>
    public ushort TileUnmatchableBaseValue { get; init; } = 15;

    /// <summary>Base value for ground layers (Ice, Grass, Leaves).</summary>
    public ushort GroundBaseValue { get; init; } = 10;

    /// <summary>Objective target bonus.</summary>
    public byte TargetBonus { get; init; } = 100;

    /// <summary>Tier assigned to objective targets.</summary>
    public byte TargetTier { get; init; } = 3;

    /// <summary>Tier assigned to "last hit" / urgent targets.</summary>
    public byte UrgentTier { get; init; } = 4;

    /// <summary>
    /// Whether a given obstacle type can be directly hit by a UFO projectile.
    /// </summary>
    public static bool CanUfoHitObstacle(ObstacleType type) => type switch
    {
        ObstacleType.Box      => true,
        ObstacleType.Bush     => true,
        ObstacleType.Safe     => true,
        ObstacleType.Cupboard => true,
        _                     => false  // ColorBox, MagicHat, Curtain, Mailbox
    };

    /// <summary>
    /// Whether a given cover type blocks penetration to layers below.
    /// Bubble is dynamic and allows penetration.
    /// </summary>
    public static bool CoverBlocksPenetration(CoverType type) => type switch
    {
        CoverType.Bubble => false,
        CoverType.None   => false,
        _                => true   // Cage, Chain, Honey, Frost
    };

    public static readonly UfoTargetConfig Default = new();
}
