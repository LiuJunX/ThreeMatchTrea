using System;
using System.Numerics;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Spawning;

namespace Match3.Core.Systems.Physics;

/// <summary>
/// Default refill system that spawns new tiles at the top of each column.
/// </summary>
public class RealtimeRefillSystem : IRefillSystem
{
    private readonly ISpawnModel _spawnModel;

    public RealtimeRefillSystem(ISpawnModel spawnModel)
    {
        _spawnModel = spawnModel;
    }

    public void Update(ref GameState state)
    {
        // Build SpawnContext from current game state
        var context = new SpawnContext
        {
            TargetDifficulty = state.TargetDifficulty,
            RemainingMoves = Math.Max(0, state.MoveLimit - state.MoveCount),
            GoalProgress = CalculateGoalProgress(ref state),
            FailedAttempts = 0,     // Phase 2 TODO: track from session. Mercy is inactive until this is wired.
            InFlowState = false     // Reserved for Phase 2
        };

        for (int x = 0; x < state.Width; x++)
        {
            // Find the topmost non-hole cell in this column (spawn point)
            int spawnY = -1;
            for (int y = 0; y < state.Height; y++)
            {
                if (!state.IsHole(x, y)) { spawnY = y; break; }
            }

            // Column is entirely holes — skip
            if (spawnY < 0) continue;

            // Only spawn if the spawn point is empty, has no obstacle, and can receive
            if (state.GetTile(x, spawnY).Type == ElementType.None && !state.HasObstacle(x, spawnY) && state.CanReceive(x, spawnY))
            {
                // Spawn a new tile at the top using the spawn model
                var type = _spawnModel.Predict(ref state, x, in context);
                var tile = new Tile(state.NextTileId++, type, x, spawnY);

                // Start position: one row above the spawn point
                tile.Position = new Vector2(x, spawnY - 1.0f);
                tile.Velocity = new Vector2(0, 2.0f); // Initial downward velocity
                tile.IsFalling = true;

                state.SetTile(x, spawnY, tile);
            }
        }
    }

    /// <summary>
    /// Calculates overall goal progress (0-1) from objective progress.
    /// </summary>
    private static float CalculateGoalProgress(ref GameState state)
    {
        int totalTarget = 0;
        int totalCurrent = 0;
        for (int i = 0; i < state.ObjectiveProgress.Length; i++)
        {
            if (!state.ObjectiveProgress[i].IsActive) continue;
            totalTarget += state.ObjectiveProgress[i].TargetCount;
            totalCurrent += state.ObjectiveProgress[i].CurrentCount;
        }
        return totalTarget > 0 ? (float)totalCurrent / totalTarget : 0f;
    }
}