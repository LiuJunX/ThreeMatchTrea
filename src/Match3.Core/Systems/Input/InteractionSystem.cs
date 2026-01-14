using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Utility;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.View;
using System;

namespace Match3.Core.Systems.Input;

/// <summary>
/// Handles user input interactions (Taps, Swipes) and manages selection state.
/// </summary>
public class InteractionSystem : IInteractionSystem
{
    private readonly IGameLogger _logger;

    public string StatusMessage { get; private set; } = "Ready";

    public InteractionSystem(IGameLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Handles a tap interaction. Returns a Move if a valid move was initiated.
    /// </summary>
    public bool TryHandleTap(ref GameState state, Position p, bool isBoardInteractive, out Move? move)
    {
        move = null;

        if (!state.IsValid(p)) return false;
        if (!isBoardInteractive) return false;

        _logger.LogInfo($"OnTap: {p}");

        if (state.SelectedPosition == Position.Invalid)
        {
            state.SelectedPosition = p;
            StatusMessage = "Select destination";
            return false;
        }
        else
        {
            if (state.SelectedPosition == p)
            {
                state.SelectedPosition = Position.Invalid;
                StatusMessage = "Selection Cleared";
                return false;
            }
            else
            {
                if (IsNeighbor(state.SelectedPosition, p))
                {
                    move = new Move(state.SelectedPosition, p);
                    state.SelectedPosition = Position.Invalid;
                    StatusMessage = "Swapping...";
                    return true;
                }
                else
                {
                    // Select the new position instead
                    state.SelectedPosition = p;
                    StatusMessage = "Select destination";
                    return false;
                }
            }
        }
    }

    /// <summary>
    /// Handles a swipe interaction. Returns a Move if a valid move was initiated.
    /// </summary>
    public bool TryHandleSwipe(ref GameState state, Position from, Direction direction, bool isBoardInteractive, out Move? move)
    {
        move = null;

        if (!state.IsValid(from)) return false;
        if (!isBoardInteractive) return false;

        Position to = from.GetNeighbor(direction);
        if (!state.IsValid(to)) return false;
        
        // Swipe doesn't use selection state, but clears it if any
        state.SelectedPosition = Position.Invalid;

        move = new Move(from, to);
        StatusMessage = "Swapping...";
        return true;
    }

    private bool IsNeighbor(Position a, Position b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;
    }
}
