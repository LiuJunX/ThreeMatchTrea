using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Objectives;

/// <summary>
/// Default implementation of level objective tracking.
/// </summary>
public class LevelObjectiveSystem : ILevelObjectiveSystem
{
    /// <inheritdoc />
    public void Initialize(ref GameState state, LevelConfig config)
    {
        // Initialize all 4 objective slots
        for (int i = 0; i < 4; i++)
        {
            if (config.Objectives != null && i < config.Objectives.Length)
            {
                var objective = config.Objectives[i];
                state.ObjectiveProgress[i] = new ObjectiveProgress
                {
                    TargetLayer = objective.TargetLayer,
                    ElementType = objective.ElementType,
                    TargetCount = objective.TargetCount,
                    CurrentCount = 0
                };
            }
            else
            {
                // Inactive slot
                state.ObjectiveProgress[i] = new ObjectiveProgress
                {
                    TargetLayer = ObjectiveTargetLayer.None,
                    ElementType = 0,
                    TargetCount = 0,
                    CurrentCount = 0
                };
            }
        }

        state.LevelStatus = LevelStatus.InProgress;
    }

    /// <inheritdoc />
    public void OnTileDestroyed(ref GameState state, TileType type, long tick, float simTime, IEventCollector events)
    {
        UpdateProgress(ref state, ObjectiveTargetLayer.Tile, (int)type, tick, simTime, events);
    }

    /// <inheritdoc />
    public void OnCoverDestroyed(ref GameState state, CoverType type, long tick, float simTime, IEventCollector events)
    {
        UpdateProgress(ref state, ObjectiveTargetLayer.Cover, (int)type, tick, simTime, events);
    }

    /// <inheritdoc />
    public void OnGroundDestroyed(ref GameState state, GroundType type, long tick, float simTime, IEventCollector events)
    {
        UpdateProgress(ref state, ObjectiveTargetLayer.Ground, (int)type, tick, simTime, events);
    }

    private void UpdateProgress(ref GameState state, ObjectiveTargetLayer layer, int elementType, long tick, float simTime, IEventCollector events)
    {
        for (int i = 0; i < 4; i++)
        {
            ref var progress = ref state.ObjectiveProgress[i];

            if (!progress.IsActive)
                continue;

            if (progress.TargetLayer != layer)
                continue;

            if (progress.ElementType != elementType)
                continue;

            // Already completed, skip
            if (progress.IsCompleted)
                continue;

            int previousCount = progress.CurrentCount;
            progress.CurrentCount++;

            if (events.IsEnabled)
            {
                events.Emit(new ObjectiveProgressEvent
                {
                    Tick = tick,
                    SimulationTime = simTime,
                    ObjectiveIndex = i,
                    PreviousCount = previousCount,
                    CurrentCount = progress.CurrentCount,
                    TargetCount = progress.TargetCount,
                    IsCompleted = progress.IsCompleted
                });
            }
        }
    }

    /// <inheritdoc />
    public bool IsLevelComplete(in GameState state)
    {
        for (int i = 0; i < 4; i++)
        {
            var progress = state.ObjectiveProgress[i];
            if (progress.IsActive && !progress.IsCompleted)
                return false;
        }

        // Check if there's at least one active objective
        bool hasActiveObjective = false;
        for (int i = 0; i < 4; i++)
        {
            if (state.ObjectiveProgress[i].IsActive)
            {
                hasActiveObjective = true;
                break;
            }
        }

        return hasActiveObjective;
    }

    /// <inheritdoc />
    public bool IsLevelFailed(in GameState state)
    {
        // No objectives means no failure check
        bool hasActiveObjective = false;
        for (int i = 0; i < 4; i++)
        {
            if (state.ObjectiveProgress[i].IsActive)
            {
                hasActiveObjective = true;
                break;
            }
        }

        if (!hasActiveObjective)
            return false;

        // Out of moves and not completed
        return state.MoveCount >= state.MoveLimit && !IsLevelComplete(in state);
    }

    /// <inheritdoc />
    public void UpdateLevelStatus(ref GameState state, long tick, float simTime, IEventCollector events)
    {
        // Already ended
        if (state.LevelStatus != LevelStatus.InProgress)
            return;

        if (IsLevelComplete(in state))
        {
            state.LevelStatus = LevelStatus.Victory;

            if (events.IsEnabled)
            {
                events.Emit(new LevelCompletedEvent
                {
                    Tick = tick,
                    SimulationTime = simTime,
                    IsVictory = true,
                    FinalScore = state.Score,
                    MovesUsed = (int)state.MoveCount
                });
            }
        }
        else if (IsLevelFailed(in state))
        {
            state.LevelStatus = LevelStatus.Defeat;

            if (events.IsEnabled)
            {
                events.Emit(new LevelCompletedEvent
                {
                    Tick = tick,
                    SimulationTime = simTime,
                    IsVictory = false,
                    FinalScore = state.Score,
                    MovesUsed = (int)state.MoveCount
                });
            }
        }
    }
}
