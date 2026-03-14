namespace Match3.Core.Systems.Elimination;

/// <summary>
/// Result of a single-cell elimination attempt.
/// </summary>
public enum EliminateResult : byte
{
    /// <summary>Tile was destroyed (event emitted, objective tracked, ground notified).</summary>
    Eliminated,

    /// <summary>Cover absorbed the hit (cover damaged, tile survived).</summary>
    Absorbed,

    /// <summary>Nothing happened (empty cell or indestructible lock).</summary>
    Blocked,
}
