using System.Numerics;

namespace Match3.Presentation.Animations;

/// <summary>
/// Animation for tile movement (gravity, swap, etc).
/// </summary>
public sealed class TileMoveAnimation : AnimationBase
{
    private readonly long _tileId;
    private readonly Vector2 _fromPosition;
    private readonly Vector2 _toPosition;

    /// <summary>
    /// Default duration for tile movement.
    /// </summary>
    public const float DefaultDuration = 0.15f;

    /// <inheritdoc />
    public override long TargetTileId => _tileId;

    /// <summary>
    /// Creates a new tile move animation.
    /// </summary>
    public TileMoveAnimation(
        long animationId,
        long tileId,
        Vector2 from,
        Vector2 to,
        float startTime,
        float duration = DefaultDuration)
        : base(animationId, startTime, duration)
    {
        _tileId = tileId;
        _fromPosition = from;
        _toPosition = to;
    }

    /// <inheritdoc />
    protected override void UpdateVisual(float progress, IVisualState visualState)
    {
        var currentPos = Vector2.Lerp(_fromPosition, _toPosition, progress);
        visualState.SetTilePosition(_tileId, currentPos);
    }
}
