using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Generation;

namespace Match3.Core.Systems.Spawning;

/// <summary>
/// Wraps legacy ITileGenerator as ISpawnModel for migration.
/// Ignores SpawnContext and uses the original generation logic.
/// </summary>
public class LegacySpawnModel : ISpawnModel
{
    private readonly ITileGenerator _generator;

    public LegacySpawnModel(ITileGenerator generator)
    {
        _generator = generator;
    }

    public TileType Predict(ref GameState state, int spawnX, in SpawnContext context)
    {
        // Legacy generator doesn't use context
        return _generator.GenerateNonMatchingTile(ref state, spawnX, 0);
    }
}
