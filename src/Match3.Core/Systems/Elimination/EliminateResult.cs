using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Elimination;

/// <summary>
/// Outcome of a single-cell elimination attempt.
/// </summary>
public enum EliminateOutcome : byte
{
    /// <summary>Tile was destroyed (event emitted, objective tracked, ground notified).</summary>
    Eliminated,

    /// <summary>Cover absorbed the hit (cover damaged, tile survived).</summary>
    Absorbed,

    /// <summary>Nothing happened (empty cell or indestructible lock).</summary>
    Blocked,

    /// <summary>Tile is immune to this elimination source (e.g., ColorBomb resists non-manual destruction).</summary>
    Immune,

    /// <summary>No tile present, but bare ground was damaged/destroyed by the hit.</summary>
    GroundOnly,

    /// <summary>Obstacle at this position took damage but survived.</summary>
    ObstacleDamaged,

    /// <summary>Obstacle at this position was fully destroyed.</summary>
    ObstacleDestroyed,
}

/// <summary>
/// Result of a single-cell elimination attempt.
/// Contains the outcome and a snapshot of the tile that was at the position.
/// </summary>
public readonly struct EliminateResult
{
    public EliminateOutcome Outcome { get; }

    /// <summary>
    /// Snapshot of the tile before elimination.
    /// Valid when <see cref="Outcome"/> is <see cref="EliminateOutcome.Eliminated"/>,
    /// <see cref="EliminateOutcome.Absorbed"/>, or <see cref="EliminateOutcome.Immune"/>.
    /// Default (empty) when <see cref="Outcome"/> is <see cref="EliminateOutcome.Blocked"/>
    /// or <see cref="EliminateOutcome.GroundOnly"/>.
    /// </summary>
    public Tile Tile { get; }

    private EliminateResult(EliminateOutcome outcome, Tile tile)
    {
        Outcome = outcome;
        Tile = tile;
    }

    public static EliminateResult Eliminated(Tile tile) => new(EliminateOutcome.Eliminated, tile);
    public static EliminateResult Absorbed(Tile tile) => new(EliminateOutcome.Absorbed, tile);
    public static EliminateResult Immune(Tile tile) => new(EliminateOutcome.Immune, tile);
    public static readonly EliminateResult Blocked = new(EliminateOutcome.Blocked, default);
    public static readonly EliminateResult GroundOnly = new(EliminateOutcome.GroundOnly, default);
    public static readonly EliminateResult ObstacleDamaged = new(EliminateOutcome.ObstacleDamaged, default);
    public static readonly EliminateResult ObstacleDestroyed = new(EliminateOutcome.ObstacleDestroyed, default);
}
