using System;
using Match3.Core.Config;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.View;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Utility;
using Match3.Random;

using System.Collections.Generic;
using Match3.Core.Models.Input;

namespace Match3.Core;

/// <summary>
/// The facade and entry point for the Match3 core logic.
/// Coordinators systems and manages the GameState.
/// </summary>
public sealed class Match3Engine : IDisposable
{
    private GameState _state;
    private readonly Match3Config _config;
    private readonly IGameView _view;
    private readonly IGameLogger _logger;

    // Systems
    private readonly IInteractionSystem _interactionSystem;
    private readonly IAnimationSystem _animationSystem;
    private readonly IAsyncGameLoopSystem _gameLoopSystem;
    private readonly IMatchFinder _matchFinder;
    private readonly IBotSystem _botSystem;
    
    // Input Queue
    private readonly Queue<InputIntent> _inputQueue = new();

    // Pending move for swap animation validation
    private Move? _pendingMove;
    private bool _pendingMoveNeedsValidation;

    public GameState State => _state;
    public Position SelectedPosition => _state.SelectedPosition;
    public string StatusMessage => _interactionSystem.StatusMessage;
    public bool IsIdle => _animationSystem.IsVisuallyStable;

    public Match3Engine(
        Match3Config config,
        IRandom rng,
        IGameView view,
        IGameLogger logger,
        IAsyncGameLoopSystem gameLoopSystem,
        IInteractionSystem interactionSystem,
        IAnimationSystem animationSystem,
        IBoardInitializer boardInitializer,
        IMatchFinder matchFinder,
        IBotSystem botSystem,
        LevelConfig? levelConfig = null)
    {
        _config = config;
        _view = view;
        _logger = logger;
        _gameLoopSystem = gameLoopSystem;
        _interactionSystem = interactionSystem;
        _animationSystem = animationSystem;
        _matchFinder = matchFinder;
        _botSystem = botSystem;

        // Initialize State
        _state = new GameState(_config.Width, _config.Height, _config.TileTypesCount, rng);
        boardInitializer.Initialize(ref _state, levelConfig);
        
        _logger.LogInfo($"Match3Engine initialized with size {_config.Width}x{_config.Height}");
    }

    public void Dispose()
    {
        _inputQueue.Clear();
    }

    public void EnqueueIntent(InputIntent intent)
    {
        _inputQueue.Enqueue(intent);
    }

    public void Update(float dt)
    {
        ProcessInput();
        _animationSystem.Animate(ref _state, dt);

        // Check pending move validation after animation completes
        ValidatePendingMove();

        _gameLoopSystem.Update(ref _state, dt);
    }

    private void ValidatePendingMove()
    {
        if (!_pendingMoveNeedsValidation || !_pendingMove.HasValue)
            return;

        var move = _pendingMove.Value;

        // Wait until both tiles have finished animating to their new positions
        if (!_animationSystem.IsVisualAtTarget(in _state, move.From) ||
            !_animationSystem.IsVisualAtTarget(in _state, move.To))
            return;

        // Animation complete, now check for matches
        _pendingMoveNeedsValidation = false;

        if (!HasMatch(move.From) && !HasMatch(move.To))
        {
            // Invalid swap - swap back (this will trigger another animation)
            SwapTiles(ref _state, move.From, move.To);
        }

        _pendingMove = null;
    }

    private void ProcessInput()
    {
        while (_inputQueue.TryDequeue(out var intent))
        {
            switch (intent)
            {
                case TapIntent tap:
                    OnTap(tap.Position);
                    break;
                case SwipeIntent swipe:
                    OnSwipe(swipe.From, swipe.Direction);
                    break;
            }
        }
    }

    public void OnTap(Position p)
    {
        bool isInteractive = true; // Could be _animationSystem.IsVisuallyStable if strict
        
        // Check for Bomb
        var tile = _state.GetTile(p.X, p.Y);
        if (tile.Bomb != BombType.None)
        {
            _gameLoopSystem.ActivateBomb(ref _state, p);
            return;
        }
        
        if (_interactionSystem.TryHandleTap(ref _state, p, isInteractive, out var move))
        {
            if (move.HasValue)
            {
                ExecuteMove(move.Value);
            }
        }
    }

    public void OnSwipe(Position from, Direction direction)
    {
        bool isInteractive = true;

        if (_interactionSystem.TryHandleSwipe(ref _state, from, direction, isInteractive, out var move))
        {
            if (move.HasValue)
            {
                 ExecuteMove(move.Value);
            }
        }
    }

    public bool TryMakeRandomMove()
    {
        if (_botSystem.TryGetRandomMove(ref _state, _interactionSystem, out var move))
        {
             // For bot moves, we can either execute directly or queue an intent.
             // Queuing ensures consistent processing order.
             // However, BotSystem returns a Move (already resolved), not an Intent.
             // Let's execute directly for now as it's a resolved move.
             ExecuteMove(move);
             return true;
        }
        return false;
    }

    private void ExecuteMove(Move move)
    {
        // Don't start a new swap if one is already pending
        if (_pendingMoveNeedsValidation)
            return;

        SwapTiles(ref _state, move.From, move.To);

        // Mark as pending - validation will happen after animation completes
        _pendingMove = move;
        _pendingMoveNeedsValidation = true;
    }

    private bool HasMatch(Position p)
    {
        return _matchFinder.HasMatchAt(in _state, p);
    }
    
    // Helper to swap tiles (Logic only, keep visual positions for animation)
    private void SwapTiles(ref GameState state, Position a, Position b)
    {
        var idxA = a.Y * state.Width + a.X;
        var idxB = b.Y * state.Width + b.X;

        // Only swap grid data, keep visual Position unchanged
        // AnimationSystem will animate tiles from their current Position to new grid position
        var temp = state.Grid[idxA];
        state.Grid[idxA] = state.Grid[idxB];
        state.Grid[idxB] = temp;
    }
    
    // Helpers exposed for tests/debug
    public void DebugSetTile(Position p, TileType t)
    {
        _state.SetTile(p.X, p.Y, new Tile(_state.NextTileId++, t, p.X, p.Y));
    }
    
    public void SetTileWithBomb(int x, int y, TileType t, BombType b)
    {
        _state.SetTile(x, y, new Tile(_state.NextTileId++, t, x, y, b));
    }
}
