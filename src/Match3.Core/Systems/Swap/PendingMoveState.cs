using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Swap;

/// <summary>
/// State data for a pending move awaiting validation.
/// </summary>
public struct PendingMoveState
{
    /// <summary>
    /// Original position of the first tile.
    /// </summary>
    public Position From;

    /// <summary>
    /// Original position of the second tile.
    /// </summary>
    public Position To;

    /// <summary>
    /// ID of the first tile.
    /// </summary>
    public long TileAId;

    /// <summary>
    /// ID of the second tile.
    /// </summary>
    public long TileBId;

    /// <summary>
    /// Whether the swap created a match (captured at swap time).
    /// </summary>
    public bool HadMatch;

    /// <summary>
    /// Whether this move still needs validation.
    /// </summary>
    public bool NeedsValidation;

    /// <summary>
    /// Accumulated animation time since swap.
    /// </summary>
    public float AnimationTime;

    /// <summary>
    /// Whether this is a bomb swap that needs processing after animation.
    /// </summary>
    public bool IsBombSwap;

    /// <summary>
    /// Bomb swap details: whether tile A is a bomb.
    /// </summary>
    public bool TileAIsBomb;

    /// <summary>
    /// Bomb swap details: whether tile B is a bomb.
    /// </summary>
    public bool TileBIsBomb;

    /// <summary>
    /// Bomb swap details: whether tile A is a color bomb (rainbow).
    /// </summary>
    public bool TileAIsColorBomb;

    /// <summary>
    /// Bomb swap details: whether tile B is a color bomb (rainbow).
    /// </summary>
    public bool TileBIsColorBomb;

    /// <summary>
    /// Empty state representing no pending move.
    /// </summary>
    public static PendingMoveState None => new() { NeedsValidation = false };

    /// <summary>
    /// Whether there is a pending move.
    /// </summary>
    public readonly bool HasPending => NeedsValidation;
}
