using Match3.Core.Events.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Obstacles;

/// <summary>
/// Snapshot of an eliminated tile, used for adjacent obstacle notification.
/// </summary>
public readonly struct EliminatedTileInfo
{
    /// <summary>Position where the tile was eliminated.</summary>
    public Position Pos { get; }

    /// <summary>Snapshot of the eliminated tile (pre-destruction).</summary>
    public Tile Tile { get; }

    /// <summary>What caused the elimination.</summary>
    public ElimSource Source { get; }

    public EliminatedTileInfo(Position pos, Tile tile, ElimSource source)
    {
        Pos = pos;
        Tile = tile;
        Source = source;
    }
}
