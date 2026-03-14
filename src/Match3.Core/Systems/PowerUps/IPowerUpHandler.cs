using Match3.Core.Events;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.PowerUps;

/// <summary>
/// Handles power-up activation and special move processing (bomb combos, color bombs, etc.).
/// </summary>
public interface IPowerUpHandler
{
    /// <summary>
    /// Process a bomb swap (bomb combo or color bomb interaction) without event collection.
    /// </summary>
    void ProcessBombSwap(ref GameState state, Position p1, Position p2, out int points);

    /// <summary>
    /// Process a bomb swap (bomb combo or color bomb interaction) with full event sourcing.
    /// </summary>
    void ProcessBombSwap(
        ref GameState state,
        Position p1,
        Position p2,
        int tick,
        float simTime,
        IEventCollector events,
        out int points);

    /// <summary>
    /// Activate a bomb at the specified position without event collection.
    /// </summary>
    void ActivateBomb(ref GameState state, Position p);

    /// <summary>
    /// Activate a bomb at the specified position with full event sourcing.
    /// </summary>
    void ActivateBomb(ref GameState state, Position p, int tick, float simTime, IEventCollector events,
        bool isChainReaction = false);
}
