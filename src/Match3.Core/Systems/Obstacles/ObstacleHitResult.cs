namespace Match3.Core.Systems.Obstacles;

/// <summary>
/// Result of an obstacle direct-hit attempt via <see cref="IObstacleSystem.TryHit"/>.
/// </summary>
public enum ObstacleHitResult : byte
{
    /// <summary>No obstacle at this position — elimination pipeline should continue.</summary>
    NoObstacle,

    /// <summary>Obstacle exists but is immune to this source — hit is blocked.</summary>
    Blocked,

    /// <summary>Obstacle took damage but survived (Stage > 0).</summary>
    Damaged,

    /// <summary>Obstacle was fully destroyed (Stage reached 0).</summary>
    Destroyed
}
