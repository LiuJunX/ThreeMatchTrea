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
    public long IssuedAtTick { get; init; }

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

        // Check tiles exist
        var fromTile = state.GetTile(From.X, From.Y);
        var toTile = state.GetTile(To.X, To.Y);
        return fromTile.Type != TileType.None && toTile.Type != TileType.None;
    }
}
