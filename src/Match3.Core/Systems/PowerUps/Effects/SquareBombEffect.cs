using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.PowerUps.Effects;

public class SquareBombEffect : IBombEffect
{
    public ElementType Type => ElementType.Square5x5;

    public void Apply(in GameState state, Position origin, HashSet<Position> affectedTiles)
    {
        // 5x5 Area (Radius 2)
        BombComboHelpers.ApplyArea(in state, origin, 2, affectedTiles);
    }
}
