using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Projectiles;

/// <summary>
/// Base class for all flying entities (projectiles).
/// Projectiles have continuous physics (position, velocity) and can emit events.
/// </summary>
public abstract class Projectile
{
    /// <summary>
    /// Unique identifier for this projectile.
    /// </summary>
    public int Id { get; protected set; }

    /// <summary>
    /// Current position in world space.
    /// </summary>
    public Vector2 Position { get; protected set; }

    /// <summary>
    /// Current velocity.
    /// </summary>
    public Vector2 Velocity { get; protected set; }

    /// <summary>
    /// Type of this projectile.
    /// </summary>
    public ProjectileType Type { get; protected set; }

    /// <summary>
    /// Whether this projectile is still active.
    /// </summary>
    public bool IsActive { get; protected set; } = true;

    /// <summary>
    /// Target grid position (for homing projectiles).
    /// </summary>
    public Position? TargetGridPosition { get; protected set; }

    /// <summary>
    /// Target tile ID (for tracking specific tiles).
    /// </summary>
    public int? TargetTileId { get; protected set; }

    /// <summary>
    /// Origin position where projectile was launched.
    /// </summary>
    public Position OriginPosition { get; protected set; }

    /// <summary>
    /// Source tile ID that spawned this projectile (for linking visual commands).
    /// </summary>
    public int? SourceTileId { get; set; }

    /// <summary>
    /// When true, the Choreographer spawns a new tile visual for this projectile
    /// (used for combo-spawned UFOs that have no pre-existing tile on the board).
    /// </summary>
    public bool SpawnVisual { get; set; }

    /// <summary>
    /// Update projectile physics for one tick.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="deltaTime">Time step in seconds.</param>
    /// <param name="tick">Current simulation tick.</param>
    /// <param name="simTime">Current simulation time.</param>
    /// <param name="events">Event collector for emitting events.</param>
    /// <returns>True if projectile reached its target and should be processed.</returns>
    public abstract bool Update(
        ref GameState state,
        float deltaTime,
        int tick,
        float simTime,
        IEventCollector events);

    /// <summary>
    /// Apply effect when projectile reaches target.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <returns>Set of affected positions.</returns>
    public abstract HashSet<Position> ApplyEffect(ref GameState state);

    /// <summary>
    /// Called when projectile needs to retarget (e.g., original target destroyed).
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="tick">Current simulation tick.</param>
    /// <param name="simTime">Current simulation time.</param>
    /// <param name="events">Event collector.</param>
    /// <returns>True if retargeting succeeded, false if projectile should fizzle.</returns>
    public virtual bool TryRetarget(
        ref GameState state,
        int tick,
        float simTime,
        IEventCollector events)
    {
        return false; // Default: no retargeting
    }

    /// <summary>
    /// Deactivate this projectile.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Check if projectile has reached its target position.
    /// </summary>
    protected bool HasReachedTarget(float threshold = 0.15f)
    {
        if (!TargetGridPosition.HasValue) return false;

        var targetPos = new Vector2(TargetGridPosition.Value.X, TargetGridPosition.Value.Y);
        return Vector2.DistanceSquared(Position, targetPos) < threshold * threshold;
    }

    /// <summary>
    /// Move towards target with given speed.
    /// </summary>
    protected Vector2 MoveTowardsTarget(float speed, float deltaTime)
    {
        if (!TargetGridPosition.HasValue) return Position;

        var targetPos = new Vector2(TargetGridPosition.Value.X, TargetGridPosition.Value.Y);
        var direction = Vector2.Normalize(targetPos - Position);
        var newPosition = Position + direction * speed * deltaTime;

        return newPosition;
    }
}
