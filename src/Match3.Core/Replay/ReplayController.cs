using System;
using Match3.Core.Commands;
using Match3.Core.DependencyInjection;
using Match3.Core.Simulation;
using Match3.Random;

namespace Match3.Core.Replay;

/// <summary>
/// Controls playback of a game recording.
/// Supports variable playback speed, pause, and seeking.
/// </summary>
public sealed class ReplayController : IDisposable
{
    private readonly GameRecording _recording;
    private readonly IGameServiceFactory _factory;
    private SimulationEngine? _engine;
    private int _currentCommandIndex;
    private long _currentTick;
    private float _accumulatedTime;
    private bool _disposed;

    /// <summary>Current playback state.</summary>
    public ReplayState State { get; private set; } = ReplayState.Stopped;

    /// <summary>Playback speed multiplier (1.0 = normal speed).</summary>
    public float PlaybackSpeed { get; set; } = 1.0f;

    /// <summary>Current playback progress (0.0 to 1.0).</summary>
    public float Progress => _recording.DurationTicks > 0
        ? (float)_currentTick / _recording.DurationTicks
        : 0f;

    /// <summary>Current tick in the replay.</summary>
    public long CurrentTick => _currentTick;

    /// <summary>Total ticks in the recording.</summary>
    public long TotalTicks => _recording.DurationTicks;

    /// <summary>Number of commands executed so far.</summary>
    public int CommandsExecuted => _currentCommandIndex;

    /// <summary>Total commands in the recording.</summary>
    public int TotalCommands => _recording.Commands.Count;

    /// <summary>The current simulation engine state.</summary>
    public SimulationEngine? Engine => _engine;

    /// <summary>Event raised when a command is executed during playback.</summary>
    public event Action<IGameCommand>? CommandExecuted;

    /// <summary>Event raised when playback completes.</summary>
    public event Action? PlaybackCompleted;

    public ReplayController(GameRecording recording, IGameServiceFactory factory)
    {
        _recording = recording ?? throw new ArgumentNullException(nameof(recording));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Starts or resumes playback from the current position.
    /// </summary>
    public void Play()
    {
        if (_disposed) return;

        if (State == ReplayState.Stopped)
        {
            Initialize();
        }

        State = ReplayState.Playing;
    }

    /// <summary>
    /// Pauses playback.
    /// </summary>
    public void Pause()
    {
        if (State == ReplayState.Playing)
        {
            State = ReplayState.Paused;
        }
    }

    /// <summary>
    /// Stops playback and resets to the beginning.
    /// </summary>
    public void Stop()
    {
        State = ReplayState.Stopped;
        _currentCommandIndex = 0;
        _currentTick = 0;
        _accumulatedTime = 0;
        _engine?.Dispose();
        _engine = null;
    }

    /// <summary>
    /// Toggles between play and pause states.
    /// </summary>
    public void TogglePause()
    {
        if (State == ReplayState.Playing)
            Pause();
        else if (State == ReplayState.Paused || State == ReplayState.Stopped)
            Play();
    }

    /// <summary>
    /// Seeks to a specific progress position (0.0 to 1.0).
    /// </summary>
    /// <param name="progress">Target progress (0.0 to 1.0).</param>
    public void Seek(float progress)
    {
        if (_disposed) return;

        progress = Math.Clamp(progress, 0f, 1f);
        long targetTick = (long)(progress * _recording.DurationTicks);

        // If seeking backwards, need to restart
        if (targetTick < _currentTick)
        {
            ResetToStart();
        }

        // Fast-forward to target tick
        while (_currentTick < targetTick && _currentCommandIndex < _recording.Commands.Count)
        {
            ExecuteNextCommandIfReady();
            _currentTick++;
        }
    }

    /// <summary>
    /// Steps forward by one command.
    /// </summary>
    public void StepForward()
    {
        if (_disposed) return;

        if (State == ReplayState.Stopped)
        {
            Initialize();
            State = ReplayState.Paused;
        }

        if (_currentCommandIndex < _recording.Commands.Count)
        {
            var cmd = _recording.Commands[_currentCommandIndex];
            _currentTick = cmd.IssuedAtTick;
            ExecuteCommand(cmd);
            _currentCommandIndex++;
        }
    }

    /// <summary>
    /// Updates the replay by the given delta time.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last update.</param>
    public void Tick(float deltaTime)
    {
        if (_disposed || State != ReplayState.Playing || _engine == null) return;

        _accumulatedTime += deltaTime * PlaybackSpeed;

        // Convert accumulated time to ticks (assuming 60 ticks per second)
        const float TickDuration = 1f / 60f;
        while (_accumulatedTime >= TickDuration)
        {
            _accumulatedTime -= TickDuration;
            _currentTick++;

            // Execute commands scheduled for this tick
            ExecuteNextCommandIfReady();

            // Tick the simulation
            _engine.Tick(TickDuration);

            // Check for completion
            if (_currentTick >= _recording.DurationTicks &&
                _currentCommandIndex >= _recording.Commands.Count)
            {
                State = ReplayState.Completed;
                PlaybackCompleted?.Invoke();
                return;
            }
        }
    }

    private void Initialize()
    {
        _engine?.Dispose();

        // Create random with recorded seed
        var seedManager = new SeedManager(_recording.RandomSeed);
        var random = seedManager.GetRandom(RandomDomain.Main);

        // Restore initial state from the recording
        var initialState = _recording.InitialState.ToState(random);

        // Create simulation engine with the RESTORED state (not a new one)
        var simulationConfig = SimulationConfig.ForHumanPlay();
        _engine = _factory.CreateSimulationEngine(initialState, simulationConfig);

        _currentCommandIndex = 0;
        _currentTick = 0;
        _accumulatedTime = 0;
    }

    private void ResetToStart()
    {
        Initialize();
    }

    private void ExecuteNextCommandIfReady()
    {
        while (_currentCommandIndex < _recording.Commands.Count)
        {
            var cmd = _recording.Commands[_currentCommandIndex];
            if (cmd.IssuedAtTick > _currentTick)
                break;

            ExecuteCommand(cmd);
            _currentCommandIndex++;
        }
    }

    private void ExecuteCommand(IGameCommand command)
    {
        if (_engine != null)
        {
            command.Execute(_engine);
            CommandExecuted?.Invoke(command);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _engine?.Dispose();
        _engine = null;
    }
}

/// <summary>
/// Playback state of the replay controller.
/// </summary>
public enum ReplayState
{
    Stopped,
    Playing,
    Paused,
    Completed
}
