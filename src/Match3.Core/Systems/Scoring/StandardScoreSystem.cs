using Match3.Core.Systems.Core;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.View;
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

    public int CalculateSpecialMoveScore(ElementType t1, ElementType t2)
    {
        bool isRainbow1 = t1.IsColorBomb();
        bool isRainbow2 = t2.IsColorBomb();

        // Rainbow + Rainbow
        if (isRainbow1 && isRainbow2) return 5000;

        // Rainbow + Bomb/Normal
        if (isRainbow1 || isRainbow2)
        {
            var other = isRainbow1 ? t2 : t1;
            return other.IsBomb() ? 2500 : 2000; // Bonus for Rainbow+Bomb
        }

        // Bomb + Bomb
        if (t1.IsBomb() && t2.IsBomb())
        {
            return 1000;
        }

        return 0;
    }
}
