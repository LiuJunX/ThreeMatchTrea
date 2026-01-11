using System;
using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Interfaces;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Gravity;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Random;

namespace Match3.Core.AI;

/// <summary>
/// A RL-friendly environment wrapper using the new Data-Oriented Core (Structs + Logic).
/// </summary>
public class Match3Environment : IGameEnvironment<GameState, Move>
{
    private GameState _state;
    private readonly int _width;
    private readonly int _height;
    private readonly int _tileTypesCount;
    private readonly int _maxMoves;
    private int _stepsTaken;

    private readonly IMatchFinder _matchFinder;
    private readonly IMatchProcessor _matchProcessor;
    private IGravitySystem _gravitySystem;
    private readonly IPowerUpHandler _powerUpHandler;
    private ITileGenerator _tileGenerator;

    public Match3Environment(int width, int height, int tileTypesCount, int maxMoves = 100)
    {
        _width = width;
        _height = height;
        _tileTypesCount = tileTypesCount;
        _maxMoves = maxMoves;

        _matchFinder = new ClassicMatchFinder();
        var scoreSystem = new StandardScoreSystem();
        _matchProcessor = new StandardMatchProcessor(scoreSystem);
        _tileGenerator = new StandardTileGenerator();
        _gravitySystem = new StandardGravitySystem(_tileGenerator);
        _powerUpHandler = new PowerUpHandler(scoreSystem);
    }

    public GameState Reset(int? seed = null)
    {
        var seeds = new SeedManager(seed);
        var rng = seeds.GetRandom(RandomDomain.Main);
        _state = new GameState(_width, _height, _tileTypesCount, rng);
        _tileGenerator = new StandardTileGenerator(seeds.GetRandom(RandomDomain.Refill));
        _gravitySystem = new StandardGravitySystem(_tileGenerator);
        InitializeBoard();
        _stepsTaken = 0;
        return _state; // Returns a copy because GameState is a struct
    }

    private void InitializeBoard()
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

    public StepResult<GameState> Step(Move move)
    {
        if (_state.Grid == null)
            throw new InvalidOperationException("Environment not initialized. Call Reset() first.");

        if (_stepsTaken >= _maxMoves)
        {
            return new StepResult<GameState>(_state, 0, true, new Dictionary<string, object> { { "Reason", "MaxMovesReached" } });
        }

        _stepsTaken++;
        bool isDone = _stepsTaken >= _maxMoves;

        if (!IsValidMove(move.From, move.To))
        {
             return new StepResult<GameState>(_state, -10.0, isDone, new Dictionary<string, object> { { "Error", "InvalidMove" } });
        }

        int cascades;
        int points;
        bool success = ApplyMove(ref _state, move.From, move.To, out cascades, out points);

        if (!success)
        {
             return new StepResult<GameState>(_state, -1.0, isDone, new Dictionary<string, object> { { "Result", "NoMatch" } });
        }

        return new StepResult<GameState>(
            _state, 
            points, 
            isDone, 
            new Dictionary<string, object> 
            { 
                { "Score", _state.Score },
                { "Cascades", cascades },
                { "CurrentMove", _state.MoveCount },
                { "StepsTaken", _stepsTaken }
            }
        );
    }

    public GameState GetState()
    {
        return _state;
    }

    private bool IsValidMove(Position from, Position to)
    {
        if (from.X < 0 || from.X >= _state.Width || from.Y < 0 || from.Y >= _state.Height) return false;
        if (to.X < 0 || to.X >= _state.Width || to.Y < 0 || to.Y >= _state.Height) return false;
        if (Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y) != 1) return false;
        return true;
    }

    private void Swap(ref GameState state, Position a, Position b)
    {
        var idxA = state.Index(a.X, a.Y);
        var idxB = state.Index(b.X, b.Y);
        var temp = state.Grid[idxA];
        state.Grid[idxA] = state.Grid[idxB];
        state.Grid[idxB] = temp;
    }

    private bool ApplyMove(ref GameState state, Position from, Position to, out int cascades, out int totalPoints)
    {
        cascades = 0;
        totalPoints = 0;

        // 1. Swap
        Swap(ref state, from, to);

        // 2. Check Special
        var tFrom = state.GetTile(from.X, from.Y); // Now at 'from' (swapped)
        var tTo = state.GetTile(to.X, to.Y); // Now at 'to'

        bool isBombCombo = (tFrom.Bomb != BombType.None || tFrom.Type == TileType.Rainbow) && 
                           (tTo.Bomb != BombType.None || tTo.Type == TileType.Rainbow);
        
        bool isColorMix = !isBombCombo && (tFrom.Type == TileType.Rainbow || tTo.Type == TileType.Rainbow);

        if (isBombCombo || isColorMix)
        {
             _powerUpHandler.ProcessSpecialMove(ref state, from, to, out int comboPoints);
             totalPoints += comboPoints;
        }
        else
        {
            if (!_matchFinder.HasMatches(in state))
            {
                Swap(ref state, from, to); // Revert
                return false;
            }
        }

        // 3. Process Cascades
        state.MoveCount++;
        
        bool firstIteration = true;
        while (true)
        {
            var groups = _matchFinder.FindMatchGroups(in state, firstIteration ? new[] { to } : null);
            
            if (groups.Count == 0) break;

            cascades++;
            firstIteration = false;
            
            int points = _matchProcessor.ProcessMatches(ref state, groups);
            
            if (cascades > 1) points *= cascades;
            totalPoints += points;

            _gravitySystem.ApplyGravity(ref state);
            _gravitySystem.Refill(ref state);
        }

        state.Score += totalPoints;
        return true;
    }
}
