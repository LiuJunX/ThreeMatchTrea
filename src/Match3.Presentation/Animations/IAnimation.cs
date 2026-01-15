using System.Numerics;

namespace Match3.Presentation.Animations;

/// <summary>
/// Base interface for all animations.
/// </summary>
public interface IAnimation
{
    /// <summary>
    /// Unique identifier for this animation.
    /// </summary>
    long Id { get; }

    /// <summary>
    /// Start time of the animation in the timeline.
    /// </summary>
    float StartTime { get; }

    /// <summary>
    /// Duration of the animation in seconds.
    /// </summary>
    float Duration { get; }

    /// <summary>
    /// Whether the animation has completed.
    /// </summary>
    bool IsComplete { get; }

    /// <summary>
    /// Whether the animation is currently active (within its time window).
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Priority for animation ordering (higher = rendered on top).
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// The tile ID this animation targets, or -1 if not tile-specific.
    /// </summary>
    long TargetTileId { get; }

    /// <summary>
    /// Update the animation state.
    /// </summary>
    /// <param name="currentTime">Current timeline time.</param>
    /// <param name="visualState">Visual state to update.</param>
    void Update(float currentTime, IVisualState visualState);

    /// <summary>
    /// Called when animation starts.
    /// </summary>
    void OnStart();

    /// <summary>
    /// Called when animation completes.
    /// </summary>
    void OnComplete();
}

/// <summary>
/// Interface for visual state that animations can modify.
/// </summary>
public interface IVisualState
{
    /// <summary>
    /// Set the visual position of a tile.
    /// </summary>
    void SetTilePosition(long tileId, Vector2 position);

    /// <summary>
    /// Set the visual scale of a tile.
    /// </summary>
    void SetTileScale(long tileId, Vector2 scale);

    /// <summary>
    /// Set the visual alpha (opacity) of a tile.
    /// </summary>
    void SetTileAlpha(long tileId, float alpha);

    /// <summary>
    /// Set whether a tile is visible.
    /// </summary>
    void SetTileVisible(long tileId, bool visible);

    /// <summary>
    /// Set the visual position of a projectile.
    /// </summary>
    void SetProjectilePosition(long projectileId, Vector2 position);

    /// <summary>
    /// Set whether a projectile is visible.
    /// </summary>
    void SetProjectileVisible(long projectileId, bool visible);

    /// <summary>
    /// Add a visual effect at a position.
    /// </summary>
    void AddEffect(string effectType, Vector2 position, float duration);
}
