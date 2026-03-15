using System;
using System.Collections.Generic;
using Match3.Core.Commands;
using Match3.Core.Config;
using Match3.Core.Models.Grid;

namespace Match3.Core.Replay;

/// <summary>
/// Records a game session for later replay.
/// Captures the initial state snapshot, random seed, and all player commands.
/// </summary>
public sealed class GameRecorder : IDisposable
{
    private readonly GameStateSnapshot _initialState;
    private readonly int _seed;
    private readonly int _tileTypesCount;
    private readonly LevelConfig? _levelConfig;
    private readonly CommandHistory _history;
    private readonly List<int> _bookmarks = new();
    private bool _isRecording;

    /// <summary>Whether the recorder is actively recording commands.</summary>
    public bool IsRecording => _isRecording;

    /// <summary>Number of commands recorded so far.</summary>
    public int CommandCount => _history.Count;

    /// <summary>The initial state snapshot captured at construction.</summary>
    public GameStateSnapshot InitialState => _initialState;

    /// <summary>The random seed used for the recorded session.</summary>
    public int Seed => _seed;

    /// <summary>
    /// Creates a new recorder capturing the initial state.
    /// </summary>
    /// <param name="initialState">The game state at the start of the session.</param>
    /// <param name="seed">The random seed for deterministic replay.</param>
    /// <param name="tileTypesCount">Number of tile types used.</param>
    /// <param name="levelConfig">Level config used for initialization (null for random boards).</param>
    public GameRecorder(in GameState initialState, int seed,
        int tileTypesCount = 6, LevelConfig? levelConfig = null)
    {
        _initialState = GameStateSnapshot.FromState(in initialState);
        _seed = seed;
        _tileTypesCount = tileTypesCount;
        _levelConfig = levelConfig;
        _history = new CommandHistory();
        _isRecording = true;
    }

    /// <summary>
    /// Records a player command. Ignored if recording is stopped.
    /// </summary>
    /// <param name="command">The command to record.</param>
    public void RecordCommand(IGameCommand command)
    {
        if (_isRecording)
            _history.Record(command);
    }

    /// <summary>
    /// Adds a bookmark at the specified tick for later reference.
    /// </summary>
    public void AddBookmark(int tick)
    {
        if (_isRecording)
            _bookmarks.Add(tick);
    }

    /// <summary>
    /// Completes the recording and returns a <see cref="GameRecording"/>.
    /// Stops further recording.
    /// </summary>
    /// <param name="durationTicks">Total simulation ticks elapsed.</param>
    /// <param name="finalScore">Final score achieved.</param>
    /// <param name="totalMoves">Total moves made.</param>
    /// <returns>A complete game recording.</returns>
    public GameRecording Complete(int durationTicks, int finalScore, int totalMoves)
    {
        _isRecording = false;
        return GameRecording.Create(
            _initialState,
            _seed,
            _history.GetCommands(),
            durationTicks,
            finalScore,
            totalMoves,
            _bookmarks.ToArray(),
            _tileTypesCount,
            _levelConfig);
    }

    public void Dispose()
    {
        _isRecording = false;
    }
}
