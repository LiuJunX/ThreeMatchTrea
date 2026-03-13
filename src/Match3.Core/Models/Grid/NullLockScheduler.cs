using Match3.Core.Models.Enums;

namespace Match3.Core.Models.Grid;

/// <summary>
/// No-op lock scheduler for testing scenarios where locking behavior is not needed.
/// All methods are safe no-ops: Acquire returns an invalid token, Release and Tick do nothing.
/// This allows test code to satisfy the non-nullable LockScheduler requirement without
/// incurring any lock state changes on the GameState.
/// </summary>
public sealed class NullLockScheduler : LockScheduler
{
    /// <summary>
    /// Shared singleton instance. Safe to reuse because all operations are stateless no-ops.
    /// </summary>
    public static readonly NullLockScheduler Instance = new();

    /// <inheritdoc />
    public override LockToken Acquire(ref GameState state, Position pos, CellLockType types)
    {
        return LockToken.Invalid;
    }

    /// <inheritdoc />
    public override LockToken Acquire(ref GameState state, Position pos, CellLockType types, float duration)
    {
        return LockToken.Invalid;
    }

    /// <inheritdoc />
    public override void Release(ref GameState state, LockToken token)
    {
        // No-op: nothing was acquired, nothing to release.
    }

    /// <inheritdoc />
    public override void Tick(ref GameState state, float dt)
    {
        // No-op: no timed locks to expire.
    }
}
