using System.Collections.Generic;

namespace Match3.Core.Commands;

/// <summary>
/// Records and manages a history of game commands for replay.
/// Thread-safe for concurrent recording and reading.
/// </summary>
public sealed class CommandHistory
{
    private readonly object _lock = new();
    private readonly List<IGameCommand> _commands = new();
    private bool _isRecording = true;

    /// <summary>
    /// Gets whether the history is currently recording commands.
    /// </summary>
    public bool IsRecording
    {
        get { lock (_lock) return _isRecording; }
        set { lock (_lock) _isRecording = value; }
    }

    /// <summary>
    /// Gets the number of recorded commands.
    /// </summary>
    public int Count
    {
        get { lock (_lock) return _commands.Count; }
    }

    /// <summary>
    /// Records a command to the history.
    /// </summary>
    /// <param name="command">The command to record.</param>
    public void Record(IGameCommand command)
    {
        if (command == null) return;

        lock (_lock)
        {
            if (_isRecording)
            {
                _commands.Add(command);
            }
        }
    }

    /// <summary>
    /// Gets a read-only snapshot of all recorded commands.
    /// </summary>
    /// <returns>A list of all recorded commands.</returns>
    public IReadOnlyList<IGameCommand> GetCommands()
    {
        lock (_lock)
        {
            return _commands.ToArray();
        }
    }

    /// <summary>
    /// Gets the command at the specified index.
    /// </summary>
    /// <param name="index">The index of the command.</param>
    /// <returns>The command at the index, or null if out of range.</returns>
    public IGameCommand? GetCommand(int index)
    {
        lock (_lock)
        {
            if (index >= 0 && index < _commands.Count)
                return _commands[index];
            return null;
        }
    }

    /// <summary>
    /// Clears all recorded commands.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _commands.Clear();
        }
    }
}
