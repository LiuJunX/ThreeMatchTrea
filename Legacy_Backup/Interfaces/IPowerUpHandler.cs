using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Interfaces;

public interface IPowerUpHandler
{
    void ProcessSpecialMove(ref GameState state, Position p1, Position p2, out int points);
}
