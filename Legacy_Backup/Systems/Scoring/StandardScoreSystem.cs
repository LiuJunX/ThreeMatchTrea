using Match3.Core.Interfaces;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;

namespace Match3.Core.Systems.Scoring;

public class StandardScoreSystem : IScoreSystem
{
    public int CalculateMatchScore(MatchGroup group)
    {
        // Base score: 10 points per tile
        return group.Positions.Count * 10;
    }

    public int CalculateSpecialMoveScore(TileType t1, BombType b1, TileType t2, BombType b2)
    {
        bool isRainbow1 = t1 == TileType.Rainbow;
        bool isRainbow2 = t2 == TileType.Rainbow;

        // Rainbow + Rainbow
        if (isRainbow1 && isRainbow2) return 5000;

        // Rainbow + Bomb/Normal
        if (isRainbow1 || isRainbow2)
        {
            var otherBomb = isRainbow1 ? b2 : b1;
            return otherBomb != BombType.None ? 2500 : 2000; // Bonus for Rainbow+Bomb
        }

        // Bomb + Bomb
        if (b1 != BombType.None && b2 != BombType.None)
        {
            return 1000;
        }

        return 0;
    }
}
