using System;
using System.Collections.Generic;
using Match3.Core.Models.Enums;

namespace Match3.Core.Models.Grid;

/// <summary>
/// Centralized lock lifecycle manager. Provides idempotent release and timed auto-release.
/// All cell lock acquisition in the simulation should go through this class.
///
/// Lock lifecycle: callers <see cref="Acquire(ref GameState, Position, CellLockType)"/> a token,
/// which increments ref-counts on the target cell. The caller later calls
/// <see cref="Release(ref GameState, LockToken)"/> to decrement those ref-counts.
/// Timed overloads auto-release after a duration via <see cref="Tick"/>.
/// </summary>
public class LockScheduler
{
    private int _nextId;
    private readonly HashSet<int> _activeIds = new();

    private int _timedCount;
    private LockToken[] _timedTokens;
    private float[] _timedRemaining;

    private const int InitialTimedCapacity = 32;

    public LockScheduler()
    {
        _timedTokens = new LockToken[InitialTimedCapacity];
        _timedRemaining = new float[InitialTimedCapacity];
    }

    /// <summary>
    /// True if there are any active timed locks awaiting expiry.
    /// Used by IsStable() — timed locks imply pending state changes.
    /// </summary>
    public bool HasTimedLocks => _timedCount > 0;

    /// <summary>
    /// Acquire a manual lock on a cell. Caller is responsible for calling Release.
    /// </summary>
    public virtual LockToken Acquire(ref GameState state, Position pos, CellLockType types)
    {
        int id = ++_nextId;
        int idx = state.Index(pos);
        state.CellLocks[idx] = CellLockOps.Lock(state.CellLocks[idx], types);
        _activeIds.Add(id);
        return new LockToken(id, idx, types);
    }

    /// <summary>
    /// Acquire a timed lock on a cell. Automatically released after duration expires.
    /// </summary>
    public virtual LockToken Acquire(ref GameState state, Position pos, CellLockType types, float duration)
    {
        var token = Acquire(ref state, pos, types);
        EnsureTimedCapacity();
        _timedTokens[_timedCount] = token;
        _timedRemaining[_timedCount] = duration;
        _timedCount++;
        return token;
    }

    /// <summary>
    /// Idempotent release. Safe to call multiple times with the same token.
    /// </summary>
    public virtual void Release(ref GameState state, LockToken token)
    {
        if (!token.IsValid) return;
        if (!_activeIds.Remove(token.Id)) return;
        state.CellLocks[token.CellIndex] = CellLockOps.Unlock(state.CellLocks[token.CellIndex], token.Types);
    }

    /// <summary>
    /// Process timed lock expiry. Call once per simulation tick.
    /// </summary>
    public virtual void Tick(ref GameState state, float dt)
    {
        for (int i = _timedCount - 1; i >= 0; i--)
        {
            _timedRemaining[i] -= dt;
            if (_timedRemaining[i] <= 0f)
            {
                Release(ref state, _timedTokens[i]);

                int last = _timedCount - 1;
                if (i < last)
                {
                    _timedTokens[i] = _timedTokens[last];
                    _timedRemaining[i] = _timedRemaining[last];
                }
                _timedTokens[last] = default;
                _timedCount--;
            }
        }
    }

    /// <summary>
    /// Clone the scheduler for AI simulation branching.
    /// </summary>
    public LockScheduler Clone()
    {
        var clone = new LockScheduler();
        clone._nextId = _nextId;
        clone._timedCount = _timedCount;

        foreach (var id in _activeIds)
            clone._activeIds.Add(id);

        clone._timedTokens = new LockToken[_timedTokens.Length];
        clone._timedRemaining = new float[_timedRemaining.Length];
        Array.Copy(_timedTokens, clone._timedTokens, _timedCount);
        Array.Copy(_timedRemaining, clone._timedRemaining, _timedCount);

        return clone;
    }

    /// <summary>
    /// Reset all state for level restart. Clears all locks from the game state.
    /// </summary>
    public void Reset(ref GameState state)
    {
        Array.Clear(state.CellLocks, 0, state.CellLocks.Length);
        _nextId = 0;
        _timedCount = 0;
        _activeIds.Clear();
        Array.Clear(_timedTokens, 0, _timedTokens.Length);
    }

    private void EnsureTimedCapacity()
    {
        if (_timedCount < _timedTokens.Length) return;
        int newCapacity = _timedTokens.Length * 2;
        var newTokens = new LockToken[newCapacity];
        var newRemaining = new float[newCapacity];
        Array.Copy(_timedTokens, newTokens, _timedCount);
        Array.Copy(_timedRemaining, newRemaining, _timedCount);
        _timedTokens = newTokens;
        _timedRemaining = newRemaining;
    }
}
