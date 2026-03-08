using System;
using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using UnityEngine;

namespace Match3.Unity.Bridge
{
    /// <summary>
    /// Scans game events for objective collections and generates fly-to-objective requests.
    /// Extracted from Match3Bridge to isolate objective event processing.
    /// </summary>
    internal sealed class ObjectiveCollectionProcessor
    {
        private readonly Dictionary<float, Dictionary<ElementType, Queue<(int TileId, Position Pos, Position? MergeTarget)>>>
            _destroyedBySimTime = new();
        private readonly List<Match3Bridge.FlyCollectionRequest> _pendingFlies = new();

        // Pooled inner collections to avoid per-Process() allocations
        private readonly List<Dictionary<ElementType, Queue<(int, Position, Position?)>>> _innerDictPool = new();
        private readonly List<Queue<(int, Position, Position?)>> _innerQueuePool = new();

        // Merge delay working collections (reused)
        private readonly Dictionary<Position, List<int>> _mergeGroups = new();
        private readonly List<List<int>> _mergeIndexListPool = new();

        /// <summary>
        /// Pending fly requests from the last Process() call.
        /// Used by CellLockManager to adjust lock durations.
        /// </summary>
        public IReadOnlyList<Match3Bridge.FlyCollectionRequest> PendingFlies => _pendingFlies;

        /// <summary>
        /// Scan events for objective collections, generate fly requests, and fire callbacks.
        /// </summary>
        public void Process(IReadOnlyList<GameEvent> events, GameState state, Action<Match3Bridge.FlyCollectionRequest> onCollected)
        {
            // Return inner collections to pools before clearing
            foreach (var kvp in _destroyedBySimTime)
            {
                var byType = kvp.Value;
                foreach (var inner in byType.Values)
                {
                    inner.Clear();
                    _innerQueuePool.Add(inner);
                }
                byType.Clear();
                _innerDictPool.Add(byType);
            }
            _destroyedBySimTime.Clear();
            _pendingFlies.Clear();

            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                if (evt is TileDestroyedEvent tde && tde.Reason == DestroyReason.Match)
                {
                    if (!_destroyedBySimTime.TryGetValue(tde.SimulationTime, out var byType))
                    {
                        byType = RentInnerDict();
                        _destroyedBySimTime[tde.SimulationTime] = byType;
                    }
                    if (!byType.TryGetValue(tde.Type, out var queue))
                    {
                        queue = RentInnerQueue();
                        byType[tde.Type] = queue;
                    }
                    queue.Enqueue((tde.TileId, tde.GridPosition, tde.MergeTarget));
                }
                else if (evt is ObjectiveProgressEvent ope)
                {
                    if (ope.ObjectiveIndex < 0 || ope.ObjectiveIndex >= state.ObjectiveProgress.Length)
                        continue;

                    var objProg = state.ObjectiveProgress[ope.ObjectiveIndex];
                    if (objProg.TargetLayer != ObjectiveTargetLayer.Tile)
                        continue;

                    var targetType = (ElementType)objProg.ElementType;

                    if (_destroyedBySimTime.TryGetValue(ope.SimulationTime, out var byType)
                        && byType.TryGetValue(targetType, out var queue)
                        && queue.Count > 0)
                    {
                        var (tileId, pos, mergeTarget) = queue.Dequeue();
                        _pendingFlies.Add(new Match3Bridge.FlyCollectionRequest
                        {
                            TileId = tileId,
                            ObjectiveIndex = ope.ObjectiveIndex,
                            ElementType = targetType,
                            SourceGridPosition = pos,
                            MergeTarget = mergeTarget,
                            FlyDelay = 0f,
                            NewCount = ope.CurrentCount,
                            TargetCount = ope.TargetCount
                        });
                    }
                }
            }

            // Post-process: assign stagger delays for merge groups
            if (_pendingFlies.Count > 0)
                AssignMergeDelays();

            // Fire events
            foreach (var fly in _pendingFlies)
                onCollected?.Invoke(fly);
        }

        private void AssignMergeDelays()
        {
            // Return index lists to pool
            foreach (var kvp in _mergeGroups)
            {
                kvp.Value.Clear();
                _mergeIndexListPool.Add(kvp.Value);
            }
            _mergeGroups.Clear();

            for (int i = 0; i < _pendingFlies.Count; i++)
            {
                var fly = _pendingFlies[i];
                if (fly.MergeTarget == null) continue;

                var target = fly.MergeTarget.Value;
                if (!_mergeGroups.TryGetValue(target, out var indices))
                {
                    indices = RentIndexList();
                    _mergeGroups[target] = indices;
                }
                indices.Add(i);
            }

            const float stagger = 0.08f;
            foreach (var kvp in _mergeGroups)
            {
                var target = kvp.Key;
                var indices = kvp.Value;
                if (indices.Count <= 1) continue;

                // Sort by distance to merge target (squared distance avoids Sqrt)
                indices.Sort((a, b) =>
                {
                    var da = GridDistanceSq(_pendingFlies[a].SourceGridPosition, target);
                    var db = GridDistanceSq(_pendingFlies[b].SourceGridPosition, target);
                    return da.CompareTo(db);
                });

                for (int j = 0; j < indices.Count; j++)
                {
                    var fly = _pendingFlies[indices[j]];
                    fly.FlyDelay = j * stagger;
                    _pendingFlies[indices[j]] = fly;
                }
            }
        }

        private static float GridDistanceSq(Position a, Position b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        private Dictionary<ElementType, Queue<(int, Position, Position?)>> RentInnerDict()
        {
            if (_innerDictPool.Count > 0)
            {
                var last = _innerDictPool[_innerDictPool.Count - 1];
                _innerDictPool.RemoveAt(_innerDictPool.Count - 1);
                return last;
            }
            return new Dictionary<ElementType, Queue<(int, Position, Position?)>>();
        }

        private Queue<(int, Position, Position?)> RentInnerQueue()
        {
            if (_innerQueuePool.Count > 0)
            {
                var last = _innerQueuePool[_innerQueuePool.Count - 1];
                _innerQueuePool.RemoveAt(_innerQueuePool.Count - 1);
                return last;
            }
            return new Queue<(int, Position, Position?)>();
        }

        private List<int> RentIndexList()
        {
            if (_mergeIndexListPool.Count > 0)
            {
                var last = _mergeIndexListPool[_mergeIndexListPool.Count - 1];
                _mergeIndexListPool.RemoveAt(_mergeIndexListPool.Count - 1);
                return last;
            }
            return new List<int>();
        }
    }
}
