using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Generation;

namespace Match3.Core.Systems.Spawning;

/// <summary>
/// Adapts ISpawnModel to ITileGenerator for backward compatibility.
/// Uses a fixed SpawnContext - useful for simple scenarios.
/// </summary>
public class SpawnModelAdapter : ITileGenerator
{
    private readonly ISpawnModel _model;
    private SpawnContext _context;

    public SpawnModelAdapter(ISpawnModel model)
    {
        _model = model;
        _context = SpawnContext.Default;
    }

    public SpawnModelAdapter(ISpawnModel model, SpawnContext context)
    {
        _model = model;
        _context = context;
    }

    /// <summary>
    /// Updates the spawn context (e.g., when game state changes).
    /// </summary>
    public void SetContext(SpawnContext context)
    {
        _context = context;
    }

    public TileType GenerateNonMatchingTile(ref GameState state, int x, int y)
    {
        return _model.Predict(ref state, x, in _context);
    }
}
