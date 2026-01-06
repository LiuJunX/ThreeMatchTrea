using System.Collections.Generic;
using Match3.Core.Structs;
using Match3.Core.Logic;

namespace Match3.Core;

/// <summary>
/// Orchestrates the game logic, handling moves, matches, and cascading effects.
/// Acts as the bridge between the GameState data and the IGameView presentation.
/// </summary>
public sealed class Match3Controller
{
    private GameState _state;
    private readonly IGameView _view;

    // Backward compatibility for GameBoard property if needed, but we should phase it out.
    // For now, we'll remove it or make it return a reconstruction if absolutely necessary.
    // But since we are strictly enforcing new design, we should remove 'Board' property if possible,
    // or return a snapshot view.
    // However, IGameView.RenderBoard expects a TileType[,]. GameState uses TileType[].
    // We need to bridge this gap.

    /// <summary>
    /// Initializes a new instance of the <see cref="Match3Controller"/> class.
    /// </summary>
    /// <param name="width">Width of the grid.</param>
    /// <param name="height">Height of the grid.</param>
    /// <param name="tileTypesCount">Number of distinct tile colors.</param>
    /// <param name="rng">Random number generator.</param>
    /// <param name="view">The view interface for rendering.</param>
    public Match3Controller(int width, int height, int tileTypesCount, IRandom rng, IGameView view)
    {
        _view = view;
        _state = new GameState(width, height, tileTypesCount, rng);
        GameRules.Initialize(ref _state);
        _view.RenderBoard(Get2DGridSnapshot());
    }

    /// <summary>
    /// Attempts to swap two tiles. If the swap results in a match, it is processed.
    /// If not, the swap is reverted.
    /// </summary>
    /// <param name="a">Position of the first tile.</param>
    /// <param name="b">Position of the second tile.</param>
    /// <returns>True if the swap resulted in a valid match; otherwise, false.</returns>
    public bool TrySwap(Position a, Position b)
    {
        // 1. Validation logic is now in GameRules
        if (!GameRules.IsValidMove(in _state, a, b))
        {
            _view.ShowSwap(a, b, false);
            return false;
        }

        // 2. Tentative Swap
        GameRules.Swap(ref _state, a, b);

        // 3. Check Outcome
        if (GameRules.HasMatches(in _state))
        {
            // Valid Move
            _view.ShowSwap(a, b, true);
            _view.RenderBoard(Get2DGridSnapshot());
            
            // Process the resulting matches and subsequent falls
            ResolveCascades();
            
            _view.RenderBoard(Get2DGridSnapshot());
            return true;
        }

        // Invalid Move - Revert
        GameRules.Swap(ref _state, a, b);
        _view.ShowSwap(a, b, false);
        _view.RenderBoard(Get2DGridSnapshot());
        return false;
    }

    private void ResolveCascades()
    {
        while (true)
        {
            var matches = GameRules.FindMatches(in _state);
            if (matches.Count == 0) break;

            // 1. Clear Matches
            _view.ShowMatches(matches);
            
            foreach (var p in matches)
            {
                _state.Set(p.X, p.Y, TileType.None);
            }
            
            _view.RenderBoard(Get2DGridSnapshot());

            // 2. Gravity
            var gravityMoves = GameRules.ApplyGravity(ref _state);
            _view.ShowGravity(gravityMoves);
            _view.RenderBoard(Get2DGridSnapshot());

            // 3. Refill
            var refillMoves = GameRules.Refill(ref _state);
            _view.ShowRefill(refillMoves);
            _view.RenderBoard(Get2DGridSnapshot());
        }
    }

    private TileType[,] Get2DGridSnapshot()
    {
        var copy = new TileType[_state.Width, _state.Height];
        for (var y = 0; y < _state.Height; y++)
        {
            for (var x = 0; x < _state.Width; x++)
            {
                copy[x, y] = _state.Get(x, y);
            }
        }
        return copy;
    }

    /// <summary>
    /// Sets a tile at a specific position.
    /// <b>WARNING:</b> This method is for testing and level editing purposes only.
    /// It bypasses all game rules and match checks.
    /// </summary>
    public void DebugSetTile(Position p, TileType t)
    {
        if (p.X >= 0 && p.X < _state.Width && p.Y >= 0 && p.Y < _state.Height)
        {
            _state.Set(p.X, p.Y, t);
        }
    }

    /// <summary>
    /// Gets the tile at a specific position.
    /// <b>WARNING:</b> This method is for testing purposes only.
    /// </summary>
    public TileType DebugGetTile(Position p)
    {
        if (p.X >= 0 && p.X < _state.Width && p.Y >= 0 && p.Y < _state.Height)
        {
            return _state.Get(p.X, p.Y);
        }
        return TileType.None;
    }
}

