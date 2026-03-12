using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.PowerUps.Effects;

public class ColorBombEffect : IBombEffect
{
    /// <summary>
    /// Filter that accepts every element type except None and ColorBomb.
    /// Used by the standalone color-bomb effect to find the most frequent type.
    /// </summary>
    private static bool IsCountable(ElementType t) => t != ElementType.None && t != ElementType.ColorBomb;

    public ElementType Type => ElementType.ColorBomb;

    public void Apply(in GameState state, Position origin, HashSet<Position> affectedTiles)
    {
        // 彩球爆炸：消除出现最多的一种颜色（排除 None 和 ColorBomb 本身）
        var maxType = BombComboHelpers.FindMostFrequentType(in state, IsCountable);

        if (maxType != ElementType.None)
        {
            BombComboHelpers.CollectPositionsOfType(in state, maxType, affectedTiles);
        }
    }
}
