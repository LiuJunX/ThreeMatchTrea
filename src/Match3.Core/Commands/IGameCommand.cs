using System;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;

namespace Match3.Core.Commands;

/// <summary>
/// Represents a player command that can be executed, recorded, and replayed.
/// All game inputs must go through commands for deterministic replay.
/// </summary>
public interface IGameCommand
{
    /// <summary>Unique identifier for this command instance.</summary>
    Guid Id { get; }

    /// <summary>Simulation tick when this command was issued.</summary>
    long IssuedAtTick { get; }

    /// <summary>
    /// Executes the command on the simulation engine.
    /// </summary>
    /// <param name="engine">The simulation engine to execute on.</param>
    /// <returns>True if the command was executed successfully.</returns>
    bool Execute(SimulationEngine engine);

    /// <summary>
    /// Checks if the command can be executed in the current state.
    /// </summary>
    /// <param name="state">The current game state.</param>
    /// <returns>True if the command is valid for execution.</returns>
    bool CanExecute(in GameState state);
}
