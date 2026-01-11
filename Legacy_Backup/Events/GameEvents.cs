using System.Collections.Generic;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;

namespace Match3.Core.Events;

/// <summary>
/// Triggered when two tiles are swapped (either by user or auto-logic).
/// </summary>
public readonly struct TileSwappedEvent : IGameEvent
{
    public readonly Position PositionA;
    public readonly Position PositionB;
    public readonly bool IsSuccessful;

    public TileSwappedEvent(Position a, Position b, bool success)
    {
        PositionA = a;
        PositionB = b;
        IsSuccessful = success;
    }
}

/// <summary>
/// Triggered when matches are identified and processed.
/// </summary>
public readonly struct MatchesFoundEvent : IGameEvent
{
    public readonly IReadOnlyCollection<MatchGroup> Matches;
    public readonly int TotalScore;

    public MatchesFoundEvent(IReadOnlyCollection<MatchGroup> matches, int score)
    {
        Matches = matches;
        TotalScore = score;
    }
}

/// <summary>
/// Triggered when gravity causes tiles to fall.
/// </summary>
public readonly struct GravityAppliedEvent : IGameEvent
{
    public readonly IEnumerable<TileMove> Moves;

    public GravityAppliedEvent(IEnumerable<TileMove> moves)
    {
        Moves = moves;
    }
}

/// <summary>
/// Triggered when new tiles are generated to fill empty spaces.
/// </summary>
public readonly struct BoardRefilledEvent : IGameEvent
{
    public readonly IEnumerable<TileMove> NewTiles;

    public BoardRefilledEvent(IEnumerable<TileMove> newTiles)
    {
        NewTiles = newTiles;
    }
}
