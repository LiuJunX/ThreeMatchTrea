using System;
using Match3.Core.Choreography;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Objectives;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.PowerUps.ColorBomb;
using Match3.Core.Systems.Projectiles;
using Match3.Core.Systems.Swap;

namespace Match3.Core.Simulation;

/// <summary>
/// Central simulation coordinator with tick-based updates.
/// Provides event sourcing for presentation layer and high-speed simulation for AI.
/// </summary>
public sealed class SimulationEngine : IDisposable
{
    private readonly SimulationConfig _config;
    private readonly IPhysicsSimulation _physics;
    private readonly IRefillSystem _refill;
    private readonly IMatchFinder _matchFinder;
    private readonly IMatchProcessor _matchProcessor;
    private readonly IPowerUpHandler _powerUpHandler;
    private readonly SimulationOrchestrator _orchestrator;
    private readonly ISwapOperations _swapOperations;
    private readonly SimulationInputHandler _inputHandler;
    private readonly IDeadlockDetectionSystem? _deadlockDetector;
    private readonly IBoardShuffleSystem? _shuffleSystem;
    private readonly ILevelObjectiveSystem? _objectiveSystem;
    private readonly ICellEliminator? _cellEliminator;
    private readonly LockScheduler _lockScheduler;
    private readonly ChoreographyConfig? _choreographyConfig;

    private IEventCollector _eventCollector;
    private int _currentTick;
    private float _elapsedTime;
    private int _cascadeDepth;
    private int _tilesCleared;
    private int _matchesProcessed;
    private int _bombsActivated;

    // Pending move tracking for invalid swap revert (uses shared PendingMoveState)
    private PendingMoveState _pendingMoveState;
    private const float SwapAnimationDuration = 0.15f; // Match EventInterpreter.MoveDuration

    // Swap positions for bomb generation (cleared after first match processing)
    private Position _lastSwapFrom = Position.Invalid;
    private Position _lastSwapTo = Position.Invalid;

    // Deadlock protection: prevent infinite shuffle attempts
    private bool _shuffleFailed;

    /// <summary>
    /// Current game state.
    /// </summary>
    public GameState State { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the simulation is paused.
    /// </summary>
    public bool IsPaused { get; private set; }

    /// <summary>
    /// Match finder for external AI/auto-play usage.
    /// </summary>
    public IMatchFinder MatchFinder => _matchFinder;

    /// <summary>
    /// Current tick number.
    /// </summary>
    public int CurrentTick => _currentTick;

    /// <summary>
    /// Total elapsed simulation time in seconds.
    /// </summary>
    public float ElapsedTime => _elapsedTime;

    /// <summary>
    /// Event collector for the simulation.
    /// </summary>
    public IEventCollector EventCollector => _eventCollector;

    /// <summary>
    /// Central lock scheduler for cell lock lifecycle management.
    /// </summary>
    public LockScheduler Locks => _lockScheduler;

    /// <summary>
    /// Creates a new simulation engine.
    /// </summary>
    public SimulationEngine(
        GameState initialState,
        SimulationConfig config,
        IPhysicsSimulation physics,
        IRefillSystem refill,
        IMatchFinder matchFinder,
        IMatchProcessor matchProcessor,
        IPowerUpHandler powerUpHandler,
        IProjectileSystem? projectileSystem = null,
        IEventCollector? eventCollector = null,
        IExplosionSystem? explosionSystem = null,
        IDeadlockDetectionSystem? deadlockDetector = null,
        IBoardShuffleSystem? shuffleSystem = null,
        ILevelObjectiveSystem? objectiveSystem = null,
        IColorBombSessionManager? colorBombSessionManager = null,
        LockScheduler? lockScheduler = null,
        ChoreographyConfig? choreographyConfig = null,
        ICellEliminator? cellEliminator = null)
    {
        State = initialState;
        _config = config ?? new SimulationConfig();
        _physics = physics ?? throw new ArgumentNullException(nameof(physics));
        _refill = refill ?? throw new ArgumentNullException(nameof(refill));
        _matchFinder = matchFinder ?? throw new ArgumentNullException(nameof(matchFinder));
        _matchProcessor = matchProcessor ?? throw new ArgumentNullException(nameof(matchProcessor));
        _powerUpHandler = powerUpHandler ?? throw new ArgumentNullException(nameof(powerUpHandler));
        _eventCollector = eventCollector ?? NullEventCollector.Instance;
        _cellEliminator = cellEliminator;
        _objectiveSystem = objectiveSystem;
        _lockScheduler = lockScheduler ?? new LockScheduler();
        _choreographyConfig = choreographyConfig;
        _pendingMoveState = PendingMoveState.None;

        // Create orchestrator to coordinate subsystems
        _orchestrator = new SimulationOrchestrator(
            physics,
            refill,
            matchFinder,
            matchProcessor,
            powerUpHandler,
            projectileSystem ?? new ProjectileSystem(),
            explosionSystem ?? new ExplosionSystem(),
            objectiveSystem,
            colorBombSessionManager,
            _lockScheduler,
            choreographyConfig,
            cellEliminator);

        // Initialize shared swap operations with instant context
        var swapContext = new InstantSwapContext(SwapAnimationDuration);
        _swapOperations = new SwapOperations(_matchFinder, swapContext);

        // Initialize input handler for move/tap processing
        _inputHandler = new SimulationInputHandler(_swapOperations, powerUpHandler);

        // Initialize deadlock detection and shuffle systems
        _deadlockDetector = deadlockDetector;
        _shuffleSystem = shuffleSystem;

        _currentTick = 0;
        _elapsedTime = 0f;
    }

    /// <summary>
    /// Execute a single simulation tick.
    /// </summary>
    public TickResult Tick()
    {
        return Tick(_config.FixedDeltaTime);
    }

    /// <summary>
    /// Execute a single simulation tick with custom delta time.
    /// </summary>
    public TickResult Tick(float deltaTime)
    {
        if (IsPaused)
        {
            var currentState = State;
            return new TickResult
            {
                CurrentTick = _currentTick,
                ElapsedTime = _elapsedTime,
                IsStable = IsStable(),
                HasActiveProjectiles = _orchestrator.HasActiveProjectiles,
                HasFallingTiles = !_orchestrator.IsPhysicsStable(in currentState),
                HasPendingMatches = HasPendingMatches(),
                DeltaTime = 0f
            };
        }

        var state = State;

        // Clear selection when board is actively processing (gravity, matching, cascading)
        // This prevents the highlight from "sticking" to a position when the tile there changes.
        if (state.SelectedPosition != Position.Invalid && !IsStable())
        {
            state.SelectedPosition = Position.Invalid;
        }

        // Phase 0: Validate pending move — check for invalid swap revert
        // Capture bomb swap info before validation clears it
        var pendingBombSwap = _pendingMoveState.IsBombSwap && _pendingMoveState.NeedsValidation
            ? _pendingMoveState
            : (PendingMoveState?)null;

        _swapOperations.ValidatePendingMove(
            ref state,
            ref _pendingMoveState,
            deltaTime,
            _currentTick,
            _elapsedTime,
            _eventCollector);

        // Process bomb swap AFTER animation completes (validation cleared NeedsValidation)
        if (pendingBombSwap.HasValue && !_pendingMoveState.NeedsValidation)
        {
            var bomb = pendingBombSwap.Value;
            _inputHandler.FinalizeBombSwap(ref state, bomb.From, bomb.To,
                bomb.TileAIsBomb, bomb.TileBIsBomb, bomb.TileAIsColorBomb, bomb.TileBIsColorBomb,
                _currentTick, _elapsedTime, _eventCollector, ref _bombsActivated);
        }

        // Phase 1: Refill — spawn tiles at column tops before gravity pulls them
        _orchestrator.ProcessRefill(ref state);

        // Phase 2: Projectiles — update in-flight projectiles (UFO, beams)
        var projectileCount = _orchestrator.UpdateProjectiles(
            ref state,
            deltaTime,
            _currentTick,
            _elapsedTime,
            _eventCollector);
        _tilesCleared += projectileCount;

        // Phase 3: Explosions — process active explosion waves
        var bombCount = _orchestrator.UpdateExplosions(
            ref state,
            deltaTime,
            _currentTick,
            _elapsedTime,
            _eventCollector);
        _bombsActivated += bombCount;

        // Phase 3.5: ColorBomb sessions — beam timing, re-scan, batch destruction
        _orchestrator.UpdateColorBombSessions(
            ref state,
            deltaTime,
            _currentTick,
            _elapsedTime,
            _eventCollector);

        // Phase 3.6: Timed locks — auto-release expired locks
        _lockScheduler.Tick(ref state, deltaTime);

        // Phase 4: Physics — gravity simulation
        _orchestrator.UpdatePhysics(ref state, deltaTime);

        // Phase 5: Match processing — detect and process stable matches (skip during swap animation)
        if (!_pendingMoveState.NeedsValidation)
        {
            // Pass swap positions as foci for bomb generation priority
            Position[]? foci = null;
            if (_lastSwapFrom != Position.Invalid && _lastSwapTo != Position.Invalid)
            {
                foci = new[] { _lastSwapFrom, _lastSwapTo };
            }

            var matchCount = _orchestrator.ProcessMatches(ref state, _currentTick, _elapsedTime, _eventCollector, foci);
            if (matchCount > 0)
            {
                _matchesProcessed += matchCount;
                _cascadeDepth++;
                // Clear swap foci after first match processing (cascade matches don't use swap priority)
                _lastSwapFrom = Position.Invalid;
                _lastSwapTo = Position.Invalid;
                // Reset shuffle failed flag — board state changed, may have valid moves now
                _shuffleFailed = false;
            }
        }

        // Phase 5.5: Deadlock detection — shuffle if no valid moves remain
        // Skip if previous shuffle already failed (prevent infinite loop)
        if (_config.EnableDeadlockDetection && !_shuffleFailed &&
            _deadlockDetector != null && _shuffleSystem != null && IsStable())
        {
            if (!_deadlockDetector.HasValidMoves(in state))
            {
                // Emit deadlock detected event
                if (_eventCollector.IsEnabled)
                {
                    _eventCollector.Emit(new DeadlockDetectedEvent
                    {
                        Tick = _currentTick,
                        SimulationTime = _elapsedTime,
                        Score = state.Score,
                        MoveCount = state.MoveCount
                    });
                }

                // Shuffle until solvable
                bool shuffleSuccess = _shuffleSystem.ShuffleUntilSolvable(
                    ref state, _eventCollector,
                    maxAttempts: _config.ShuffleMaxAttempts,
                    tick: _currentTick,
                    simulationTime: _elapsedTime);

                if (!shuffleSuccess)
                {
                    // All attempts exhausted — level fails
                    state.LevelStatus = LevelStatus.Defeat;
                    _shuffleFailed = true;
                }
            }
        }

        // Phase 6: Tick counter — advance simulation time
        _currentTick++;
        _elapsedTime += deltaTime;

        // Phase 6.5: Safety net — clear stale physics on immovable tiles
        // Gravity's ProcessColumn handles this inline, but systems after gravity
        // (e.g., match processing) may create new tiles at immovable positions.
        ClearImmovableTilePhysicsState(ref state);

        State = state;

        var isStable = IsStable();

        // Phase 7: Level status — check objectives when stable
        if (isStable && _objectiveSystem != null)
        {
            _objectiveSystem.UpdateLevelStatus(ref state, _currentTick, _elapsedTime, _eventCollector);
            State = state;
        }

        return new TickResult
        {
            CurrentTick = _currentTick,
            ElapsedTime = _elapsedTime,
            IsStable = isStable,
            HasActiveProjectiles = _orchestrator.HasActiveProjectiles,
            HasFallingTiles = !_orchestrator.IsPhysicsStable(in state),
            HasPendingMatches = HasPendingMatches(),
            DeltaTime = deltaTime
        };
    }

    /// <summary>
    /// Run simulation until stable state.
    /// Optimized for AI - disables event collection.
    /// </summary>
    public SimulationResult RunUntilStable()
    {
        // Store original collector and disable events for performance
        var originalCollector = _eventCollector;
        _eventCollector = NullEventCollector.Instance;

        var initialScore = State.Score;
        _tilesCleared = 0;
        _matchesProcessed = 0;
        _bombsActivated = 0;
        _cascadeDepth = 0;

        int tickCount = 0;

        try
        {
            while (!IsStable() && tickCount < _config.MaxTicksPerRun)
            {
                Tick(_config.FixedDeltaTime);
                tickCount++;
            }
        }
        finally
        {
            _eventCollector = originalCollector;
        }

        return new SimulationResult
        {
            TickCount = tickCount,
            FinalState = State.Clone(),
            ReachedStability = IsStable(),
            ElapsedTime = _elapsedTime,
            ScoreGained = State.Score - initialScore,
            TilesCleared = _tilesCleared,
            MatchesProcessed = _matchesProcessed,
            BombsActivated = _bombsActivated,
            MaxCascadeDepth = _cascadeDepth
        };
    }

    /// <summary>
    /// Apply a move (swap two tiles).
    /// </summary>
    public bool ApplyMove(Position from, Position to)
    {
        var state = State;
        bool result = _inputHandler.ApplyMove(
            from, to,
            ref state,
            ref _pendingMoveState,
            ref _lastSwapFrom,
            ref _lastSwapTo,
            _currentTick, _elapsedTime, _eventCollector);

        if (result)
        {
            State = state;
        }

        return result;
    }

    /// <summary>
    /// Clear stale physics state on tiles that cannot move (under covers, Drop locks, etc.).
    /// These tiles are considered "always stable" by IsTileStable, but their physics state
    /// (IsFalling, Velocity, Position) can become stale when set by systems that don't
    /// check movement restrictions (e.g., spawning, gravity inheritance).
    /// </summary>
    private static void ClearImmovableTilePhysicsState(ref GameState state)
    {
        for (int x = 0; x < state.Width; x++)
        {
            for (int y = 0; y < state.Height; y++)
            {
                var tile = state.GetTile(x, y);
                if (tile.Type == ElementType.None) continue;
                if (state.CanMove(x, y)) continue;

                // Tile is immovable — clear any stale physics state
                if (tile.IsFalling || tile.Velocity.X != 0 || tile.Velocity.Y != 0 ||
                    tile.Position.X != x || tile.Position.Y != y)
                {
                    tile.IsFalling = false;
                    tile.Velocity.X = 0;
                    tile.Velocity.Y = 0;
                    tile.Position.X = x;
                    tile.Position.Y = y;
                    state.SetTile(x, y, tile);
                }
            }
        }
    }

    /// <summary>
    /// Activate a bomb at the specified position.
    /// </summary>
    public void ActivateBomb(Position position)
    {
        var state = State;
        _powerUpHandler.ActivateBomb(ref state, position, _currentTick, _elapsedTime, _eventCollector);
        _bombsActivated++;
        State = state;
    }

    /// <summary>
    /// Set the paused state of the simulation.
    /// </summary>
    public void SetPaused(bool paused)
    {
        IsPaused = paused;
    }

    /// <summary>
    /// Set the selected position for input handling.
    /// </summary>
    public void SetSelectedPosition(Position position)
    {
        var state = State;
        state.SelectedPosition = position;
        State = state;
    }

    /// <summary>
    /// Handle a tap interaction at the specified position.
    /// Handles bomb activation, selection, and swap logic.
    /// </summary>
    public void HandleTap(Position p)
    {
        var state = State;
        _inputHandler.HandleTap(
            p, ref state,
            ref _pendingMoveState,
            ref _lastSwapFrom,
            ref _lastSwapTo,
            _currentTick, _elapsedTime, _eventCollector,
            ref _bombsActivated);
        State = state;
    }

    /// <summary>
    /// Check if simulation is in stable state.
    /// </summary>
    public bool IsStable()
    {
        var state = State;
        return _orchestrator.IsPhysicsStable(in state)
            && !_orchestrator.HasActiveProjectiles
            && !_orchestrator.HasActiveExplosions
            && !_orchestrator.HasActiveColorBombSessions
            && !_lockScheduler.HasTimedLocks
            && !HasPendingMatches()
            && !_pendingMoveState.HasPending;
    }

    /// <summary>
    /// Clone the engine for parallel simulation (AI branching / DryRun).
    /// <para>
    /// Thread-safety: the returned engine has fully independent mutable state
    /// (GameState, Physics, Config, frame buffers) and can run on a background
    /// thread concurrently with the original.
    /// </para>
    /// <para>
    /// Stateless systems (MatchFinder, MatchProcessor, DeadlockDetector, ShuffleSystem,
    /// ObjectiveSystem) are safely shared — they have no mutable instance fields and use
    /// ThreadLocal pools.
    /// </para>
    /// </summary>
    public SimulationEngine Clone(Match3.Random.IRandom newRandom)
    {
        var clonedState = State.Clone(newRandom);

        // Clone mutable systems — each clone needs independent frame buffers and RNG
        var cloneRandom = newRandom;
        var cloneConfig = _config.Clone();
        var clonePhysics = _physics.CloneForSimulation(cloneRandom);

        var cloneContext = new SimulationContext(
            new CellEliminator(new CoverSystem(_objectiveSystem), new GroundSystem(_objectiveSystem), _objectiveSystem),
            BombEffectRegistry.CreateDefault(),
            _lockScheduler.Clone());
        var cloneExplosion = new ExplosionSystem(cloneContext);
        var cloneProjectile = new ProjectileSystem();
        var cloneColorBomb = new ColorBombSessionManager(cloneContext);
        var clonePowerUp = PowerUpHandlerFactory.CloneForSimulation(
            (BombResolution)_powerUpHandler,
            cloneContext,
            cloneExplosion,
            cloneProjectile,
            cloneColorBomb);

        // Shared stateless systems: _matchFinder, _matchProcessor, _deadlockDetector,
        // _shuffleSystem, _objectiveSystem — safe to share (no mutable instance fields).
        // _refill is safe when SpawnModel uses state.Random (per-clone) instead of a stored IRandom.
        return new SimulationEngine(
            clonedState,
            cloneConfig,
            clonePhysics,
            _refill,
            _matchFinder,
            _matchProcessor,
            clonePowerUp,
            cloneProjectile,
            NullEventCollector.Instance,
            cloneExplosion,
            _deadlockDetector,
            _shuffleSystem,
            _objectiveSystem,
            cloneColorBomb,
            cloneContext.LockScheduler,
            _choreographyConfig,
            cloneContext.CellEliminator
        );
    }

    /// <summary>
    /// Launch a projectile into the simulation.
    /// </summary>
    public void LaunchProjectile(Projectile projectile)
    {
        _orchestrator.ProjectileSystem.Launch(projectile, _currentTick, _elapsedTime, _eventCollector);
    }

    /// <summary>
    /// Gets the projectile system for advanced usage.
    /// </summary>
    public IProjectileSystem ProjectileSystem => _orchestrator.ProjectileSystem;

    /// <summary>
    /// Set a new event collector.
    /// </summary>
    public void SetEventCollector(IEventCollector collector)
    {
        _eventCollector = collector ?? NullEventCollector.Instance;
    }

    /// <summary>
    /// Reset simulation counters.
    /// </summary>
    public void ResetCounters()
    {
        _currentTick = 0;
        _elapsedTime = 0f;
        _tilesCleared = 0;
        _matchesProcessed = 0;
        _bombsActivated = 0;
        _cascadeDepth = 0;
    }

    private bool HasPendingMatches()
    {
        var state = State;
        return _orchestrator.HasPendingMatches(in state);
    }

    /// <summary>
    /// Acquire a cell lock via the central LockScheduler.
    /// Safe because CellLocks is a reference-type array shared between struct copies.
    /// </summary>
    public LockToken AcquireLock(Position pos, CellLockType types)
    {
        var state = State;
        return _lockScheduler.Acquire(ref state, pos, types);
    }

    /// <summary>
    /// Release a previously acquired cell lock via the central LockScheduler (idempotent).
    /// </summary>
    public void ReleaseLock(LockToken token)
    {
        var state = State;
        _lockScheduler.Release(ref state, token);
    }

    public void Dispose()
    {
        // Cleanup resources if needed
    }
}
