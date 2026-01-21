using System;
using System.Numerics;
using Match3.Core.Events;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching;

namespace Match3.Core.Systems.Swap;

/// <summary>
/// Shared implementation of swap operations.
/// Used by both Match3Engine and SimulationEngine through ISwapContext strategy.
/// </summary>
public sealed class SwapOperations : ISwapOperations
{
    private readonly IMatchFinder _matchFinder;
    private readonly ISwapContext _context;

    public SwapOperations(IMatchFinder matchFinder, ISwapContext context)
    {
        _matchFinder = matchFinder ?? throw new ArgumentNullException(nameof(matchFinder));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public void SwapTiles(ref GameState state, Position a, Position b)
    {
        var idxA = a.Y * state.Width + a.X;
        var idxB = b.Y * state.Width + b.X;

        var temp = state.Grid[idxA];
        state.Grid[idxA] = state.Grid[idxB];
        state.Grid[idxB] = temp;

        // Sync position based on strategy
        if (_context.SyncPositionOnSwap)
        {
            state.Grid[idxA].Position = new Vector2(a.X, a.Y);
            state.Grid[idxB].Position = new Vector2(b.X, b.Y);
        }
    }

    public bool HasMatch(in GameState state, Position p)
    {
        return _matchFinder.HasMatchAt(in state, p);
    }

    public bool ValidatePendingMove(
        ref GameState state,
        ref PendingMoveState pending,
        float deltaTime,
        long tick,
        float simTime,
        IEventCollector events)
    {
        if (!pending.NeedsValidation)
            return true;

        // Accumulate animation time
        pending.AnimationTime += deltaTime;

        // Check if animation is complete
        if (!_context.IsSwapAnimationComplete(in state, pending.From, pending.To, pending.AnimationTime))
            return false;

        // Animation complete - validate the move
        pending.NeedsValidation = false;

        // If no match was found, revert the swap
        if (!pending.HadMatch)
        {
            SwapTiles(ref state, pending.From, pending.To);
            _context.EmitRevertEvent(in state, pending.From, pending.To, tick, simTime, events);
        }
        else
        {
            // Valid move - increment move count
            state.MoveCount++;
        }

        // Clear pending state
        pending = PendingMoveState.None;
        return true;
    }
}
