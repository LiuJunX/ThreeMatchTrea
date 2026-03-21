namespace Match3.Core.Systems.Projectiles.Targeting;

/// <summary>
/// Result of evaluating a single cell for UFO targeting.
/// Temporary — discarded after selection.
/// </summary>
public struct EvalResult
{
    /// <summary>Whether the cell can be attacked by a UFO projectile.</summary>
    public bool CanAttack;

    /// <summary>Multi-dimensional score.</summary>
    public CellScore Score;

    /// <summary>How many meaningful attacks this cell can still absorb.</summary>
    public byte MeaningfulHits;
}
