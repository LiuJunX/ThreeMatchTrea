using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Match3.Core;
using Match3.Core.Config;
using Match3.Core.Interfaces;
using Match3.Core.Logic;
using Match3.Core.Structs;
using Microsoft.Extensions.Logging;

namespace Match3.Web.Services;

public class Match3GameService : IDisposable
{
    private readonly ILogger<Match3GameService> _appLogger;
    private Match3Controller? _controller;
    private bool _isAutoPlaying;
    private float _gameSpeed = 1.0f;
    private bool _disposed;
    private CancellationTokenSource? _loopCts;

    public event Action? OnChange;

    public Match3GameService(ILogger<Match3GameService> appLogger)
    {
        _appLogger = appLogger;
    }

    public Match3Controller? Controller => _controller;
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

        var rng = new DefaultRandom(Environment.TickCount);
        var view = new ServiceGameView(this);
        
        var tileGenerator = new StandardTileGenerator();
        var gravitySystem = new StandardGravitySystem(tileGenerator);
        var matchFinder = new ClassicMatchFinder();
        var matchProcessor = new StandardMatchProcessor();
        var powerUpHandler = new PowerUpHandler();

        var gameLogger = new MicrosoftGameLogger(_appLogger);
        var config = new Match3Config(Width, Height, 6);

        _controller = new Match3Controller(
            config,
            rng, 
            view,
            matchFinder,
            matchProcessor,
            gravitySystem,
            powerUpHandler,
            tileGenerator,
            gameLogger,
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
            if (_controller != null)
            {
                _controller.Update((FrameMs / 1000.0f) * _gameSpeed);

                if (_isAutoPlaying && _controller.IsIdle)
                {
                    _controller.TryMakeRandomMove();
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
    
    public void OnTap(int x, int y) => _controller?.OnTap(new Position(x, y));
    public void OnSwipe(Position from, Direction dir) => _controller?.OnSwipe(from, dir);

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
