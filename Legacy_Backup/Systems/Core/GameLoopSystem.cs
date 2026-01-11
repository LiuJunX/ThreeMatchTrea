using System;
using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Interfaces;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Gravity;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.Core;

/// <summary>
/// Manages the core gameplay loop: Swapping, Matching, Gravity, Refilling.
/// Emits events to notify the outer world of state changes.
/// </summary>
public class GameLoopSystem
{
    private readonly IMatchFinder _matchFinder;
    private readonly IMatchProcessor _matchProcessor;
    private readonly IGravitySystem _gravitySystem;
    private readonly IPowerUpHandler _powerUpHandler;
    private readonly IScoreSystem _scoreSystem;

    // Async/Real-time state tracking
    private readonly HashSet<long> _lockedTileIds = new();
    
    private class SwapTask
    {
        public long IdA;
        public long IdB;
        public bool CheckMatch;
    }
    private readonly List<SwapTask> _activeSwaps = new();

    public event Action<IGameEvent>? OnEvent;

    public bool HasActiveTasks => _activeSwaps.Count > 0;

    public GameLoopSystem(
        IMatchFinder matchFinder,
        IMatchProcessor matchProcessor,
        IGravitySystem gravitySystem,
        IPowerUpHandler powerUpHandler,
        IScoreSystem scoreSystem)
    {
        _matchFinder = matchFinder;
        _matchProcessor = matchProcessor;
        _gravitySystem = gravitySystem;
        _powerUpHandler = powerUpHandler;
        _scoreSystem = scoreSystem;
    }

    public void Update(ref GameState state, AnimationSystem animationSystem)
    {
        // 1. Update Pending Swaps
        var foci = UpdateSwapTasks(ref state, animationSystem);

        // 2. Resolve Board State (Matches, Gravity, Refill)
        // Only resolve if we are not in the middle of a swap? 
        // Or continuously? Typically we resolve when things settle.
        // But in this implementation, ResolveMatches checks validity.
        ResolveMatches(ref state, foci);
        
        Pools.Release(foci);
    }

    public bool IsLocked(in GameState state, Position p)
    {
        var tile = state.GetTile(p.X, p.Y);
        return _lockedTileIds.Contains(tile.Id);
    }

    public bool TryStartSwap(ref GameState state, Position a, Position b)
    {
        if (IsLocked(in state, a) || IsLocked(in state, b)) return false;

        var tA = state.GetTile(a.X, a.Y);
        var tB = state.GetTile(b.X, b.Y);

        // Lock tiles
        Lock(tA.Id);
        Lock(tB.Id);

        // Perform logical swap immediately
        Swap(ref state, a, b);

        // Add task
        _activeSwaps.Add(new SwapTask { IdA = tA.Id, IdB = tB.Id, CheckMatch = true });

        // Notify
        OnEvent?.Invoke(new TileSwappedEvent(a, b, true));
        
        return true;
    }

    private List<Position> UpdateSwapTasks(ref GameState state, AnimationSystem animationSystem)
    {
        var validSwapPositions = Pools.ObtainList<Position>();
        for (int i = _activeSwaps.Count - 1; i >= 0; i--)
        {
            var task = _activeSwaps[i];
            
            var posA = FindTilePosition(in state, task.IdA);
            var posB = FindTilePosition(in state, task.IdB);

            if (!posA.IsValid || !posB.IsValid)
            {
                Unlock(task.IdA);
                Unlock(task.IdB);
                _activeSwaps.RemoveAt(i);
                continue;
            }

            // Check visual arrival
            if (animationSystem.IsVisualAtTarget(in state, posA) && animationSystem.IsVisualAtTarget(in state, posB))
            {
                if (task.CheckMatch)
                {
                    var matchesA = _matchFinder.FindMatchGroups(in state, new[] { posA });
                    var matchesB = _matchFinder.FindMatchGroups(in state, new[] { posB });
                    bool hasMatch = matchesA.Count > 0 || matchesB.Count > 0;
                    bool isSpecial = IsSpecialMove(in state, posA, posB);

                    if (hasMatch || isSpecial)
                    {
                        // Valid Move
                        if (isSpecial)
                        {
                            _powerUpHandler.ProcessSpecialMove(ref state, posA, posB, out int points);
                            state.Score += points;
                        }
                        
                        if (hasMatch)
                        {
                            validSwapPositions.Add(posA);
                            validSwapPositions.Add(posB);
                        }

                        Unlock(task.IdA);
                        Unlock(task.IdB);
                        _activeSwaps.RemoveAt(i);
                    }
                    else
                    {
                        // Invalid Move -> Revert
                        Swap(ref state, posA, posB);
                        task.CheckMatch = false;
                        
                        // Notify Revert
                        OnEvent?.Invoke(new TileSwappedEvent(posA, posB, false));
                    }

                    ClassicMatchFinder.ReleaseGroups(matchesA);
                    ClassicMatchFinder.ReleaseGroups(matchesB);
                }
                else
                {
                    // Revert Finished
                    Unlock(task.IdA);
                    Unlock(task.IdB);
                    _activeSwaps.RemoveAt(i);
                }
            }
        }
        return validSwapPositions;
    }

    private void ResolveMatches(ref GameState state, IEnumerable<Position>? foci = null)
    {
        var allGroups = _matchFinder.FindMatchGroups(in state, foci);
        var validGroups = Pools.ObtainList<MatchGroup>();
        
        try
        {
            foreach (var group in allGroups)
            {
                bool isGroupValid = true;
                foreach (var p in group.Positions)
                {
                    // Can't match locked or moving tiles (unless we want them to match mid-air, but usually not)
                    // Also check visual stability if strict
                    if (IsLocked(in state, p)) // || !IsStable(p)
                    {
                        isGroupValid = false;
                        break;
                    }
                }
                if (isGroupValid)
                {
                    validGroups.Add(group);
                }
            }

            if (validGroups.Count == 0 && !HasHoles(in state)) return;

            // Process Matches
            if (validGroups.Count > 0)
            {
                int points = _matchProcessor.ProcessMatches(ref state, validGroups);
                state.Score += points;
                
                OnEvent?.Invoke(new MatchesFoundEvent(validGroups, points));
            }

            // Apply Gravity
            var gravityMoves = _gravitySystem.ApplyGravity(ref state);
            if (gravityMoves.Count > 0)
            {
                OnEvent?.Invoke(new GravityAppliedEvent(gravityMoves));
            }
            Pools.Release(gravityMoves);
            
            // Refill
            var refillMoves = _gravitySystem.Refill(ref state);
            if (refillMoves.Count > 0)
            {
                OnEvent?.Invoke(new BoardRefilledEvent(refillMoves));
            }
            Pools.Release(refillMoves);
        }
        finally
        {
            ClassicMatchFinder.ReleaseGroups(allGroups);
            Pools.Release(validGroups);
        }
    }

    private void Lock(long id) => _lockedTileIds.Add(id);
    private void Unlock(long id) => _lockedTileIds.Remove(id);

    private Position FindTilePosition(in GameState state, long id)
    {
        for (int i = 0; i < state.Grid.Length; i++)
        {
            if (state.Grid[i].Id == id)
            {
                return new Position(i % state.Width, i / state.Width);
            }
        }
        return Position.Invalid;
    }

    private void Swap(ref GameState state, Position a, Position b)
    {
        var idxA = state.Index(a.X, a.Y);
        var idxB = state.Index(b.X, b.Y);
        var temp = state.Grid[idxA];
        state.Grid[idxA] = state.Grid[idxB];
        state.Grid[idxB] = temp;
    }

    private bool IsSpecialMove(in GameState state, Position a, Position b)
    {
        var t1 = state.GetTile(a.X, a.Y);
        var t2 = state.GetTile(b.X, b.Y);
        
        if (t1.Type == TileType.Rainbow || t2.Type == TileType.Rainbow) return true;
        
        bool isBombCombo = t1.Bomb != BombType.None && t2.Bomb != BombType.None;
        
        return isBombCombo;
    }

    private bool HasHoles(in GameState state)
    {
        for (int i = 0; i < state.Grid.Length; i++)
        {
            if (state.Grid[i].Type == TileType.None) return true;
        }
        return false;
    }
}
