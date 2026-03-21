using System.Collections.Generic;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Projectiles.Targeting.BombRange;

/// <summary>
/// 5x5 area centered on the drop point (radius=2), clipped to board bounds.
/// </summary>
public sealed class Area5x5RangeProvider : IBombRangeProvider
{
    public static readonly Area5x5RangeProvider Instance = new();
    public UfoPayload Payload => UfoPayload.Area5x5;

    public void GetRange(Position dropPoint, in GameState state, List<Position> positions)
    {
        for (int dy = -2; dy <= 2; dy++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                int x = dropPoint.X + dx;
                int y = dropPoint.Y + dy;
                if (state.IsValid(x, y) && !state.IsVoid(x, y))
                    positions.Add(new Position(x, y));
            }
        }
    }
}
