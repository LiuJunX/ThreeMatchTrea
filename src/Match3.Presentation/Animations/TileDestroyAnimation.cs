using System.Numerics;

namespace Match3.Presentation.Animations;

/// <summary>
/// Animation for tile destruction (shrink and fade).
/// </summary>
public sealed class TileDestroyAnimation : AnimationBase
{
    private readonly long _tileId;
    private readonly Vector2 _position;

    /// <summary>
    /// Default duration for tile destruction.
    /// </summary>
    public const float DefaultDuration = 0.2f;

    /// <inheritdoc />
    public override long TargetTileId => _tileId;

    /// <summary>
    /// The grid position where this destruction is happening.
    /// </summary>
    public Vector2 GridPosition => _position;

    /// <summary>
    /// Creates a new tile destroy animation.
    /// </summary>
    public TileDestroyAnimation(
        long animationId,
        long tileId,
        Vector2 position,
        float startTime,
        float duration = DefaultDuration)
        : base(animationId, startTime, duration)
    {
        _tileId = tileId;
        _position = position;
    }

    /// <inheritdoc />
    public override int Priority => -1; // Render behind moving tiles

    /// <inheritdoc />
    protected override float ApplyEasing(float t)
    {
        // Ease-out: fast start, slow end - player quickly sees destruction begin
        return 1f - (1f - t) * (1f - t);
    }

    /// <inheritdoc />
    protected override void UpdateVisual(float progress, IVisualState visualState)
    {
        // Shrink to zero
        float scale = 1f - progress;
        visualState.SetTileScale(_tileId, new Vector2(scale, scale));

        // Fade out
        float alpha = 1f - progress;
        visualState.SetTileAlpha(_tileId, alpha);
    }

    /// <inheritdoc />
    public override void OnComplete()
    {
        // No need to hide here, the visual layer should handle removal
    }
}
