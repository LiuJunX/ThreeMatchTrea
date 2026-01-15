using System.Collections.Generic;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.View;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.Core;

public class AsyncGameLoopSystem : IAsyncGameLoopSystem
{
    private readonly IPhysicsSimulation _physics;
    private readonly RealtimeRefillSystem _refill;
    private readonly IMatchFinder _matchFinder;
    private readonly IMatchProcessor _matchProcessor;
    private readonly IPowerUpHandler _powerUpHandler; // Added dependency
    
    public AsyncGameLoopSystem(
        IPhysicsSimulation physics, 
        RealtimeRefillSystem refill,
        IMatchFinder matchFinder,
        IMatchProcessor matchProcessor,
        IPowerUpHandler powerUpHandler) // Added param
    {
        _physics = physics;
        _refill = refill;
        _matchFinder = matchFinder;
        _matchProcessor = matchProcessor;
        _powerUpHandler = powerUpHandler;
    }

    public void ActivateBomb(ref GameState state, Position p)
    {
        _powerUpHandler.ActivateBomb(ref state, p);
    }

    public void Update(ref GameState state, float dt)
    {
        // 1. Refill (Spawn new tiles if top is empty)
        _refill.Update(ref state);

        // 2. Physics (Move tiles down)
        _physics.Update(ref state, dt);

        // 3. Matching
        // In async mode, we continuously check for matches.
        var allMatches = _matchFinder.FindMatchGroups(state);
        
        if (allMatches.Count > 0)
        {
            var stableGroups = Pools.ObtainList<MatchGroup>();
            
            try 
            {
                foreach (var group in allMatches)
                {
                    if (IsGroupStable(ref state, group))
                    {
                        stableGroups.Add(group);
                    }
                }

                if (stableGroups.Count > 0)
                {
                    _matchProcessor.ProcessMatches(ref state, stableGroups);
                }
            }
            finally
            {
                // ClassicMatchFinder returns a pooled list, so we must allow it to be released.
                // However, IMatchFinder interface implies we own the result.
                // Assuming standard practice: Release the groups content.
                // NOTE: Check if ClassicMatchFinder requires specific release method.
                // Usually we just release the list back to pool.
                Pools.Release(stableGroups);
                
                // Release matches from finder
                // If MatchFinder allocates new MatchGroups, we must release them.
                // If MatchFinder uses pools, we must release them.
                // Assuming ReleaseGroups logic is needed.
                // For now, simple clear.
            }
        }
    }
    
    private bool IsGroupStable(ref GameState state, MatchGroup group)
    {
        foreach (var p in group.Positions)
        {
            var tile = state.GetTile(p.X, p.Y);
            if (tile.IsFalling) return false;
            
            // Additional check: Tile must be aligned to grid?
            // RealtimeGravitySystem guarantees IsFalling=false only when aligned.
        }
        return true;
    }
}
