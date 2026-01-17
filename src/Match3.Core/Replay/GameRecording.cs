using System;
using System.Collections.Generic;
using Match3.Core.Commands;

namespace Match3.Core.Replay;

/// <summary>
/// Complete recording of a game session for replay.
/// Contains initial state, random seed, and all player commands.
/// </summary>
public sealed record GameRecording
{
    /// <summary>Version of the recording format.</summary>
    public int Version { get; init; } = 1;

    /// <summary>Timestamp when recording started.</summary>
    public DateTimeOffset RecordedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Initial game state snapshot.</summary>
    public GameStateSnapshot InitialState { get; init; } = new();

    /// <summary>Random seed used for deterministic replay.</summary>
    public int RandomSeed { get; init; }

    /// <summary>All recorded player commands.</summary>
    public IReadOnlyList<IGameCommand> Commands { get; init; } = Array.Empty<IGameCommand>();

    /// <summary>Duration of the recording in ticks.</summary>
    public long DurationTicks { get; init; }

    /// <summary>Final score achieved.</summary>
    public int FinalScore { get; init; }

    /// <summary>Total moves made.</summary>
    public int TotalMoves { get; init; }

    /// <summary>
    /// Creates a recording from a game session.
    /// </summary>
    /// <param name="initialState">Snapshot of the initial state.</param>
    /// <param name="seed">Random seed used.</param>
    /// <param name="commands">All recorded commands.</param>
    /// <param name="durationTicks">Total ticks elapsed.</param>
    /// <param name="finalScore">Final score.</param>
    /// <param name="totalMoves">Total moves made.</param>
    public static GameRecording Create(
        GameStateSnapshot initialState,
        int seed,
        IReadOnlyList<IGameCommand> commands,
        long durationTicks,
        int finalScore,
        int totalMoves)
    {
        return new GameRecording
        {
            InitialState = initialState,
            RandomSeed = seed,
            Commands = commands,
            DurationTicks = durationTicks,
            FinalScore = finalScore,
            TotalMoves = totalMoves
        };
    }
}
