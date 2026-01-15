namespace Match3.Presentation.Animations;

/// <summary>
/// Base class for animations providing common functionality.
/// </summary>
public abstract class AnimationBase : IAnimation
{
    private bool _started;
    private bool _completed;

    /// <inheritdoc />
    public long Id { get; }

    /// <inheritdoc />
    public float StartTime { get; }

    /// <inheritdoc />
    public float Duration { get; protected set; }

    /// <inheritdoc />
    public bool IsComplete => _completed;

    /// <inheritdoc />
    public bool IsActive => _started && !_completed;

    /// <inheritdoc />
    public virtual int Priority => 0;

    /// <inheritdoc />
    public virtual long TargetTileId => -1;

    /// <summary>
    /// Creates a new animation.
    /// </summary>
    protected AnimationBase(long id, float startTime, float duration)
    {
        Id = id;
        StartTime = startTime;
        Duration = duration;
    }

    /// <inheritdoc />
    public void Update(float currentTime, IVisualState visualState)
    {
        if (_completed) return;

        float localTime = currentTime - StartTime;

        // Not yet started
        if (localTime < 0) return;

        // Start the animation
        if (!_started)
        {
            _started = true;
            OnStart();
        }

        // Calculate progress (0-1)
        float progress = Duration > 0 ? localTime / Duration : 1f;

        if (progress >= 1f)
        {
            progress = 1f;
            _completed = true;
        }

        // Apply eased progress
        float easedProgress = ApplyEasing(progress);

        // Update visual state
        UpdateVisual(easedProgress, visualState);

        if (_completed)
        {
            OnComplete();
        }
    }

    /// <summary>
    /// Apply easing function to progress.
    /// Override in subclasses for custom easing.
    /// </summary>
    protected virtual float ApplyEasing(float t)
    {
        // Default: ease-out cubic
        return 1f - (1f - t) * (1f - t) * (1f - t);
    }

    /// <summary>
    /// Update visual state based on eased progress.
    /// </summary>
    protected abstract void UpdateVisual(float progress, IVisualState visualState);

    /// <inheritdoc />
    public virtual void OnStart() { }

    /// <inheritdoc />
    public virtual void OnComplete() { }
}
