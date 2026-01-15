using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.PowerUps.Effects;

public class HorizontalRocketEffect : IBombEffect
{
    public BombType Type => BombType.Horizontal;

    public void Apply(in GameState state, Position origin, HashSet<Position> affectedTiles)
    {
        for (int x = 0; x < state.Width; x++)
        {
            affectedTiles.Add(new Position(x, origin.Y));
        }
    }
}
