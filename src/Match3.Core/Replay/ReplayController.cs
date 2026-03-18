using System;
using System.Collections.Generic;
using Match3.Core.Commands;
using Match3.Core.DependencyInjection;
using Match3.Core.Events;
using Match3.Core.Simulation;

namespace Match3.Core.Replay;

/// <summary>
/// Controls playback of a game recording.
/// Supports variable playback speed, pause, seeking, and bookmark management.
/// </summary>
public sealed class ReplayController : IDisposable
{
    private const float TickDuration = SimulationConfig.DefaultFixedDeltaTime;
    private const int BookmarkSnapThreshold = 30; // ticks (~0.5s at 60fps)

    private readonly GameRecording _recording;
    private readonly IGameServiceFactory _factory;
    private readonly List<int> _bookmarks;
    private readonly HashSet<int> _bookmarkSet;
    private SimulationEngine? _engine;
    private int _currentCommandIndex;
    private int _currentTick;
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
    public int CurrentTick => _currentTick;

    /// <summary>Total ticks in the recording.</summary>
    public int TotalTicks => _recording.DurationTicks;

    /// <summary>Number of commands executed so far.</summary>
    public int CommandsExecuted => _currentCommandIndex;

    /// <summary>Total commands in the recording.</summary>
    public int TotalCommands => _recording.Commands.Count;

    /// <summary>The current simulation engine state.</summary>
    public SimulationEngine? Engine => _engine;

    /// <summary>Current bookmarks (mutable copy, sorted).</summary>
    public IReadOnlyList<int> Bookmarks => _bookmarks;

    /// <summary>Event raised when a command is executed during playback.</summary>
    public event Action<IGameCommand>? CommandExecuted;

    /// <summary>Event raised when playback completes.</summary>
    public event Action? PlaybackCompleted;

    /// <summary>Event raised when playback hits a bookmark tick (auto-pauses).</summary>
    public event Action<int>? BookmarkHit;

    /// <summary>Event raised when bookmarks are added or removed.</summary>
    public event Action? BookmarksChanged;

    public ReplayController(GameRecording recording, IGameServiceFactory factory)
    {
        _recording = recording ?? throw new ArgumentNullException(nameof(recording));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _bookmarks = new List<int>(recording.Bookmarks);
        _bookmarks.Sort();
        _bookmarkSet = new HashSet<int>(_bookmarks);
    }

    /// <summary>
    /// Adds a bookmark at the specified tick. If a bookmark already exists nearby, removes it instead (toggle).
    /// Returns true if a bookmark was added, false if removed.
    /// </summary>
    public bool ToggleBookmark(int tick)
    {
        int nearIndex = FindNearestBookmarkIndex(tick, BookmarkSnapThreshold);
        if (nearIndex >= 0)
        {
            _bookmarkSet.Remove(_bookmarks[nearIndex]);
            _bookmarks.RemoveAt(nearIndex);
            BookmarksChanged?.Invoke();
            return false;
        }

        // Insert sorted
        int insertIndex = _bookmarks.BinarySearch(tick);
        if (insertIndex < 0) insertIndex = ~insertIndex;
        _bookmarks.Insert(insertIndex, tick);
        _bookmarkSet.Add(tick);
        BookmarksChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Moves a bookmark from one tick to another (single BookmarksChanged event).
    /// </summary>
    public void MoveBookmark(int oldTick, int newTick)
    {
        int nearIndex = FindNearestBookmarkIndex(oldTick, BookmarkSnapThreshold);
        if (nearIndex >= 0)
        {
            _bookmarkSet.Remove(_bookmarks[nearIndex]);
            _bookmarks.RemoveAt(nearIndex);
        }

        if (!_bookmarkSet.Contains(newTick))
        {
            int insertIndex = _bookmarks.BinarySearch(newTick);
            if (insertIndex < 0) insertIndex = ~insertIndex;
            _bookmarks.Insert(insertIndex, newTick);
            _bookmarkSet.Add(newTick);
        }

        BookmarksChanged?.Invoke();
    }

    /// <summary>
    /// Removes the bookmark nearest to the specified tick within the snap threshold.
    /// Returns true if a bookmark was removed.
    /// </summary>
    public bool RemoveBookmarkNear(int tick)
    {
        int nearIndex = FindNearestBookmarkIndex(tick, BookmarkSnapThreshold);
        if (nearIndex >= 0)
        {
            _bookmarkSet.Remove(_bookmarks[nearIndex]);
            _bookmarks.RemoveAt(nearIndex);
            BookmarksChanged?.Invoke();
            return true;
        }
        return false;
    }

    private int FindNearestBookmarkIndex(int tick, int threshold)
    {
        int bestIndex = -1;
        int bestDist = int.MaxValue;
        for (int i = 0; i < _bookmarks.Count; i++)
        {
            int dist = Math.Abs(_bookmarks[i] - tick);
            if (dist <= threshold && dist < bestDist)
            {
                bestDist = dist;
                bestIndex = i;
            }
        }
        return bestIndex;
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
    /// Replays commands using RunUntilStable per command — matching the original
    /// game's execution pattern exactly for deterministic state reproduction.
    /// </summary>
    /// <param name="progress">Target progress (0.0 to 1.0).</param>
    public void Seek(float progress)
    {
        if (_disposed) return;

        progress = Math.Clamp(progress, 0f, 1f);
        int targetTick = (int)(progress * _recording.DurationTicks);

        // If seeking backwards or engine not yet created, (re)initialize
        if (targetTick < _currentTick || _engine == null)
        {
            Initialize();
        }

        // Replay commands using RunUntilStable — identical to how the original
        // game executed (command → RunUntilStable → next command).
        // Tick-by-tick replay diverges because idle ticks consume random streams
        // (e.g. physics column shuffle) differently from RunUntilStable.
        while (_currentCommandIndex < _recording.Commands.Count)
        {
            var cmd = _recording.Commands[_currentCommandIndex];
            if (cmd.IssuedAtTick > targetTick)
                break;

            cmd.Execute(_engine!);
            _engine!.RunUntilStable();
            CommandExecuted?.Invoke(cmd);
            _currentCommandIndex++;
        }

        _currentTick = targetTick;

        // Drain accumulated events during seek to prevent memory leak
        if (_engine!.EventCollector is BufferedEventCollector buffered)
        {
            buffered.Clear();
        }
    }

    /// <summary>
    /// Steps forward by one command, ticking the engine to the command's tick for accurate simulation state.
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
            int targetTick = cmd.IssuedAtTick;

            // Tick the engine to catch up to the command's tick
            while (_currentTick < targetTick)
            {
                _engine!.Tick(TickDuration);
                _currentTick++;
            }

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

        while (_accumulatedTime >= TickDuration)
        {
            _accumulatedTime -= TickDuration;

            // Execute commands scheduled for this tick, then advance simulation
            ExecuteNextCommandIfReady();
            _engine.Tick(TickDuration);
            _currentTick++;

            // Check for bookmark hit — auto-pause
            if (IsBookmarkTick(_currentTick))
            {
                State = ReplayState.Paused;
                _accumulatedTime = 0;
                BookmarkHit?.Invoke(_currentTick);
                return;
            }

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

    private bool IsBookmarkTick(int tick) => _bookmarkSet.Contains(tick);

    private void Initialize()
    {
        _engine?.Dispose();

        // Recreate the game session using the same path as gameplay:
        // CreateGameSession(config, levelConfig) ensures all random streams
        // (Refill, Physics, etc.) are consumed identically during board initialization.
        var gameConfig = new GameServiceConfiguration
        {
            Width = _recording.InitialState.Width,
            Height = _recording.InitialState.Height,
            TileTypesCount = _recording.TileTypesCount,
            RngSeed = _recording.RandomSeed,
            EnableEventCollection = true,
            SimulationConfig = SimulationConfig.ForHumanPlay()
        };
        var session = _factory.CreateGameSession(gameConfig, _recording.LevelConfig);
        _engine = session.Engine;

        _currentCommandIndex = 0;
        _currentTick = 0;
        _accumulatedTime = 0;
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
