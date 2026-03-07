using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Generation;

public interface ITileGenerator
{
    ElementType GenerateNonMatchingTile(ref GameState state, int x, int y);
}
