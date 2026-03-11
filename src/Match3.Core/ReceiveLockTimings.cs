namespace Match3.Core;

/// <summary>
/// Timing constants for post-destruction Receive locks.
/// These prevent gravity/refill from filling destroyed cells too early,
/// giving the presentation layer time to animate destruction.
/// </summary>
public static class ReceiveLockTimings
{
    /// <summary>Simple match clear → Receive lock duration (matches DestroyDuration).</summary>
    public const float MatchClear = 0.15f;

    /// <summary>Merge source (tiles that merge into a bomb) → Receive lock duration (matches MergeDuration).</summary>
    public const float MergeSource = 0.3f;

    /// <summary>Bomb origin (newly spawned bomb) → Drop lock duration (matches BombPopDuration).</summary>
    public const float BombOriginDrop = 0.15f;

    /// <summary>Explosion wave clear → Receive lock duration.</summary>
    public const float ExplosionClear = 0.15f;

    /// <summary>Bomb activation (ClearBombAttribute) → Receive lock duration.</summary>
    public const float BombActivateClear = 0.15f;

    /// <summary>Projectile impact clear → Receive lock duration.</summary>
    public const float ProjectileImpactClear = 0.05f;

    /// <summary>ColorBomb batch destroy → Receive lock duration.</summary>
    public const float ColorBombBatchClear = 0.15f;
}
