namespace Match3.Core.Systems.Projectiles.Targeting;

/// <summary>
/// Represents a pending UFO attack (in-flight or just selected).
/// 2 bytes — safe to snapshot for MCTS/look-ahead.
/// </summary>
public struct PendingAttack
{
    /// <summary>Grid index: y * width + x. ushort supports up to 255x255 boards.</summary>
    public ushort GridIndex;

    /// <summary>Which layer was hit: 0=Cover, 1=Obstacle, 2=Tile, 3=Ground.</summary>
    public byte HitLayer;
}
