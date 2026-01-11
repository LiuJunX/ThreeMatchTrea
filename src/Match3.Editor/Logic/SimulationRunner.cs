using System;
using System.Collections.Generic;
using Match3.Core;
using Match3.Core.Utility;
using Match3.Core.Config;
using Match3.Core.Interfaces;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Scenarios;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Gravity;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Random;

namespace Match3.Editor.Logic
{
    public class SimulationRunner : IDisposable
    {
        private Match3Engine? _engine;
        private readonly IGameLogger _logger;
        private System.Threading.Timer? _timer;
        private bool _isRecording;
        private ScenarioConfig? _recordingScenario;

        public event Action? OnRepaintRequired;

        public Match3Engine? Engine => _engine;
        public bool IsRecording => _isRecording;

        public SimulationRunner(IGameLogger logger)
        {
            _logger = logger;
        }

        public void StartRecording(LevelConfig initialConfig, ScenarioConfig scenarioTarget)
        {
            StopRecording();

            _isRecording = true;
            _recordingScenario = scenarioTarget;
            _recordingScenario.Operations.Clear();

            var seed = _recordingScenario.Seed;
            var seedManager = new SeedManager(seed);

            var view = new EditorGameView(this);
            var engineConfig = new Match3Config(initialConfig.Width, initialConfig.Height, 6);

            var scoreSystem = new StandardScoreSystem();
            var inputSystem = new StandardInputSystem();
            var tileGen = new StandardTileGenerator(seedManager.GetRandom(RandomDomain.Refill));

            _engine = new Match3Engine(
                engineConfig,
                seedManager.GetRandom(RandomDomain.Main),
                view,
                _logger,
                inputSystem,
                new ClassicMatchFinder(),
                new StandardMatchProcessor(scoreSystem),
                new StandardGravitySystem(tileGen),
                new PowerUpHandler(scoreSystem),
                scoreSystem,
                tileGen,
                initialConfig
            );

            // Start Timer (approx 60fps)
            _timer = new System.Threading.Timer(OnTimerTick, null, 16, 16);
        }

        public void StopRecording()
        {
            _isRecording = false;
            _timer?.Dispose();
            _timer = null;
            _engine = null;
            _recordingScenario = null;
        }

        private void OnTimerTick(object? state)
        {
            if (_isRecording && _engine != null)
            {
                _engine.Update(0.016f);
                OnRepaintRequired?.Invoke();
            }
        }

        public void HandleInput(int x, int y)
        {
            if (_isRecording && _engine != null)
            {
                _engine.OnTap(new Position(x, y));
            }
        }

        private void RecordMove(Position a, Position b)
        {
            if (_isRecording && _recordingScenario != null)
            {
                _recordingScenario.Operations.Add(new MoveOperation(a.X, a.Y, b.X, b.Y));
            }
        }

        public void Dispose()
        {
            StopRecording();
        }

        private class EditorGameView : IGameView
        {
            private readonly SimulationRunner _runner;
            public EditorGameView(SimulationRunner runner) => _runner = runner;
            
            public void RenderBoard(TileType[,] board) 
            { 
                _runner.OnRepaintRequired?.Invoke(); 
            }
            
            public void ShowSwap(Position a, Position b, bool success)
            {
                if (success) _runner.RecordMove(a, b);
                _runner.OnRepaintRequired?.Invoke();
            }
            
            public void ShowMatches(IReadOnlyCollection<Position> matched) { }
            public void ShowGravity(IEnumerable<TileMove> moves) { }
            public void ShowRefill(IEnumerable<TileMove> moves) { }
        }
    }
}
