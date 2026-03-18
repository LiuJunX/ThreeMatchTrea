using System;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Undo;

namespace Match3.Core.Commands;

/// <summary>
/// Command representing a player undo action.
/// Restores the engine to the most recent stable checkpoint via <see cref="UndoSystem"/>.
/// </summary>
/// <remarks>
/// <para>
/// Unlike other commands, UndoCommand is a class (not a record) because it holds a
/// mutable <see cref="UndoSystem"/> reference, which breaks record value semantics.
/// </para>
/// <para>
/// <b>Replay exclusion:</b> UndoCommand must NOT be recorded in the replay stream.
/// It is a meta-action that pops the command history stack rather than appending to it.
/// Recording it would break replay determinism because the undo stack state is not
/// captured in the replay file.
/// </para>
/// </remarks>
public sealed class UndoCommand : IGameCommand
{
    /// <inheritdoc />
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public int IssuedAtTick { get; init; }

    private readonly UndoSystem _undoSystem;

    public UndoCommand(UndoSystem undoSystem)
    {
        _undoSystem = undoSystem ?? throw new ArgumentNullException(nameof(undoSystem));
    }

    /// <inheritdoc />
    public bool Execute(SimulationEngine engine)
    {
        if (engine == null) return false;
        return _undoSystem.Undo(engine) != null;
    }

    /// <inheritdoc />
    public bool CanExecute(in GameState state)
    {
        return _undoSystem.CanUndo;
    }
}
