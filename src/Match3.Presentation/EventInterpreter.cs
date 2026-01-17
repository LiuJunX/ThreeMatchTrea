using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Presentation.Animations;

namespace Match3.Presentation;

/// <summary>
/// Interprets game events and creates corresponding animations.
/// Bridges the gap between Core simulation events and Presentation animations.
/// Implements IEventVisitor for compile-time type safety.
/// </summary>
public sealed class EventInterpreter : IEventVisitor
{
    private readonly AnimationTimeline _timeline;
    private readonly VisualState _visualState;
    private float _eventTimeOffset;

    /// <summary>
    /// Duration for tile movement animation.
    /// </summary>
    public float MoveDuration { get; set; } = 0.15f;

    /// <summary>
    /// Duration for tile destruction animation.
    /// </summary>
    public float DestroyDuration { get; set; } = 0.2f;

    /// <summary>
    /// Duration for match highlight before destruction.
    /// </summary>
    public float MatchHighlightDuration { get; set; } = 0.1f;

    /// <summary>
    /// Creates a new event interpreter.
    /// </summary>
    public EventInterpreter(AnimationTimeline timeline, VisualState visualState)
    {
        _timeline = timeline;
        _visualState = visualState;
    }

    /// <summary>
    /// Interpret a batch of events and create animations.
    /// </summary>
    public void InterpretEvents(IReadOnlyList<GameEvent> events)
    {
        foreach (var evt in events)
        {
            evt.Accept(this);
        }
    }

    /// <summary>
    /// Interpret a single event and create animations.
    /// Uses visitor pattern for type-safe dispatch.
    /// </summary>
    public void InterpretEvent(GameEvent evt)
    {
        evt.Accept(this);
    }

    /// <summary>
    /// Set the time offset for event interpretation.
    /// Use this to align event times with timeline time.
    /// </summary>
    public void SetTimeOffset(float offset)
    {
        _eventTimeOffset = offset;
    }

    private float GetAnimationStartTime(GameEvent evt)
    {
        return evt.SimulationTime + _eventTimeOffset;
    }

    #region IEventVisitor Implementation

    /// <inheritdoc />
    public void Visit(TileMovedEvent evt)
    {
        float startTime = GetAnimationStartTime(evt);

        var animation = new TileMoveAnimation(
            _timeline.GenerateAnimationId(),
            evt.TileId,
            evt.FromPosition,
            evt.ToPosition,
            startTime,
            MoveDuration);

        _timeline.AddAnimation(animation);
    }

    /// <inheritdoc />
    public void Visit(TileDestroyedEvent evt)
    {
        float startTime = GetAnimationStartTime(evt);
        var position = new Vector2(evt.GridPosition.X, evt.GridPosition.Y);

        var animation = new TileDestroyAnimation(
            _timeline.GenerateAnimationId(),
            evt.TileId,
            position,
            startTime,
            DestroyDuration);

        _timeline.AddAnimation(animation);

        // Add visual effect based on destruction reason
        string effectType = evt.Reason switch
        {
            DestroyReason.Match => "match_pop",
            DestroyReason.BombEffect => "explosion",
            DestroyReason.Projectile => "projectile_hit",
            DestroyReason.ChainReaction => "chain_pop",
            _ => "pop"
        };

        _visualState.AddEffect(effectType, position, DestroyDuration);
    }

    /// <inheritdoc />
    public void Visit(TileSpawnedEvent evt)
    {
        // Add tile to visual state at spawn position
        _visualState.AddTile(
            evt.TileId,
            evt.Type,
            evt.Bomb,
            evt.GridPosition,
            evt.SpawnPosition);

        // Create move animation from spawn position to grid position
        float startTime = GetAnimationStartTime(evt);
        var targetPos = new Vector2(evt.GridPosition.X, evt.GridPosition.Y);

        var animation = new TileMoveAnimation(
            _timeline.GenerateAnimationId(),
            evt.TileId,
            evt.SpawnPosition,
            targetPos,
            startTime,
            MoveDuration);

        _timeline.AddAnimation(animation);
    }

    /// <inheritdoc />
    public void Visit(TilesSwappedEvent evt)
    {
        float startTime = GetAnimationStartTime(evt);
        var posA = new Vector2(evt.PositionA.X, evt.PositionA.Y);
        var posB = new Vector2(evt.PositionB.X, evt.PositionB.Y);

        // Swap animation for tile A
        var animA = new TileMoveAnimation(
            _timeline.GenerateAnimationId(),
            evt.TileAId,
            posA,
            posB,
            startTime,
            MoveDuration);

        // Swap animation for tile B
        var animB = new TileMoveAnimation(
            _timeline.GenerateAnimationId(),
            evt.TileBId,
            posB,
            posA,
            startTime,
            MoveDuration);

        _timeline.AddAnimation(animA);
        _timeline.AddAnimation(animB);
    }

    /// <inheritdoc />
    public void Visit(MatchDetectedEvent evt)
    {
        // Match detection triggers highlight effects before destruction
        foreach (var pos in evt.Positions)
        {
            var position = new Vector2(pos.X, pos.Y);
            _visualState.AddEffect("match_highlight", position, MatchHighlightDuration);
        }
    }

    /// <inheritdoc />
    public void Visit(BombCreatedEvent evt)
    {
        // Bomb creation visual feedback
        var position = new Vector2(evt.Position.X, evt.Position.Y);
        _visualState.AddEffect("bomb_created", position, 0.3f);
    }

    /// <inheritdoc />
    public void Visit(BombActivatedEvent evt)
    {
        var position = new Vector2(evt.Position.X, evt.Position.Y);

        // Add explosion effect
        _visualState.AddEffect("bomb_explosion", position, 0.4f);

        // Affected tiles will receive their own TileDestroyedEvent
    }

    /// <inheritdoc />
    public void Visit(BombComboEvent evt)
    {
        var posA = new Vector2(evt.PositionA.X, evt.PositionA.Y);
        var posB = new Vector2(evt.PositionB.X, evt.PositionB.Y);

        // Add combo explosion effect at both positions
        _visualState.AddEffect("bomb_combo", posA, 0.5f);
        _visualState.AddEffect("bomb_combo", posB, 0.5f);
    }

    /// <inheritdoc />
    public void Visit(ScoreAddedEvent evt)
    {
        // Score events can trigger floating score text
        // UI layer handles score display
    }

    /// <inheritdoc />
    public void Visit(ComboChangedEvent evt)
    {
        // Combo change can trigger UI effects
        // Currently no animation needed
    }

    /// <inheritdoc />
    public void Visit(MoveCompletedEvent evt)
    {
        // Move completion can trigger UI updates
        // Currently no animation needed
    }

    /// <inheritdoc />
    public void Visit(ProjectileLaunchedEvent evt)
    {
        // Add projectile to visual state
        _visualState.AddProjectile(evt.ProjectileId, evt.Origin);

        // Create launch animation (takeoff phase)
        float startTime = GetAnimationStartTime(evt);
        const float takeoffDuration = 0.3f;
        const float arcHeight = 1.5f;

        var launchAnim = new ProjectileLaunchAnimation(
            _timeline.GenerateAnimationId(),
            evt.ProjectileId,
            evt.Origin,
            arcHeight,
            startTime,
            takeoffDuration);

        _timeline.AddAnimation(launchAnim);
    }

    /// <inheritdoc />
    public void Visit(ProjectileMovedEvent evt)
    {
        float startTime = GetAnimationStartTime(evt);

        // Calculate duration based on velocity (if available)
        float distance = Vector2.Distance(evt.FromPosition, evt.ToPosition);
        float velocity = evt.Velocity.Length();
        float duration = velocity > 0 ? distance / velocity : 0.016f;

        var animation = new ProjectileAnimation(
            _timeline.GenerateAnimationId(),
            evt.ProjectileId,
            evt.FromPosition,
            evt.ToPosition,
            startTime,
            duration);

        _timeline.AddAnimation(animation);
    }

    /// <inheritdoc />
    public void Visit(ProjectileRetargetedEvent evt)
    {
        // Retarget events can trigger a visual indicator
        // Currently no animation needed
    }

    /// <inheritdoc />
    public void Visit(ProjectileImpactEvent evt)
    {
        float startTime = GetAnimationStartTime(evt);
        var position = new Vector2(evt.ImpactPosition.X, evt.ImpactPosition.Y);

        var impactAnim = new ProjectileImpactAnimation(
            _timeline.GenerateAnimationId(),
            evt.ProjectileId,
            position,
            "projectile_explosion",
            startTime);

        _timeline.AddAnimation(impactAnim);

        // Add explosion effect
        _visualState.AddEffect("projectile_explosion", position, 0.3f);

        // Note: Projectile removal from visual state should happen after animation completes
    }

    /// <inheritdoc />
    public void Visit(CoverDestroyedEvent evt)
    {
        var position = new Vector2(evt.GridPosition.X, evt.GridPosition.Y);
        _visualState.AddEffect("cover_destroyed", position, 0.25f);
    }

    /// <inheritdoc />
    public void Visit(GroundDestroyedEvent evt)
    {
        var position = new Vector2(evt.GridPosition.X, evt.GridPosition.Y);
        _visualState.AddEffect("ground_destroyed", position, 0.25f);
    }

    #endregion
}
