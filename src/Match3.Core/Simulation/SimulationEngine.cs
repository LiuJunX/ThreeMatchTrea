using System;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
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
    private readonly IDeadlockDetectionSystem? _deadlockDetector;
    private readonly IBoardShuffleSystem? _shuffleSystem;
    private readonly ILevelObjectiveSystem? _objectiveSystem;
    private readonly LockScheduler _lockScheduler;

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
        LockScheduler? lockScheduler = null)
    {
        State = initialState;
        _config = config ?? new SimulationConfig();
        _physics = physics ?? throw new ArgumentNullException(nameof(physics));
        _refill = refill ?? throw new ArgumentNullException(nameof(refill));
        _matchFinder = matchFinder ?? throw new ArgumentNullException(nameof(matchFinder));
        _matchProcessor = matchProcessor ?? throw new ArgumentNullException(nameof(matchProcessor));
        _powerUpHandler = powerUpHandler ?? throw new ArgumentNullException(nameof(powerUpHandler));
        _eventCollector = eventCollector ?? NullEventCollector.Instance;
        _objectiveSystem = objectiveSystem;
        _lockScheduler = lockScheduler ?? new LockScheduler();
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
            colorBombSessionManager);

        // Initialize shared swap operations with instant context
        var swapContext = new InstantSwapContext(SwapAnimationDuration);
        _swapOperations = new SwapOperations(_matchFinder, swapContext);

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

        // 0. Validate pending move (check for invalid swap revert)
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
            ProcessBombSwap(ref state, bomb.From, bomb.To,
                bomb.TileAIsBomb, bomb.TileBIsBomb, bomb.TileAIsColorBomb, bomb.TileBIsColorBomb);
        }

        // 1. Refill empty columns
        _orchestrator.ProcessRefill(ref state);

        // 2. Update projectiles
        var projectileCount = _orchestrator.UpdateProjectiles(
            ref state,
            deltaTime,
            _currentTick,
            _elapsedTime,
            _eventCollector);
        _tilesCleared += projectileCount;

        // 3. Update explosions
        var bombCount = _orchestrator.UpdateExplosions(
            ref state,
            deltaTime,
            _currentTick,
            _elapsedTime,
            _eventCollector);
        _bombsActivated += bombCount;

        // 3.5. Update ColorBomb sessions (beam timing, re-scan, batch destruction)
        _orchestrator.UpdateColorBombSessions(
            ref state,
            deltaTime,
            _currentTick,
            _elapsedTime,
            _eventCollector);

        // 3.6. Tick timed locks (auto-release expired)
        _lockScheduler.Tick(ref state, deltaTime);

        // 4. Physics (gravity)
        _orchestrator.UpdatePhysics(ref state, deltaTime);

        // 5. Process stable matches (skip during swap animation to let tiles visually complete swap)
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
                // Reset shuffle failed flag - board state changed, may have valid moves now
                _shuffleFailed = false;
            }
        }

        // 5.5. Deadlock detection and auto-shuffle
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
                _shuffleSystem.ShuffleUntilSolvable(
                    ref state, _eventCollector, maxAttempts: _config.ShuffleMaxAttempts);

                if (!_deadlockDetector.HasValidMoves(in state))
                {
                    // Extreme case: still deadlocked after max attempts
                    // Mark as failed to prevent infinite shuffle loop
                    _shuffleFailed = true;
                }
            }
        }

        // 6. Update tick counter
        _currentTick++;
        _elapsedTime += deltaTime;

        State = state;

        var isStable = IsStable();

        // 7. Update level status when stable
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

        if (!state.IsValid(from) || !state.IsValid(to))
            return false;

        // Check if tiles are blocked by cover
        if (!state.CanInteract(from) || !state.CanInteract(to))
            return false;

        // Clear selection when a move is applied (swipe bypasses HandleTap)
        state.SelectedPosition = Position.Invalid;

        // Get tile info BEFORE swap
        var tileA = state.GetTile(from.X, from.Y);
        var tileB = state.GetTile(to.X, to.Y);
        var tileAId = tileA.Id;
        var tileBId = tileB.Id;

        // Check if either tile is a bomb or color bomb (before swap)
        bool tileAIsBomb = tileA.Type.IsBomb();
        bool tileBIsBomb = tileB.Type.IsBomb();
        bool tileAIsColorBomb = tileA.Type.IsColorBomb();
        bool tileBIsColorBomb = tileB.Type.IsColorBomb();
        bool hasSpecialMove = tileAIsBomb || tileBIsBomb;

        // Swap tiles in grid using shared operations
        _swapOperations.SwapTiles(ref state, from, to);

        // Check if swap creates a match (check both positions)
        var hadMatch = _swapOperations.HasMatch(in state, from) || _swapOperations.HasMatch(in state, to);

        // If there's a bomb involved, treat as valid move (no revert)
        // Bomb effects will be processed AFTER swap animation completes
        if (hasSpecialMove)
        {
            hadMatch = true;
        }

        // Track pending move for potential revert (or bomb processing)
        _pendingMoveState = new PendingMoveState
        {
            From = from,
            To = to,
            TileAId = tileAId,
            TileBId = tileBId,
            HadMatch = hadMatch,
            NeedsValidation = true,
            AnimationTime = 0f,
            // Store bomb swap info for delayed processing
            IsBombSwap = hasSpecialMove,
            TileAIsBomb = tileAIsBomb,
            TileBIsBomb = tileBIsBomb,
            TileAIsColorBomb = tileAIsColorBomb,
            TileBIsColorBomb = tileBIsColorBomb
        };

        // Save swap positions for bomb generation priority
        // Note: 'from' is where player started drag, 'to' is destination
        // After swap, tiles have swapped places, so:
        // - Original tile at 'from' is now at 'to'
        // - Original tile at 'to' is now at 'from'
        // Per bomb-generation.md: bomb should spawn at player's "touched" positions
        _lastSwapFrom = from;
        _lastSwapTo = to;

        // Emit swap event
        if (_eventCollector.IsEnabled)
        {
            _eventCollector.Emit(new TilesSwappedEvent
            {
                Tick = _currentTick,
                SimulationTime = _elapsedTime,
                TileAId = tileAId,
                TileBId = tileBId,
                PositionA = from,
                PositionB = to,
                IsRevert = false
            });
        }

        State = state;
        return true;
    }

    /// <summary>
    /// Process bomb swap effects (combo or single bomb activation).
    /// </summary>
    private void ProcessBombSwap(
        ref GameState state,
        Position from,
        Position to,
        bool tileAIsBomb,
        bool tileBIsBomb,
        bool tileAIsColorBomb,
        bool tileBIsColorBomb)
    {
        // After swap:
        // - Original tile A (from) is now at position 'to'
        // - Original tile B (to) is now at position 'from'

        // Try combo first (handles: 彩球+普通, 炸弹+炸弹, 彩球+炸弹)
        _powerUpHandler.ProcessSpecialMove(
            ref state, from, to, _currentTick, _elapsedTime, _eventCollector, out int points);

        if (points > 0)
        {
            // Combo was processed
            _bombsActivated++;
            return;
        }

        // If no combo, activate single bomb
        // After swap:
        // - If tileA was bomb, it's now at 'to'
        // - If tileB was bomb, it's now at 'from'
        if (tileAIsBomb && !tileBIsBomb && !tileBIsColorBomb)
        {
            // Single bomb A + normal B: activate bomb at 'to' (where A is now)
            _powerUpHandler.ActivateBomb(ref state, to, _currentTick, _elapsedTime, _eventCollector);
            _bombsActivated++;
        }
        else if (tileBIsBomb && !tileAIsBomb && !tileAIsColorBomb)
        {
            // Single bomb B + normal A: activate bomb at 'from' (where B is now)
            _powerUpHandler.ActivateBomb(ref state, from, _currentTick, _elapsedTime, _eventCollector);
            _bombsActivated++;
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
        if (!state.IsValid(p)) return;

        var tile = state.GetTile(p.X, p.Y);

        // 1. Check for Bomb - single tap activates bomb
        if (tile.Type.IsBomb())
        {
            ActivateBomb(p);
            return;
        }

        // 2. Handle selection logic
        if (state.SelectedPosition == Position.Invalid)
        {
            // Nothing selected - select this tile (if not blocked by cover)
            if (state.CanInteract(p))
            {
                state.SelectedPosition = p;
            }
        }
        else if (state.SelectedPosition == p)
        {
            // Same tile tapped - deselect
            state.SelectedPosition = Position.Invalid;
        }
        else if (IsNeighbor(state.SelectedPosition, p))
        {
            // Adjacent tile tapped - swap
            var from = state.SelectedPosition;
            state.SelectedPosition = Position.Invalid;
            State = state;
            ApplyMove(from, p);
            return;
        }
        else
        {
            // Non-adjacent tile tapped - change selection (if not blocked by cover)
            if (state.CanInteract(p))
            {
                state.SelectedPosition = p;
            }
        }

        State = state;
    }

    private static bool IsNeighbor(Position a, Position b)
    {
        return System.Math.Abs(a.X - b.X) + System.Math.Abs(a.Y - b.Y) == 1;
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
            && !HasPendingMatches()
            && !_pendingMoveState.HasPending;
    }

    /// <summary>
    /// Clone the engine for parallel simulation (AI branching / DryRun).
    /// <para>
    /// Thread-safety: when <paramref name="newRandom"/> is provided, the returned engine
    /// has fully independent mutable state (GameState, Physics, Config, frame buffers)
    /// and can run on a background thread concurrently with the original.
    /// </para>
    /// <para>
    /// Stateless systems (MatchFinder, MatchProcessor, DeadlockDetector, ShuffleSystem,
    /// ObjectiveSystem) are safely shared — they have no mutable instance fields and use
    /// ThreadLocal pools.
    /// </para>
    /// </summary>
    public SimulationEngine Clone(Match3.Random.IRandom? newRandom = null)
    {
        var clonedState = State.Clone();
        if (newRandom != null)
        {
            clonedState.Random = newRandom;
        }

        // Clone mutable systems — each clone needs independent frame buffers and RNG
        var cloneRandom = newRandom ?? clonedState.Random;
        var cloneConfig = _config.Clone();
        var clonePhysics = _physics.CloneForSimulation(cloneRandom);

        var cloneLocks = _lockScheduler.Clone();
        var cloneCover = new CoverSystem(_objectiveSystem);
        var cloneGround = new GroundSystem(_objectiveSystem);
        var cloneExplosion = new ExplosionSystem(cloneCover, cloneGround, _objectiveSystem, cloneLocks);
        var cloneProjectile = new ProjectileSystem();
        var cloneColorBomb = new ColorBombSessionManager(null, cloneCover, cloneGround, _objectiveSystem);
        var clonePowerUp = _powerUpHandler.WithExplosionSystem(cloneExplosion).WithProjectileSystem(cloneProjectile);

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
            cloneLocks
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
