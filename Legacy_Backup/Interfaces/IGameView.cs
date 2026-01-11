using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;

namespace Match3.Core.Interfaces;

public interface IGameView
{
    void RenderBoard(TileType[,] board);
    void ShowSwap(Position a, Position b, bool success);
    void ShowMatches(IReadOnlyCollection<Position> matched);
    void ShowGravity(IEnumerable<TileMove> moves);
    void ShowRefill(IEnumerable<TileMove> newTiles);
}
