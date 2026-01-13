using System;
using System.Collections.Generic;
using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Interfaces;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.Utility;
using Match3.Random;

namespace Match3.Core;

/// <summary>
/// The facade and entry point for the Match3 core logic.
/// Refactored to use Async/Realtime Architecture.
/// </summary>
public sealed class Match3Engine : IDisposable
{
    private GameState _state;
    private readonly Match3Config _config;
    private readonly IGameView _view;
    private readonly IGameLogger _logger;
    private readonly ITileGenerator _tileGenerator;
    private readonly IInputSystem _inputSystem;

    // Sub-Systems
    private readonly InteractionSystem _interactionSystem;
    private readonly AnimationSystem _animationSystem;
    private readonly AsyncGameLoopSystem _gameLoopSystem; // Replaced GameLoopSystem
    private readonly IMatchFinder _matchFinder;
    private readonly IPowerUpHandler _powerUpHandler;

    public GameState State => _state;
    public Position SelectedPosition => _state.SelectedPosition;
    public string StatusMessage => _interactionSystem.StatusMessage;
    // In async mode, we are never truly "idle" in the global sense, but we can check visual stability
    public bool IsIdle => _animationSystem.IsVisuallyStable; 

    public Match3Engine(
        Match3Config config,
        IRandom rng,
        IGameView view,
        IGameLogger logger,
        IInputSystem inputSystem,
        IMatchFinder matchFinder,
        IMatchProcessor matchProcessor,
        // IGravitySystem removed
        IPowerUpHandler powerUpHandler,
        IScoreSystem scoreSystem,
        ITileGenerator tileGenerator,
        LevelConfig? levelConfig = null)
    {
        _config = config;
        _view = view;
        _logger = logger;
        _tileGenerator = tileGenerator;
        _matchFinder = matchFinder;
        _powerUpHandler = powerUpHandler;
        _inputSystem = inputSystem;

        // Initialize State
        _state = new GameState(_config.Width, _config.Height, _config.TileTypesCount, rng);
        InitializeBoard(levelConfig);
        _logger.LogInfo($"Match3Engine (Async) initialized with size {_config.Width}x{_config.Height}");

        // Initialize Systems
        _interactionSystem = new InteractionSystem(inputSystem, logger);
        _animationSystem = new AnimationSystem(config);
        
        var physics = new RealtimeGravitySystem(config);
        var refill = new RealtimeRefillSystem(tileGenerator);
        _gameLoopSystem = new AsyncGameLoopSystem(physics, refill, matchFinder, matchProcessor, powerUpHandler);

        // Bind Events
        _inputSystem.TapDetected += OnTap;
        _inputSystem.SwipeDetected += OnSwipe;
    }

    public void Dispose()
    {
        if (_inputSystem != null)
        {
            _inputSystem.TapDetected -= OnTap;
            _inputSystem.SwipeDetected -= OnSwipe;
        }
    }

    public void Update(float dt)
    {
        // 1. Animate (Visuals) - Still useful for interpolations if any
        _animationSystem.Animate(ref _state, dt);

        // 2. Logic Loop (Physics & Matching)
        _gameLoopSystem.Update(ref _state, dt);
    }

    public void OnTap(Position p)
    {
        // In async mode, we allow interaction even if things are moving elsewhere
        bool isInteractive = true; 
        
        // 1. Check for Single Tap Explosion (Bomb)
        var tile = _state.GetTile(p.X, p.Y);
        if (tile.Bomb != BombType.None)
        {
            // Activate Bomb Logic
            _gameLoopSystem.ActivateBomb(ref _state, p);
            return;
        }
        
        // 2. Standard Interaction (Select / Swap)
        if (_interactionSystem.TryHandleTap(ref _state, p, isInteractive, out var move))
        {
            if (move.HasValue)
            {
                // Execute Swap
                // For now, just swap logically. Physics will handle the rest.
                SwapTiles(ref _state, move.Value.From, move.Value.To);
                
                // Check if valid match?
                // If not, swap back?
                // In Royal Match, you can only swap if it results in a match.
                if (!HasMatch(move.Value.From) && !HasMatch(move.Value.To))
                {
                    // Invalid swap, swap back
                    SwapTiles(ref _state, move.Value.From, move.Value.To);
                }
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
                 SwapTiles(ref _state, move.Value.From, move.Value.To);
                 if (!HasMatch(move.Value.From) && !HasMatch(move.Value.To))
                 {
                     SwapTiles(ref _state, move.Value.From, move.Value.To);
                 }
            }
        }
    }

    private bool HasMatch(Position p)
    {
        return _matchFinder.HasMatchAt(in _state, p);
    }

    private void InitializeBoard(LevelConfig? levelConfig)
    {
        if (levelConfig != null)
        {
            for (int i = 0; i < levelConfig.Grid.Length; i++)
            {
                int x = i % levelConfig.Width;
                int y = i / levelConfig.Width;
                
                if (x < _state.Width && y < _state.Height)
                {
                    var type = levelConfig.Grid[i];
                    var bomb = BombType.None;
                    if (levelConfig.Bombs != null && i < levelConfig.Bombs.Length)
                    {
                        bomb = levelConfig.Bombs[i];
                    }
                    _state.SetTile(x, y, new Tile(_state.NextTileId++, type, x, y, bomb));
                }
            }
        }
        else
        {
            for (int y = 0; y < _state.Height; y++)
            {
                for (int x = 0; x < _state.Width; x++)
                {
                    var type = _tileGenerator.GenerateNonMatchingTile(ref _state, x, y);
                    _state.SetTile(x, y, new Tile(_state.NextTileId++, type, x, y));
                }
            }
        }
    }
    
    // Debug/Helpers exposed if needed
    public void DebugSetTile(Position p, TileType t)
    {
        _state.SetTile(p.X, p.Y, new Tile(_state.NextTileId++, t, p.X, p.Y));
    }

    public void SetTileWithBomb(int x, int y, TileType t, BombType b)
    {
        _state.SetTile(x, y, new Tile(_state.NextTileId++, t, x, y, b));
    }

    public void TryMakeRandomMove()
    {
        // Simple random move logic for AutoPlay
        // Try random positions and directions
        int attempts = 20;
        var w = _state.Width;
        var h = _state.Height;
        
        for (int i = 0; i < attempts; i++)
        {
            int x = _state.Random.Next(0, w);
            int y = _state.Random.Next(0, h);
            var p = new Position(x, y);
            
            // Try 4 directions
            var directions = new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right };
            foreach (var d in directions)
            {
                // Simulate swipe
                if (_interactionSystem.TryHandleSwipe(ref _state, p, d, true, out var move))
                {
                     if (move.HasValue)
                     {
                          SwapTiles(ref _state, move.Value.From, move.Value.To);
                          if (HasMatch(move.Value.From) || HasMatch(move.Value.To))
                          {
                              // Found a valid move!
                              return;
                          }
                          else
                          {
                              // Swap back
                              SwapTiles(ref _state, move.Value.From, move.Value.To);
                          }
                     }
                }
            }
        }
    }

    private void SwapTiles(ref GameState state, Position a, Position b)
    {
        var idxA = a.Y * state.Width + a.X;
        var idxB = b.Y * state.Width + b.X;
        var tA = state.Grid[idxA];
        var tB = state.Grid[idxB];
        
        // Update Logic Position in Grid
        state.Grid[idxA] = tB;
        state.Grid[idxB] = tA;
        
        // Update Physics Position (Visuals) to match Logic?
        // Or let them interpolate?
        // For immediate response, we swap their target positions (Logic X,Y) 
        // and let Physics move them.
        // But here we are modifying the struct in the array directly.
        // The Tile struct has 'Position' field.
        // We should probably NOT swap their 'Position' field immediately if we want animation.
        // But RealtimeGravitySystem assumes Position is close to Grid Index.
        // If we swap grid slots, tB is now at idxA.
        // tB.Position should eventually become (a.X, a.Y).
        // So we leave tB.Position as is, and let Physics move it?
        // RealtimeGravitySystem only moves Y currently. We need X movement too.
        // For now, let's swap Position immediately to avoid visual glitch until X-axis physics is added.
        
        var tempPos = tA.Position;
        tA.Position = tB.Position;
        tB.Position = tempPos;
        
        state.Grid[idxA] = tB; // Write back with swapped position
        state.Grid[idxB] = tA;
    }
}
