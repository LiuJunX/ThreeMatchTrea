using System;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;

namespace Match3.Core.Commands;

/// <summary>
/// Command representing a tile tap action (for activating power-ups).
/// </summary>
public sealed record TapCommand : IGameCommand
{
    /// <inheritdoc />
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public long IssuedAtTick { get; init; }

    /// <summary>Position of the tap.</summary>
    public Position Position { get; init; }

    /// <inheritdoc />
    public bool Execute(SimulationEngine engine)
    {
        if (engine == null) return false;
        engine.HandleTap(Position);
        return true;
    }

    /// <inheritdoc />
    public bool CanExecute(in GameState state)
    {
        // Check bounds
        if (!state.IsValid(Position.X, Position.Y))
            return false;

        // Check tile is a tappable power-up
        var tile = state.GetTile(Position.X, Position.Y);
        return tile.Bomb != BombType.None;
    }
}
