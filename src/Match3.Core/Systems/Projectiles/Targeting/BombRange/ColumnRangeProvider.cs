using System.Collections.Generic;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Projectiles.Targeting.BombRange;

/// <summary>
/// Entire column at the drop point's X coordinate.
/// </summary>
public sealed class ColumnRangeProvider : IBombRangeProvider
{
    public static readonly ColumnRangeProvider Instance = new();
    public UfoPayload Payload => UfoPayload.Column;

    public void GetRange(Position dropPoint, in GameState state, List<Position> positions)
    {
        for (int y = 0; y < state.Height; y++)
        {
            if (!state.IsVoid(dropPoint.X, y))
                positions.Add(new Position(dropPoint.X, y));
        }
    }
}
