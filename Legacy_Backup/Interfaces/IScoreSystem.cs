using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;

namespace Match3.Core.Interfaces;

/// <summary>
/// Responsible for calculating scores for various game events.
/// </summary>
public interface IScoreSystem
{
    /// <summary>
    /// Calculates the score for a match group.
    /// </summary>
    int CalculateMatchScore(MatchGroup group);

    /// <summary>
    /// Calculates the score for a special move (e.g., Rainbow + Bomb).
    /// </summary>
    int CalculateSpecialMoveScore(TileType t1, BombType b1, TileType t2, BombType b2);
}
