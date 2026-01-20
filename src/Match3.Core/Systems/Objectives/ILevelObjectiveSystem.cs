using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Objectives;

/// <summary>
/// Interface for level objective tracking system.
/// </summary>
public interface ILevelObjectiveSystem
{
    /// <summary>
    /// Initialize objectives from level configuration.
    /// </summary>
    void Initialize(ref GameState state, LevelConfig config);

    /// <summary>
    /// Called when a tile is destroyed.
    /// </summary>
    void OnTileDestroyed(ref GameState state, TileType type, long tick, float simTime, IEventCollector events);

    /// <summary>
    /// Called when a cover is destroyed.
    /// </summary>
    void OnCoverDestroyed(ref GameState state, CoverType type, long tick, float simTime, IEventCollector events);

    /// <summary>
    /// Called when a ground is destroyed.
    /// </summary>
    void OnGroundDestroyed(ref GameState state, GroundType type, long tick, float simTime, IEventCollector events);

    /// <summary>
    /// Check if all objectives are completed (victory condition).
    /// </summary>
    bool IsLevelComplete(in GameState state);

    /// <summary>
    /// Check if level is failed (out of moves).
    /// </summary>
    bool IsLevelFailed(in GameState state);

    /// <summary>
    /// Update level status based on current state.
    /// Should be called after board stabilizes.
    /// </summary>
    void UpdateLevelStatus(ref GameState state, long tick, float simTime, IEventCollector events);
}
