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
        int radius = 2;
        for (int y = origin.Y - radius; y <= origin.Y + radius; y++)
        {
            for (int x = origin.X - radius; x <= origin.X + radius; x++)
            {
                if (x >= 0 && x < state.Width && y >= 0 && y < state.Height)
                {
                    affectedTiles.Add(new Position(x, y));
                }
            }
        }
    }
}
