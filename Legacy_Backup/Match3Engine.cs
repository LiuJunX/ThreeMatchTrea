using System;
using System.Collections.Generic;
using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Interfaces;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Gravity;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.Utility;
using Match3.Random;

namespace Match3.Core;

/// <summary>
/// The facade and entry point for the Match3 core logic.
/// Orchestrates the Interaction, Animation, and GameLoop systems.
/// </summary>
public sealed class Match3Engine
{
    private GameState _state;
    private readonly Match3Config _config;
    private readonly IGameView _view;
    private readonly IGameLogger _logger;
    private readonly ITileGenerator _tileGenerator; // For initialization

    // Sub-Systems
    private readonly InteractionSystem _interactionSystem;
    private readonly AnimationSystem _animationSystem;
    private readonly GameLoopSystem _gameLoopSystem;
    private readonly IMatchFinder _matchFinder;

    public GameState State => _state;
    public Position SelectedPosition => _state.SelectedPosition;
    public string StatusMessage => _interactionSystem.StatusMessage;
    public bool IsIdle => _animationSystem.IsVisuallyStable && !_gameLoopSystem.HasActiveTasks;

    public Match3Engine(
        Match3Config config,
        IRandom rng,
        IGameView view,
        IGameLogger logger,
        IInputSystem inputSystem,
        IMatchFinder matchFinder,
        IMatchProcessor matchProcessor,
        IGravitySystem gravitySystem,
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

        // Initialize State
        _state = new GameState(_config.Width, _config.Height, _config.TileTypesCount, rng);
        InitializeBoard(levelConfig);
        _logger.LogInfo($"Match3Engine initialized with size {_config.Width}x{_config.Height}");

        // Initialize Systems
        _interactionSystem = new InteractionSystem(inputSystem, logger);
        _animationSystem = new AnimationSystem(config);
        _gameLoopSystem = new GameLoopSystem(matchFinder, matchProcessor, gravitySystem, powerUpHandler, scoreSystem);

        // Bind Events
        _gameLoopSystem.OnEvent += HandleGameEvent;
    }

    public void Update(float dt)
    {
        // 1. Animate (Visuals)
        _animationSystem.Animate(ref _state, dt);

        // 2. Logic Loop
        _gameLoopSystem.Update(ref _state, _animationSystem);
    }

    public void OnTap(Position p)
    {
        bool isInteractive = _animationSystem.IsVisuallyStable && !_gameLoopSystem.HasActiveTasks;
        
        if (_interactionSystem.TryHandleTap(ref _state, p, isInteractive, out var move))
        {
            if (move.HasValue)
            {
                _gameLoopSystem.TryStartSwap(ref _state, move.Value.From, move.Value.To);
            }
        }
    }

    public void OnSwipe(Position from, Direction direction)
    {
        bool isInteractive = _animationSystem.IsVisuallyStable && !_gameLoopSystem.HasActiveTasks;

        if (_interactionSystem.TryHandleSwipe(ref _state, from, direction, isInteractive, out var move))
        {
            if (move.HasValue)
            {
                _gameLoopSystem.TryStartSwap(ref _state, move.Value.From, move.Value.To);
            }
        }
    }

    private void HandleGameEvent(IGameEvent evt)
    {
        switch (evt)
        {
            case TileSwappedEvent e:
                _view.ShowSwap(e.PositionA, e.PositionB, e.IsSuccessful);
                break;
            case MatchesFoundEvent e:
                var positions = new HashSet<Position>();
                foreach (var g in e.Matches)
                {
                    foreach (var p in g.Positions) positions.Add(p);
                }
                _view.ShowMatches(positions);
                break;
            case GravityAppliedEvent e:
                _view.ShowGravity(e.Moves);
                break;
            case BoardRefilledEvent e:
                _view.ShowRefill(e.NewTiles);
                break;
        }
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

    public bool TryMakeRandomMove()
    {
        int w = _state.Width;
        int h = _state.Height;
        int count = w * h;
        
        // Pick a random starting point to avoid bias
        int startIdx = _state.Random.Next(0, count);
        
        for (int i = 0; i < count; i++)
        {
            int idx = (startIdx + i) % count;
            int x = idx % w;
            int y = idx / w;
            var p = new Position(x, y);

            // Try Right
            if (x < w - 1)
            {
                if (TryMove(p, new Position(x + 1, y))) return true;
            }
            
            // Try Down
            if (y < h - 1)
            {
                if (TryMove(p, new Position(x, y + 1))) return true;
            }
        }
        
        return false;
    }

    private bool TryMove(Position a, Position b)
    {
        // 1. Check Special Move (Rainbow, Bomb combo) - valid without matching
        if (IsSpecialMove(a, b))
        {
            _gameLoopSystem.TryStartSwap(ref _state, a, b);
            return true;
        }

        // 2. Swap in state
        SwapTiles(ref _state, a, b);
        
        // 3. Check match (Optimized)
        bool hasMatch = _matchFinder.HasMatchAt(in _state, a) || _matchFinder.HasMatchAt(in _state, b);
        
        // 4. Swap back
        SwapTiles(ref _state, a, b);
        
        if (hasMatch)
        {
            _gameLoopSystem.TryStartSwap(ref _state, a, b);
            return true;
        }
        
        return false;
    }

    private void SwapTiles(ref GameState state, Position a, Position b)
    {
        var idxA = a.Y * state.Width + a.X;
        var idxB = b.Y * state.Width + b.X;
        var temp = state.Grid[idxA];
        state.Grid[idxA] = state.Grid[idxB];
        // Note: We don't update X/Y in Tile struct here because FindMatchGroups only uses Grid index.
        // But if we did, we should revert it too.
        
        state.Grid[idxB] = temp;
    }

    private bool IsSpecialMove(Position a, Position b)
    {
        var t1 = _state.GetTile(a.X, a.Y);
        var t2 = _state.GetTile(b.X, b.Y);
        
        if (t1.Type == TileType.Rainbow || t2.Type == TileType.Rainbow) return true;
        
        bool isBombCombo = t1.Bomb != BombType.None && t2.Bomb != BombType.None;
        
        return isBombCombo;
    }
}
