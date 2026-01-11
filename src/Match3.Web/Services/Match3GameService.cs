using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Match3.Core;
using Match3.Core.Config;
using Match3.Core.Interfaces;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Gravity;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.Utility;
using Microsoft.Extensions.Logging;
using Match3.Random;

namespace Match3.Web.Services;

public class Match3GameService : IDisposable
{
    private readonly ILogger<Match3GameService> _appLogger;
    private Match3Engine? _engine;
    private bool _isAutoPlaying;
    private float _gameSpeed = 1.0f;
    private bool _disposed;
    private CancellationTokenSource? _loopCts;

    public event Action? OnChange;

    public Match3GameService(ILogger<Match3GameService> appLogger)
    {
        _appLogger = appLogger;
    }

    public Match3Engine? Engine => _engine;
    public bool IsAutoPlaying => _isAutoPlaying;
    public float GameSpeed 
    { 
        get => _gameSpeed; 
        set => _gameSpeed = Math.Clamp(value, 0.1f, 5.0f); 
    }
    public int LastMatchesCount { get; private set; }

    public int Width { get; private set; } = 8;
    public int Height { get; private set; } = 8;
    public const int CellSize = 66; // Exposed for UI

    public void StartNewGame(LevelConfig? levelConfig = null)
    {
        StopLoop();
        
        if (levelConfig != null)
        {
            Width = levelConfig.Width;
            Height = levelConfig.Height;
        }

        var rngSeed = Environment.TickCount;
        var seedManager = new SeedManager(rngSeed);
        var rng = seedManager.GetRandom(RandomDomain.Main);
        var view = new ServiceGameView(this);
        
        var gameLogger = new MicrosoftGameLogger(_appLogger);
        var config = new Match3Config(Width, Height, 6);

        var tileGenerator = new StandardTileGenerator(seedManager.GetRandom(RandomDomain.Refill));
        var gravitySystem = new StandardGravitySystem(tileGenerator, config);
        var bombGenerator = new Match3.Core.Systems.Matching.Generation.BombGenerator();
        var matchFinder = new ClassicMatchFinder(bombGenerator);
        var scoreSystem = new StandardScoreSystem();
        var matchProcessor = new StandardMatchProcessor(scoreSystem);
        var powerUpHandler = new PowerUpHandler(scoreSystem);
        var inputSystem = new StandardInputSystem();

        _engine = new Match3Engine(
            config,
            rng, 
            view,
            gameLogger,
            inputSystem,
            matchFinder,
            matchProcessor,
            gravitySystem,
            powerUpHandler,
            scoreSystem,
            tileGenerator,
            levelConfig
        );
        
        LastMatchesCount = 0;
        _isAutoPlaying = false;
        
        StartLoop();
        NotifyStateChanged();
    }
    
    public void ResetGame()
    {
        StartNewGame();
    }

    private void StartLoop()
    {
        _loopCts = new CancellationTokenSource();
        _ = GameLoopAsync(_loopCts.Token);
    }

    private void StopLoop()
    {
        _loopCts?.Cancel();
        _loopCts = null;
    }

    private async Task GameLoopAsync(CancellationToken token)
    {
        const int TargetFps = 60;
        const int FrameMs = 1000 / TargetFps;

        while (!token.IsCancellationRequested && !_disposed)
        {
            if (_engine != null)
            {
                _engine.Update((FrameMs / 1000.0f) * _gameSpeed);

                if (_isAutoPlaying && _engine.IsIdle)
                {
                    _engine.TryMakeRandomMove();
                }

                NotifyStateChanged();
            }
            
            try 
            {
                await Task.Delay(FrameMs, token);
            }
            catch (TaskCanceledException) { break; }
        }
    }

    public void ToggleAutoPlay()
    {
        _isAutoPlaying = !_isAutoPlaying;
    }
    
    public void OnTap(int x, int y) => _engine?.OnTap(new Position(x, y));
    public void OnSwipe(Position from, Direction dir) => _engine?.OnSwipe(from, dir);

    public void SetLastMatches(int count)
    {
        LastMatchesCount = count;
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    public void Dispose()
    {
        _disposed = true;
        StopLoop();
    }
    
    private class ServiceGameView : IGameView
    {
        private readonly Match3GameService _service;
        public ServiceGameView(Match3GameService service) => _service = service;

        public void RenderBoard(TileType[,] board) { }
        public void ShowSwap(Position a, Position b, bool success) { }
        public void ShowMatches(IReadOnlyCollection<Position> matched) 
        {
            _service.SetLastMatches(matched.Count);
        }
        public void ShowGravity(IEnumerable<TileMove> moves) { }
        public void ShowRefill(IEnumerable<TileMove> moves) { }
    }
}
