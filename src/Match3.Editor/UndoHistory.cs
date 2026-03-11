using System.Collections.Generic;
using Match3.Core.Config;

namespace Match3.Editor;

/// <summary>
/// Snapshot-based undo/redo history.
/// Each entry is a full DeepCopy of LevelConfig at that point in time.
/// Pointer always points at the "current" state.
/// </summary>
public class UndoHistory
{
    private const int MaxSnapshots = 20;
    private readonly List<LevelConfig> _snapshots = new List<LevelConfig>();
    private int _pointer = -1;

    public bool CanUndo => _pointer > 0;
    public bool CanRedo => _pointer < _snapshots.Count - 1;
    public int Count => _snapshots.Count;

    /// <summary>
    /// Record the current state as a new snapshot.
    /// Truncates any redo history beyond the current pointer.
    /// </summary>
    public void Record(LevelConfig config)
    {
        // Truncate forward history (discard redo branch)
        if (_pointer < _snapshots.Count - 1)
            _snapshots.RemoveRange(_pointer + 1, _snapshots.Count - _pointer - 1);

        _snapshots.Add(config.DeepCopy());

        // Cap at max size, dropping oldest
        if (_snapshots.Count > MaxSnapshots)
        {
            _snapshots.RemoveAt(0);
        }

        _pointer = _snapshots.Count - 1;
    }

    /// <summary>
    /// Move pointer back and return the previous state.
    /// Returns null if nothing to undo.
    /// </summary>
    public LevelConfig? Undo()
    {
        if (!CanUndo) return null;
        return _snapshots[--_pointer].DeepCopy();
    }

    /// <summary>
    /// Move pointer forward and return the next state.
    /// Returns null if nothing to redo.
    /// </summary>
    public LevelConfig? Redo()
    {
        if (!CanRedo) return null;
        return _snapshots[++_pointer].DeepCopy();
    }

    public void Clear()
    {
        _snapshots.Clear();
        _pointer = -1;
    }
}
