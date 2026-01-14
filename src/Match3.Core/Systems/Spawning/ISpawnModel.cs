using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Spawning;

/// <summary>
/// Interface for spawn point models that decide which tile type to generate.
/// Implementations can range from simple rules to ML models.
/// </summary>
public interface ISpawnModel
{
    /// <summary>
    /// Predicts the best tile type to spawn at the given position.
    /// </summary>
    /// <param name="state">Current game state (read-only)</param>
    /// <param name="spawnX">X coordinate of the spawn point</param>
    /// <param name="context">Spawn context with difficulty and game progress info</param>
    /// <returns>The tile type to spawn</returns>
    TileType Predict(ref GameState state, int spawnX, in SpawnContext context);
}
