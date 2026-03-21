namespace Match3.Core.Systems.Projectiles.Targeting;

/// <summary>
/// Result of evaluating a single layer in the cell stack.
/// Temporary — collected during penetration traversal.
/// </summary>
public struct HitLayer
{
    /// <summary>Layer index: 0=Cover, 1=Obstacle, 2=Tile, 3=Ground.</summary>
    public byte Layer;

    /// <summary>Value contribution of this layer.</summary>
    public ushort Value;

    /// <summary>How many meaningful hits this layer can absorb.</summary>
    public byte HitCapacity;

    /// <summary>Whether this layer is an active level objective.</summary>
    public bool IsTarget;

    /// <summary>Whether this layer blocks penetration to layers below.</summary>
    public bool BlocksPenetration;
}
