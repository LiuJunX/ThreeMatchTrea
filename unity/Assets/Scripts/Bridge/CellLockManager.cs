using System;
using System.Collections.Generic;
using Match3.Core.Choreography;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using UnityEngine;

namespace Match3.Unity.Bridge
{
    /// <summary>
    /// Manages per-cell lock timers for choreography synchronization.
    /// Acquires locks from Choreographer's schedule, ticks timers, and releases expired locks.
    /// </summary>
    internal sealed class CellLockManager
    {
        private readonly struct ActiveLock
        {
            public readonly LockToken Token;
            public readonly float Duration;

            public ActiveLock(LockToken token, float duration)
            {
                Token = token;
                Duration = duration;
            }
        }

        private readonly List<ActiveLock> _activeLocks = new();
        private readonly List<float> _lockTimers = new();
        private readonly HashSet<long> _flyPositionKeys = new();

        /// <summary>
        /// Read lock entries from Choreographer, adjust durations based on fly context, acquire locks.
        /// </summary>
        public void AcquireFromSchedule(
            IReadOnlyList<CellLockEntry> entries,
            IReadOnlyList<Match3Bridge.FlyCollectionRequest> pendingFlies,
            Func<Position, CellLockType, LockToken> acquireLock)
        {
            if (entries.Count == 0) return;

            // Build set of positions that will fly to objectives (for duration adjustment)
            _flyPositionKeys.Clear();
            foreach (var fly in pendingFlies)
            {
                if (fly.MergeTarget == null)
                    _flyPositionKeys.Add(PackGridKey(fly.SourceGridPosition.X, fly.SourceGridPosition.Y));
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                float duration = entry.Duration;

                if (entry.IsMerge)
                {
                    // Merge locks: slightly shorter to let gravity start sooner
                    duration -= 0.03f;
                }
                else
                {
                    // Match locks: adjust based on whether tile flies to objective
                    var key = PackGridKey(entry.Position.X, entry.Position.Y);
                    duration += _flyPositionKeys.Contains(key) ? 0.05f : -0.05f;
                }

                duration = Mathf.Max(duration, 0.01f);
                var token = acquireLock(entry.Position, entry.LockType);
                _activeLocks.Add(new ActiveLock(token, duration));
                _lockTimers.Add(duration);
            }
        }

        /// <summary>
        /// Tick all active lock timers, release expired ones.
        /// </summary>
        public void Tick(float deltaTime, Action<LockToken> releaseLock)
        {
            for (int i = _lockTimers.Count - 1; i >= 0; i--)
            {
                _lockTimers[i] -= deltaTime;
                if (_lockTimers[i] <= 0f)
                {
                    releaseLock(_activeLocks[i].Token);
                    _activeLocks.RemoveAt(i);
                    _lockTimers.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Release all active locks immediately.
        /// </summary>
        public void ReleaseAll(Action<LockToken> releaseLock)
        {
            for (int i = 0; i < _activeLocks.Count; i++)
                releaseLock(_activeLocks[i].Token);
            _activeLocks.Clear();
            _lockTimers.Clear();
        }

        private static long PackGridKey(int x, int y) => ((long)x << 32) | (uint)y;
    }
}
