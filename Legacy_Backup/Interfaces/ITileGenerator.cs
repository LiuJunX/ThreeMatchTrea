using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Interfaces;

public interface ITileGenerator
{
    TileType GenerateNonMatchingTile(ref GameState state, int x, int y);
}
