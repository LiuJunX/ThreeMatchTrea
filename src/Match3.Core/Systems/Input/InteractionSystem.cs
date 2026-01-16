using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Utility;
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

        // Check if the position can be interacted with (no blocking cover)
        if (!state.CanInteract(p))
        {
            StatusMessage = "Blocked by cover";
            return false;
        }

        _logger.LogInfo("OnTap: {0}", p);

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
                    // Check if both positions can be interacted with
                    if (!state.CanInteract(state.SelectedPosition))
                    {
                        state.SelectedPosition = p;
                        StatusMessage = "Previous selection blocked";
                        return false;
                    }

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

        // Check if the 'from' position can be interacted with
        if (!state.CanInteract(from))
        {
            StatusMessage = "Blocked by cover";
            return false;
        }

        Position to = from.GetNeighbor(direction);
        if (!state.IsValid(to)) return false;

        // Check if the 'to' position can be interacted with
        if (!state.CanInteract(to))
        {
            StatusMessage = "Target blocked by cover";
            return false;
        }

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
