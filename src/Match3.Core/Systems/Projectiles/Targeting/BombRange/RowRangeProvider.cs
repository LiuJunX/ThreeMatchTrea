using System.Collections.Generic;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Projectiles.Targeting.BombRange;

/// <summary>
/// Entire row at the drop point's Y coordinate.
/// </summary>
public sealed class RowRangeProvider : IBombRangeProvider
{
    public static readonly RowRangeProvider Instance = new();
    public UfoPayload Payload => UfoPayload.Row;

    public void GetRange(Position dropPoint, in GameState state, List<Position> positions)
    {
        for (int x = 0; x < state.Width; x++)
        {
            if (!state.IsVoid(x, dropPoint.Y))
                positions.Add(new Position(x, dropPoint.Y));
        }
    }
}
