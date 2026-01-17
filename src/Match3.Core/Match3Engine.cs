using System;
using System.Collections.Generic;
using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Models.Input;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.Systems.Selection;
using Match3.Core.Systems.Swap;
using Match3.Core.Utility;
using Match3.Core.View;
using Match3.Random;

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
    private readonly IMoveSelector _moveSelector;
    private readonly ISwapOperations _swapOperations;

    // Input Queue
    private readonly Queue<InputIntent> _inputQueue = new();

    // Pending move for swap animation validation (uses shared PendingMoveState)
    // When a swap is executed, we need to wait for animation to complete before deciding
    // whether to swap back (invalid move) or keep the swap (valid move).
    // IMPORTANT: Match detection must happen at swap time, not after animation completes,
    // because tiles may have been eliminated by the game loop during animation.
    private PendingMoveState _pendingMoveState;

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
        IMoveSelector moveSelector,
        LevelConfig? levelConfig = null)
    {
        _config = config;
        _view = view;
        _logger = logger;
        _gameLoopSystem = gameLoopSystem;
        _interactionSystem = interactionSystem;
        _animationSystem = animationSystem;
        _matchFinder = matchFinder;
        _moveSelector = moveSelector;

        // Initialize shared swap operations with animated context
        var swapContext = new AnimatedSwapContext(animationSystem);
        _swapOperations = new SwapOperations(matchFinder, swapContext);

        // Initialize State
        _state = new GameState(_config.Width, _config.Height, _config.TileTypesCount, rng);
        boardInitializer.Initialize(ref _state, levelConfig);

        _logger.LogInfo("Match3Engine initialized with size {0}x{1}", _config.Width, _config.Height);
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

        // Check pending move validation after animation completes (uses shared swap operations)
        _swapOperations.ValidatePendingMove(
            ref _state,
            ref _pendingMoveState,
            dt,
            0, // tick not used for animated context
            0f, // simTime not used for animated context
            NullEventCollector.Instance); // Match3Engine doesn't emit events

        _gameLoopSystem.Update(ref _state, dt);
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
        if (_moveSelector.TryGetMove(in _state, out var action))
        {
             // For bot moves, we can either execute directly or queue an intent.
             // Queuing ensures consistent processing order.
             // MoveSelector returns a MoveAction, convert to Move.
             // Let's execute directly for now as it's a resolved move.
             ExecuteMove(action.ToMove());
             return true;
        }
        return false;
    }

    private void ExecuteMove(Move move)
    {
        // Don't start a new swap if one is already pending
        if (_pendingMoveState.HasPending)
            return;

        // Swap tiles using shared operations
        _swapOperations.SwapTiles(ref _state, move.From, move.To);

        // Check for matches IMMEDIATELY after swap, before any game loop update
        // This ensures we capture the match state before tiles get eliminated
        var hadMatch = _swapOperations.HasMatch(in _state, move.From) ||
                       _swapOperations.HasMatch(in _state, move.To);

        // Track pending move for validation after animation completes
        _pendingMoveState = new PendingMoveState
        {
            From = move.From,
            To = move.To,
            TileAId = _state.GetTile(move.From.X, move.From.Y).Id,
            TileBId = _state.GetTile(move.To.X, move.To.Y).Id,
            HadMatch = hadMatch,
            NeedsValidation = true,
            AnimationTime = 0f
        };
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
