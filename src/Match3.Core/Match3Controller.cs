using System;
using System.Collections.Generic;
using System.Numerics;
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
    
    // Animation constants
    private const float SwapSpeed = 10.0f; // Tiles per second
    private const float GravitySpeed = 20.0f; // Tiles per second
    private const float Epsilon = 0.01f;

    private enum ControllerState
    {
        Idle,
        AnimateSwap,
        Resolving,
        AnimateRevert
    }
    
    private ControllerState _currentState = ControllerState.Idle;
    private Position _swapA;
    private Position _swapB;

    public GameState State => _state; // Expose state for View
    public bool IsIdle => _currentState == ControllerState.Idle;
    public Position SelectedPosition { get; private set; } = Position.Invalid;
    public string StatusMessage { get; private set; } = "Ready";

    public Match3Controller(int width, int height, int tileTypesCount, IRandom rng, IGameView view)
    {
        _view = view;
        _state = new GameState(width, height, tileTypesCount, rng);
        GameRules.Initialize(ref _state);
    }

    /// <summary>
    /// Handles a tap/click interaction on a specific tile.
    /// </summary>
    public void OnTap(Position p)
    {
        if (!IsIdle) return;
        if (!IsValidPosition(p)) return;

        if (SelectedPosition == Position.Invalid)
        {
            // Select first tile
            SelectedPosition = p;
            StatusMessage = "Select destination";
        }
        else
        {
            if (SelectedPosition == p)
            {
                // Deselect
                SelectedPosition = Position.Invalid;
                StatusMessage = "Selection Cleared";
            }
            else
            {
                // Try swap
                bool success = TrySwapInternal(SelectedPosition, p);
                if (success)
                {
                     SelectedPosition = Position.Invalid;
                     StatusMessage = "Swapping...";
                }
                else
                {
                    // If neighbors but invalid move -> Invalid Move
                    // If not neighbors -> Select new tile
                    if (IsNeighbor(SelectedPosition, p))
                    {
                        StatusMessage = "Invalid Move";
                        SelectedPosition = Position.Invalid;
                    }
                    else
                    {
                        SelectedPosition = p;
                        StatusMessage = "Select destination";
                    }
                }
            }
        }
    }

    /// <summary>
    /// Handles a swipe interaction originating from a specific tile.
    /// </summary>
    public void OnSwipe(Position from, Direction direction)
    {
        if (!IsIdle) return;
        if (!IsValidPosition(from)) return;

        Position to = GetNeighbor(from, direction);
        if (!IsValidPosition(to)) return;

        // Swipe overrides selection
        SelectedPosition = Position.Invalid;

        bool success = TrySwapInternal(from, to);
        if (success)
        {
            StatusMessage = "Swapping...";
        }
        else
        {
            StatusMessage = "Invalid Move";
        }
    }

    private bool IsValidPosition(Position p)
    {
        return p.X >= 0 && p.X < _state.Width && p.Y >= 0 && p.Y < _state.Height;
    }

    private bool IsNeighbor(Position a, Position b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;
    }

    private Position GetNeighbor(Position p, Direction dir)
    {
        return dir switch
        {
            Direction.Up => new Position(p.X, p.Y - 1),
            Direction.Down => new Position(p.X, p.Y + 1),
            Direction.Left => new Position(p.X - 1, p.Y),
            Direction.Right => new Position(p.X + 1, p.Y),
            _ => p
        };
    }

    /// <summary>
    /// Advances the simulation by dt seconds.
    /// MUST be called by the game loop (e.g. from the View).
    /// </summary>
    public void Update(float dt)
    {
        bool isStable = AnimateTiles(dt);

        if (!isStable) return;

        switch (_currentState)
        {
            case ControllerState.AnimateSwap:
                if (GameRules.HasMatches(in _state))
                {
                    _view.ShowSwap(_swapA, _swapB, true);
                    _currentState = ControllerState.Resolving;
                    ResolveStep(_swapB);
                }
                else
                {
                    // Invalid move, revert
                    GameRules.Swap(ref _state, _swapA, _swapB);
                    _currentState = ControllerState.AnimateRevert;
                }
                break;

            case ControllerState.AnimateRevert:
                _view.ShowSwap(_swapA, _swapB, false);
                _currentState = ControllerState.Idle;
                break;

            case ControllerState.Resolving:
                if (!ResolveStep())
                {
                    _currentState = ControllerState.Idle;
                }
                break;
                
            case ControllerState.Idle:
                // Do nothing
                break;
        }
    }

    private bool ResolveStep(Position? focus = null)
    {
        var groups = GameRules.FindMatchGroups(in _state, focus);
        if (groups.Count == 0) return false;

        // Flatten for View
        var allPositions = new HashSet<Position>();
        foreach(var g in groups) 
            foreach(var p in g.Positions) allPositions.Add(p);

        _view.ShowMatches(allPositions);
        
        // 1. Process matches (Clear + Spawn Bombs)
        GameRules.ProcessMatches(ref _state, groups);

        // 2. Gravity & Refill (Logic)
        // These methods now move Tile structs, preserving their old visual positions
        // so the animation system will see them as "out of place" and move them.
        var gravityMoves = GameRules.ApplyGravity(ref _state);
        _view.ShowGravity(gravityMoves);
        
        var refillMoves = GameRules.Refill(ref _state);
        _view.ShowRefill(refillMoves);
        
        return true;
    }

    private bool AnimateTiles(float dt)
    {
        bool allStable = true;
        for (int i = 0; i < _state.Grid.Length; i++)
        {
            // Use ref to modify struct in array directly
            ref var tile = ref _state.Grid[i];
            if (tile.Type == TileType.None) continue;

            int x = i % _state.Width;
            int y = i / _state.Width;
            var targetPos = new Vector2(x, y);

            if (Vector2.DistanceSquared(tile.Position, targetPos) > Epsilon * Epsilon)
            {
                allStable = false;
                var dir = targetPos - tile.Position;
                float dist = dir.Length();
                float move = GravitySpeed * dt;
                
                if (move >= dist)
                {
                    tile.Position = targetPos;
                }
                else
                {
                    tile.Position += Vector2.Normalize(dir) * move;
                }
            }
            else
            {
                tile.Position = targetPos; // Snap
            }
        }
        return allStable;
    }

    private bool TrySwapInternal(Position a, Position b)
    {
        if (_currentState != ControllerState.Idle) return false;
        
        if (!GameRules.IsValidMove(in _state, a, b)) return false;

        _swapA = a;
        _swapB = b;
        
        GameRules.Swap(ref _state, a, b);
        // After swap, tile at A has VisPos of B, tile at B has VisPos of A.
        // Animation loop will fix this.
        
        _currentState = ControllerState.AnimateSwap;
        return true;
    }

    // Helper for tests/debug
    public void DebugSetTile(Position p, TileType t)
    {
        _state.SetTile(p.X, p.Y, new Tile(t, p.X, p.Y));
    }

    public bool TryMakeRandomMove()
    {
        if (!IsIdle) return false;

        // Naive search for any valid move
        for (int y = 0; y < _state.Height; y++)
        {
            for (int x = 0; x < _state.Width; x++)
            {
                var p = new Position(x, y);

                // Try Right
                var right = new Position(x + 1, y);
                if (IsValidPosition(right))
                {
                    if (CheckAndPerformMove(p, right)) return true;
                }

                // Try Down
                var down = new Position(x, y + 1);
                if (IsValidPosition(down))
                {
                    if (CheckAndPerformMove(p, down)) return true;
                }
            }
        }
        
        return false;
    }

    private bool CheckAndPerformMove(Position a, Position b)
    {
        // Simulate
        GameRules.Swap(ref _state, a, b);
        bool hasMatch = GameRules.HasMatches(in _state);

        // Also check for special combos (Rainbow, Bomb+Bomb)
        // Note: After swap, the tile that was at A is now at B, and vice versa.
        var t1 = _state.GetTile(b.X, b.Y); // Original A
        var t2 = _state.GetTile(a.X, a.Y); // Original B

        bool isSpecial = (t1.Bomb != BombType.None && t2.Bomb != BombType.None) ||
                         (t1.Type == TileType.Rainbow || t2.Type == TileType.Rainbow);

        GameRules.Swap(ref _state, a, b); // Swap back

        if (hasMatch || isSpecial)
        {
            // Execute real move
            // We know it's valid, so TrySwapInternal will proceed to AnimateSwap
            // and Update will eventually resolve it.
            TrySwapInternal(a, b);
            StatusMessage = "Auto Move...";
            return true;
        }
        return false;
    }
}
