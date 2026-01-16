using Match3.Core.Events;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Layers;

/// <summary>
/// System responsible for managing ground elements.
/// Ground elements are damaged when tiles above are destroyed.
/// </summary>
public interface IGroundSystem
{
    /// <summary>
    /// Notifies the ground system that a tile was destroyed at this position.
    /// Damages the ground element if one exists.
    /// </summary>
    /// <param name="state">The game state.</param>
    /// <param name="position">The position where tile was destroyed.</param>
    /// <param name="tick">Current simulation tick for event.</param>
    /// <param name="simTime">Current simulation time for event.</param>
    /// <param name="events">Event collector for emitting events.</param>
    void OnTileDestroyed(ref GameState state, Position position, long tick, float simTime, IEventCollector events);
}
