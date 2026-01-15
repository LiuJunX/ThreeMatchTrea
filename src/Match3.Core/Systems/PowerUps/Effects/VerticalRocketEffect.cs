using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.PowerUps.Effects;

public class VerticalRocketEffect : IBombEffect
{
    public BombType Type => BombType.Vertical;

    public void Apply(in GameState state, Position origin, HashSet<Position> affectedTiles)
    {
        for (int y = 0; y < state.Height; y++)
        {
            affectedTiles.Add(new Position(origin.X, y));
        }
    }
}
