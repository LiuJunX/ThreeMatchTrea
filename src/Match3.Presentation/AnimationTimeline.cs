using System.Collections.Generic;
using Match3.Presentation.Animations;

namespace Match3.Presentation;

/// <summary>
/// Manages a timeline of animations with proper sequencing and timing.
/// </summary>
public sealed class AnimationTimeline
{
    private readonly List<IAnimation> _animations = new();
    private readonly List<IAnimation> _completedAnimations = new();
    private float _currentTime;
    private long _nextAnimationId = 1;

    /// <summary>
    /// Current timeline time.
    /// </summary>
    public float CurrentTime => _currentTime;

    /// <summary>
    /// All active animations.
    /// </summary>
    public IReadOnlyList<IAnimation> Animations => _animations;

    /// <summary>
    /// Whether there are any active animations.
    /// </summary>
    public bool HasActiveAnimations => _animations.Count > 0;

    /// <summary>
    /// Time when the last animation will complete.
    /// </summary>
    public float EndTime
    {
        get
        {
            float end = _currentTime;
            foreach (var anim in _animations)
            {
                float animEnd = anim.StartTime + anim.Duration;
                if (animEnd > end) end = animEnd;
            }
            return end;
        }
    }

    /// <summary>
    /// Generate a unique animation ID.
    /// </summary>
    public long GenerateAnimationId()
    {
        return _nextAnimationId++;
    }

    /// <summary>
    /// Add an animation to the timeline.
    /// </summary>
    public void AddAnimation(IAnimation animation)
    {
        _animations.Add(animation);
    }

    /// <summary>
    /// Add an animation that starts at the current time.
    /// </summary>
    public void AddAnimationNow(IAnimation animation)
    {
        _animations.Add(animation);
    }

    /// <summary>
    /// Add an animation that starts after all current animations complete.
    /// </summary>
    public void AddAnimationAfterCurrent(IAnimation animation)
    {
        // Note: The animation's start time should be set by the caller
        _animations.Add(animation);
    }

    /// <summary>
    /// Update the timeline and all animations.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last update.</param>
    /// <param name="visualState">Visual state to update.</param>
    public void Update(float deltaTime, VisualState visualState)
    {
        _currentTime += deltaTime;
        _completedAnimations.Clear();

        // Update all animations
        foreach (var animation in _animations)
        {
            animation.Update(_currentTime, visualState);

            if (animation.IsComplete)
            {
                _completedAnimations.Add(animation);
            }
        }

        // Remove completed animations
        foreach (var completed in _completedAnimations)
        {
            _animations.Remove(completed);
        }

        // Update visual effects
        visualState.UpdateEffects(deltaTime);
    }

    /// <summary>
    /// Skip to the end of all animations (for fast-forward).
    /// </summary>
    public void SkipToEnd(VisualState visualState)
    {
        float targetTime = EndTime;

        while (_animations.Count > 0 && _currentTime < targetTime)
        {
            Update(0.016f, visualState);
        }
    }

    /// <summary>
    /// Clear all animations.
    /// </summary>
    public void Clear()
    {
        _animations.Clear();
        _completedAnimations.Clear();
    }

    /// <summary>
    /// Reset timeline to time zero.
    /// </summary>
    public void Reset()
    {
        _currentTime = 0;
        Clear();
    }

    /// <summary>
    /// Get animations affecting a specific tile.
    /// </summary>
    public IEnumerable<IAnimation> GetAnimationsForTile(long tileId)
    {
        foreach (var anim in _animations)
        {
            if (anim.TargetTileId == tileId)
                yield return anim;
        }
    }

    /// <summary>
    /// Check if a specific tile has any active animations.
    /// </summary>
    public bool HasAnimationForTile(long tileId)
    {
        foreach (var anim in _animations)
        {
            if (anim.TargetTileId == tileId)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Remove all animations for a specific tile.
    /// </summary>
    public void RemoveAnimationsForTile(long tileId)
    {
        _animations.RemoveAll(a => a.TargetTileId == tileId);
    }

    /// <summary>
    /// Get the target position of the first move animation for a tile.
    /// Returns null if no move animation exists for this tile.
    /// </summary>
    public System.Numerics.Vector2? GetMoveTargetForTile(long tileId)
    {
        foreach (var anim in _animations)
        {
            if (anim.TargetTileId == tileId && anim is TileMoveAnimation moveAnim)
            {
                return moveAnim.ToPosition;
            }
        }
        return null;
    }

    /// <summary>
    /// Get the latest destroy animation end time for a specific column (at or above a row).
    /// Used to delay fall animations until destruction completes.
    /// </summary>
    /// <param name="column">The column (x coordinate).</param>
    /// <param name="maxRow">The maximum row to check (inclusive).</param>
    /// <returns>The end time of the latest destroy animation, or current time if none.</returns>
    public float GetDestroyEndTimeForColumn(int column, int maxRow)
    {
        float latestEnd = _currentTime;

        foreach (var anim in _animations)
        {
            if (anim is TileDestroyAnimation destroy)
            {
                int animX = (int)destroy.GridPosition.X;
                int animY = (int)destroy.GridPosition.Y;

                // Check if this destroy animation is in the same column and at or above maxRow
                if (animX == column && animY <= maxRow)
                {
                    float endTime = anim.StartTime + anim.Duration;
                    if (endTime > latestEnd)
                    {
                        latestEnd = endTime;
                    }
                }
            }
        }

        return latestEnd;
    }

    /// <summary>
    /// Get the latest move animation end time for a specific column (at or below a row).
    /// Used to delay new tile animations until existing tiles finish falling.
    /// </summary>
    /// <param name="column">The column (x coordinate).</param>
    /// <param name="minRow">The minimum row to check (inclusive) - tiles at or below this row.</param>
    /// <returns>The end time of the latest move animation, or current time if none.</returns>
    public float GetMoveEndTimeForColumn(int column, int minRow)
    {
        float latestEnd = _currentTime;

        foreach (var anim in _animations)
        {
            if (anim is TileMoveAnimation move)
            {
                int animX = (int)move.ToPosition.X;
                int animY = (int)move.ToPosition.Y;

                // Check if this move animation is in the same column and at or below minRow
                if (animX == column && animY >= minRow)
                {
                    float endTime = anim.StartTime + anim.Duration;
                    if (endTime > latestEnd)
                    {
                        latestEnd = endTime;
                    }
                }
            }
        }

        return latestEnd;
    }

    /// <summary>
    /// Get the time when a tile in the column crosses the half-cell threshold (0.5 cells moved).
    /// This is used to start the above tile's animation when the below tile clears enough space.
    /// </summary>
    /// <param name="column">The column (x coordinate).</param>
    /// <param name="minRow">The minimum row to check (tiles moving through or to this row).</param>
    /// <returns>The time when a tile crosses the 0.5 cell threshold, or current time if none.</returns>
    public float GetMoveHalfCellTimeForColumn(int column, int minRow)
    {
        float latestHalfTime = _currentTime;

        foreach (var anim in _animations)
        {
            if (anim is TileMoveAnimation move)
            {
                int animX = (int)move.ToPosition.X;
                int toY = (int)move.ToPosition.Y;
                float fromY = move.FromPosition.Y;

                // Check if this move animation is in the same column and passes through or ends at/below minRow
                if (animX == column && toY >= minRow)
                {
                    // Calculate how far the tile moves
                    float totalDistance = move.ToPosition.Y - fromY;

                    if (totalDistance > 0)
                    {
                        // Calculate the time ratio to move 0.5 cells from the starting position
                        // This is when the tile "clears" its original cell
                        float halfCellRatio = 0.5f / totalDistance;
                        halfCellRatio = System.Math.Min(halfCellRatio, 1.0f); // Cap at 100%

                        float halfCellTime = anim.StartTime + anim.Duration * halfCellRatio;

                        if (halfCellTime > latestHalfTime)
                        {
                            latestHalfTime = halfCellTime;
                        }
                    }
                }
            }
        }

        return latestHalfTime;
    }

    /// <summary>
    /// Get the latest destroy animation end time across all active destroy animations.
    /// </summary>
    public float GetLatestDestroyEndTime()
    {
        float latestEnd = _currentTime;

        foreach (var anim in _animations)
        {
            if (anim is TileDestroyAnimation)
            {
                float endTime = anim.StartTime + anim.Duration;
                if (endTime > latestEnd)
                {
                    latestEnd = endTime;
                }
            }
        }

        return latestEnd;
    }
}
