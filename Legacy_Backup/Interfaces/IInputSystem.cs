using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Interfaces;

/// <summary>
/// Handles user input interactions and validation.
/// </summary>
public interface IInputSystem
{
    /// <summary>
    /// Checks if a position is valid for selection/interaction.
    /// </summary>
    bool IsValidPosition(in GameState state, Position p);

    /// <summary>
    /// Determines the target position for a swipe action.
    /// </summary>
    Position GetSwipeTarget(Position from, Direction direction);
}
