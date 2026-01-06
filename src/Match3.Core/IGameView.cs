using System.Collections.Generic;
using Match3.Core.Structs;

namespace Match3.Core;
public interface IGameView
{
    void RenderBoard(TileType[,] board);
    void ShowSwap(Position a, Position b, bool success);
    void ShowMatches(IReadOnlyCollection<Position> matched);
    void ShowGravity(IEnumerable<TileMove> moves);
    void ShowRefill(IEnumerable<TileMove> newTiles);
}
