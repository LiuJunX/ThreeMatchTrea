using System.Collections.Generic;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Projectiles.Targeting.BombRange;

/// <summary>
/// Provides the affected cell positions for a bomb payload dropped at a given point.
/// Stateless — safe to share across parallel simulations.
/// </summary>
public interface IBombRangeProvider
{
    UfoPayload Payload { get; }

    /// <summary>
    /// Returns all positions affected if a bomb lands at <paramref name="dropPoint"/>.
    /// Writes results into <paramref name="positions"/> to avoid allocation.
    /// </summary>
    void GetRange(Position dropPoint, in GameState state, List<Position> positions);
}
