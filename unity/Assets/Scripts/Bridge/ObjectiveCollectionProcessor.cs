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
            _destroyedBySimTime.Clear();
            _pendingFlies.Clear();

            foreach (var evt in events)
            {
                if (evt is TileDestroyedEvent tde && tde.Reason == DestroyReason.Match)
                {
                    if (!_destroyedBySimTime.TryGetValue(tde.SimulationTime, out var byType))
                    {
                        byType = new Dictionary<ElementType, Queue<(int, Position, Position?)>>();
                        _destroyedBySimTime[tde.SimulationTime] = byType;
                    }
                    if (!byType.TryGetValue(tde.Type, out var queue))
                    {
                        queue = new Queue<(int, Position, Position?)>();
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
            // Group merge flies by MergeTarget, sort by distance, assign delays
            var mergeGroups = new Dictionary<Position, List<int>>();

            for (int i = 0; i < _pendingFlies.Count; i++)
            {
                var fly = _pendingFlies[i];
                if (fly.MergeTarget == null) continue;

                var target = fly.MergeTarget.Value;
                if (!mergeGroups.TryGetValue(target, out var indices))
                {
                    indices = new List<int>();
                    mergeGroups[target] = indices;
                }
                indices.Add(i);
            }

            const float stagger = 0.08f;
            foreach (var kvp in mergeGroups)
            {
                var target = kvp.Key;
                var indices = kvp.Value;
                if (indices.Count <= 1) continue;

                // Sort by distance to merge target
                indices.Sort((a, b) =>
                {
                    var da = GridDistance(_pendingFlies[a].SourceGridPosition, target);
                    var db = GridDistance(_pendingFlies[b].SourceGridPosition, target);
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

        private static float GridDistance(Position a, Position b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}
