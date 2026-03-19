using System;
using System.Collections.Generic;
using Match3.Core.Commands;
using Match3.Core.Events;
using Match3.Core.Models.Grid;
using Match3.Core.Replay;
using Match3.Core.Undo;

namespace Match3.Core.Simulation;

/// <summary>
/// Game-level engine that wraps <see cref="SimulationEngine"/> with a ring buffer
/// for speculative execution and lookahead.
/// <para>
/// Responsibilities:
/// <list type="bullet">
///   <item>Ring buffer of pre-computed tick snapshots (configurable depth)</item>
///   <item>Peek ahead via <see cref="IPeekProvider"/> for physics/AI</item>
///   <item>Unified input gateway (<see cref="InjectCommand"/>) that handles
///         undo, replay recording, and speculation invalidation</item>
///   <item>Fixed-timestep accumulation (moved from Bridge layer)</item>
///   <item>Debug hash verification to catch external state modifications</item>
/// </list>
/// </para>
/// </summary>
public sealed class GameEngine : IPeekProvider, IDisposable
{
    private readonly SimulationEngine _engine;
    private readonly EngineSnapshot[] _buffer;
    private readonly int _bufferSize;
    private readonly BufferedEventCollector _tickCollector;

    private int _current;   // Rendering reads this slot (absolute tick index)
    private int _write;     // Engine has written up to this slot (exclusive)
    private float _accumulator;
    private int _frameStartCurrent; // _current at the start of AdvanceFrame, for multi-tick event drain

    // Optional integrations
    private UndoSystem? _undoSystem;
    private GameRecorder? _recorder;

    /// <summary>Maximum speculative ticks per AdvanceFrame call.</summary>
    public int MaxTicksPerFrame { get; set; } = 2;

    /// <summary>Fixed time step used for each tick.</summary>
    public float FixedDeltaTime { get; }

    /// <summary>
    /// Creates a new GameEngine wrapping the given SimulationEngine.
    /// </summary>
    /// <param name="engine">The underlying tick executor.</param>
    /// <param name="bufferSize">Ring buffer depth (default 5).</param>
    /// <param name="fixedDeltaTime">Fixed time step (default 1/60).</param>
    public GameEngine(
        SimulationEngine engine,
        int bufferSize = 5,
        float fixedDeltaTime = SimulationConfig.DefaultFixedDeltaTime)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _bufferSize = bufferSize;
        _buffer = new EngineSnapshot[bufferSize];
        _tickCollector = new BufferedEventCollector();
        FixedDeltaTime = fixedDeltaTime;

        // Snapshot the initial state as slot 0, then assign pre-allocated events
        _buffer[0] = _engine.CreateSnapshot();
        _buffer[0].Events = new List<GameEvent>();
        _buffer[0].IsValid = true;
        _buffer[0].IsStable = _engine.IsStable();
        StoreStateHash(ref _buffer[0]);

        // Pre-allocate event lists for remaining slots
        for (int i = 1; i < _bufferSize; i++)
            _buffer[i].Events = new List<GameEvent>();

        _current = 0;
        _write = 1; // Next write goes to slot 1
    }

    /// <summary>
    /// Set the undo system for automatic checkpoint saving on input.
    /// </summary>
    public void SetUndoSystem(UndoSystem? undoSystem) => _undoSystem = undoSystem;

    /// <summary>
    /// Set the game recorder for automatic replay recording on input.
    /// </summary>
    public void SetRecorder(GameRecorder? recorder) => _recorder = recorder;

    /// <summary>
    /// The underlying SimulationEngine, for direct access by AI/tests.
    /// </summary>
    public SimulationEngine Engine => _engine;

    /// <summary>
    /// Current game state (the slot being rendered).
    /// </summary>
    public GameState CurrentState => _buffer[_current % _bufferSize].State;

    /// <summary>
    /// Current simulation tick.
    /// </summary>
    public int CurrentTick => _buffer[_current % _bufferSize].Tick;

    /// <summary>
    /// Current elapsed simulation time.
    /// </summary>
    public float CurrentElapsedTime => _buffer[_current % _bufferSize].ElapsedTime;

    /// <summary>
    /// Whether the current rendered state is stable (no active effects).
    /// Reads from the ring buffer snapshot, not the engine's speculated state.
    /// </summary>
    public bool IsStable => _buffer[_current % _bufferSize].IsStable;

    #region IPeekProvider

    /// <inheritdoc />
    public bool TryPeekState(int tickOffset, out GameState state)
    {
        if (tickOffset < 0 || _current + tickOffset >= _write)
        {
            state = default;
            return false;
        }

        var slot = (_current + tickOffset) % _bufferSize;
        var snapshot = _buffer[slot];
        if (!snapshot.IsValid)
        {
            state = default;
            return false;
        }

        state = snapshot.State;
        return true;
    }

    /// <inheritdoc />
    public int MaxPeekOffset => Math.Max(0, _write - _current - 1);

    #endregion

    #region Frame Advancement

    /// <summary>
    /// Advance the simulation by the given scaled delta time.
    /// Uses fixed-timestep accumulation internally.
    /// Guarantees peek+1 is available before returning.
    /// </summary>
    public void AdvanceFrame(float scaledDelta)
    {
        _frameStartCurrent = _current;
        _accumulator += scaledDelta;
        int ticksThisFrame = 0;

        while (_accumulator >= FixedDeltaTime)
        {
            _accumulator -= FixedDeltaTime;

            // Guarantee: current+1 must be valid before we advance
            EnsureFilledTo(_current + 2);

            // Advance the read head
            _current++;

            // Debug: verify the consumed slot wasn't externally mutated
            VerifySpeculation();
            ticksThisFrame++;
        }

        // Fill ahead with remaining budget
        int budget = MaxTicksPerFrame - ticksThisFrame;
        while (budget > 0 && _write - _current < _bufferSize)
        {
            TickAndStore();
            budget--;
        }
    }

    /// <summary>
    /// Drain events for all ticks consumed in the last AdvanceFrame call.
    /// When multiple fixed steps occur in one frame (e.g., GameSpeed > 1x or frame drops),
    /// this returns events from ALL consumed ticks, not just the last one.
    /// </summary>
    public void DrainCurrentEvents(IList<GameEvent> target)
    {
        // Drain events from every slot consumed during this frame:
        // from _frameStartCurrent+1 through _current (inclusive).
        for (int i = _frameStartCurrent + 1; i <= _current; i++)
        {
            var slot = i % _bufferSize;
            var events = _buffer[slot].Events;
            if (events == null || events.Count == 0) continue;

            foreach (var evt in events)
                target.Add(evt);
            events.Clear();
        }
    }

    #endregion

    #region Input Gateway

    /// <summary>
    /// Unified external input entry point.
    /// Handles: undo checkpoint → restore to current → execute → record → invalidate.
    /// All external inputs (player swap, tap, server push, etc.) must go through here.
    /// </summary>
    /// <returns>True if the command executed successfully.</returns>
    public bool InjectCommand(IGameCommand command)
    {
        // 1. Save undo checkpoint before the action
        _undoSystem?.SaveCheckpoint(_engine);

        // 2. Restore engine to current state (discard speculation)
        RestoreToSlot(_current);

        // 3. Execute the command
        bool success = command.Execute(_engine);

        if (success)
        {
            // 4. Record for replay
            _recorder?.RecordCommand(command);
        }

        // 5. Invalidate all speculated slots (whether command succeeded or not,
        //    because RestoreToSlot already reset the engine)
        InvalidateSpeculation();

        return success;
    }

    /// <summary>
    /// Perform undo via the undo system, invalidating speculation.
    /// </summary>
    /// <returns>The restored checkpoint, or null if undo is not available.</returns>
    public UndoCheckpoint? Undo()
    {
        if (_undoSystem == null) return null;

        var checkpoint = _undoSystem.Undo(_engine);
        if (checkpoint == null) return null;

        // Engine has been restored by UndoSystem.
        // Re-snapshot the restored state as current.
        SnapshotCurrent();
        InvalidateSpeculation();

        return checkpoint;
    }

    #endregion

    #region Internal

    private void TickAndStore()
    {
        var slot = _write % _bufferSize;

        // Prepare event storage for this slot
        _buffer[slot].PrepareEvents();

        // Tick with event collection (try/finally ensures collector is restored on exception)
        TickResult tickResult;
        var originalCollector = _engine.EventCollector;
        _engine.SetEventCollector(_tickCollector);
        try
        {
            tickResult = _engine.Tick(FixedDeltaTime);
            _tickCollector.DrainEventsTo(_buffer[slot].Events!);
        }
        finally
        {
            _engine.SetEventCollector(originalCollector);
        }

        // Snapshot the post-tick state (including stability from TickResult)
        var snapshot = _engine.CreateSnapshot();
        snapshot.Events = _buffer[slot].Events;
        snapshot.IsValid = true;
        snapshot.IsStable = tickResult.IsStable;
        StoreStateHash(ref snapshot);
        _buffer[slot] = snapshot;

        _write++;
    }

    private void EnsureFilledTo(int target)
    {
        // Clamp to prevent overwriting the current slot
        int maxTarget = _current + _bufferSize;
        if (target > maxTarget) target = maxTarget;

        while (_write < target)
            TickAndStore();
    }

    private void RestoreToSlot(int absoluteIndex)
    {
        var slot = absoluteIndex % _bufferSize;
        if (!_buffer[slot].IsValid)
            throw new InvalidOperationException(
                $"Cannot restore to invalid slot {slot} (absolute index {absoluteIndex}).");

        _engine.RestoreFromSnapshot(in _buffer[slot]);
    }

    private void InvalidateSpeculation()
    {
        // Mark all slots after current as invalid
        for (int i = _current + 1; i < _write; i++)
            _buffer[i % _bufferSize].Invalidate();

        // Reset write head: only current is valid
        _write = _current + 1;

        // Re-snapshot current from the (now restored) engine
        SnapshotCurrent();
    }

    private void SnapshotCurrent()
    {
        var slot = _current % _bufferSize;
        var snapshot = _engine.CreateSnapshot();
        snapshot.Events = _buffer[slot].Events;
        snapshot.Events?.Clear();
        snapshot.IsValid = true;
        snapshot.IsStable = _engine.IsStable();
        StoreStateHash(ref snapshot);
        _buffer[slot] = snapshot;
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private void StoreStateHash(ref EngineSnapshot snapshot)
    {
        snapshot.StateHash = StateHasher.ComputeHash(in snapshot.State);
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private void VerifySpeculation()
    {
        // Compare the stored hash (computed at snapshot time) with a fresh hash.
        // If they differ, something modified the state outside InjectCommand.
        var slot = _current % _bufferSize;
        ref var snapshot = ref _buffer[slot];
        if (!snapshot.IsValid || snapshot.StateHash == 0) return;

        var freshHash = StateHasher.ComputeHash(in snapshot.State);
        System.Diagnostics.Debug.Assert(freshHash == snapshot.StateHash,
            $"Speculative divergence at tick {snapshot.Tick}. " +
            $"Stored hash={snapshot.StateHash:X8}, fresh hash={freshHash:X8}. " +
            "An external system may have modified state without InjectCommand().");
    }

    #endregion

    public void Dispose()
    {
        // GameEngine does not own the SimulationEngine — it was injected externally.
        // The owner (GameSession) is responsible for disposing it.
    }
}
