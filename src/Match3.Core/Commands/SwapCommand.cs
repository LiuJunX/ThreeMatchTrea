using System;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;

namespace Match3.Core.Commands;

/// <summary>
/// Command representing a tile swap action.
/// </summary>
public sealed record SwapCommand : IGameCommand
{
    /// <inheritdoc />
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public int IssuedAtTick { get; init; }

    /// <summary>Source position of the swap.</summary>
    public Position From { get; init; }

    /// <summary>Target position of the swap.</summary>
    public Position To { get; init; }

    /// <inheritdoc />
    public bool Execute(SimulationEngine engine)
    {
        if (engine == null) return false;
        return engine.ApplyMove(From, To);
    }

    /// <inheritdoc />
    public bool CanExecute(in GameState state)
    {
        // Check bounds
        if (!state.IsValid(From.X, From.Y) || !state.IsValid(To.X, To.Y))
            return false;

        // Check adjacency
        int dx = Math.Abs(From.X - To.X);
        int dy = Math.Abs(From.Y - To.Y);
        if ((dx + dy) != 1)
            return false;

        // FROM must have an interactable tile; TO can be a bare empty cell.
        if (!state.CanInteract(From))
            return false;
        if (!state.CanInteract(To) && !state.IsEmptySwapTarget(To))
            return false;

        // Multi-stage tiles or moving obstacles cannot be swapped
        var fromTile = state.GetTile(From.X, From.Y);
        if (fromTile.Stage > 1 || fromTile.Type.IsMovingObstacle())
            return false;
        var toTile = state.GetTile(To.X, To.Y);
        if (toTile.Type != ElementType.None && (toTile.Stage > 1 || toTile.Type.IsMovingObstacle()))
            return false;

        return true;
    }
}
