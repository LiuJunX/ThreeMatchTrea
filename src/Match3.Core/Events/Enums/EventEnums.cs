namespace Match3.Core.Events.Enums;

/// <summary>
/// Reason for tile movement.
/// </summary>
public enum MoveReason
{
    /// <summary>Tile fell due to gravity.</summary>
    Gravity,

    /// <summary>Tile was swapped by player.</summary>
    Swap,

    /// <summary>Swap was reverted (no match found).</summary>
    SwapRevert,

    /// <summary>Tile moved during animation interpolation.</summary>
    Animation,

    /// <summary>Tile slid diagonally.</summary>
    Slide
}

/// <summary>
/// Reason for tile destruction.
/// </summary>
public enum DestroyReason
{
    /// <summary>Tile was part of a match.</summary>
    Match,

    /// <summary>Tile was destroyed by bomb effect.</summary>
    BombEffect,

    /// <summary>Tile was hit by a projectile.</summary>
    Projectile,

    /// <summary>Tile was destroyed by chain reaction.</summary>
    ChainReaction
}

/// <summary>
/// Reason for score addition.
/// </summary>
public enum ScoreReason
{
    /// <summary>Score from matching tiles.</summary>
    Match,

    /// <summary>Score from bomb activation.</summary>
    Bomb,

    /// <summary>Score from combo multiplier.</summary>
    Combo,

    /// <summary>Score from projectile hit.</summary>
    Projectile
}

/// <summary>
/// Type of projectile.
/// </summary>
public enum ProjectileType
{
    /// <summary>UFO projectile that flies to target.</summary>
    Ufo,

    /// <summary>Homing missile projectile.</summary>
    HomingMissile,

    /// <summary>Color bomb beam — visual-only projectile flying from origin to target.</summary>
    ColorBombBeam
}

// Note: MatchShape is defined in Match3.Core.Models.Gameplay.MatchGroup
