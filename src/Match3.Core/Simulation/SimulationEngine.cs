using System;
using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Models.Gameplay;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Projectiles;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Simulation;

/// <summary>
/// Central simulation coordinator with tick-based updates.
/// Provides event sourcing for presentation layer and high-speed simulation for AI.
/// </summary>
public sealed class SimulationEngine : IDisposable
{
    private readonly SimulationConfig _config;
    private readonly IPhysicsSimulation _physics;
    private readonly RealtimeRefillSystem _refill;
    private readonly IMatchFinder _matchFinder;
    private readonly IMatchProcessor _matchProcessor;
    private readonly IPowerUpHandler _powerUpHandler;
    private readonly IProjectileSystem _projectileSystem;

    private IEventCollector _eventCollector;
    private long _currentTick;
    private float _elapsedTime;
    private int _cascadeDepth;
    private int _tilesCleared;
    private int _matchesProcessed;
    private int _bombsActivated;

    /// <summary>
    /// Current game state.
    /// </summary>
    public GameState State { get; private set; }

    /// <summary>
    /// Current tick number.
    /// </summary>
    public long CurrentTick => _currentTick;

    /// <summary>
    /// Total elapsed simulation time in seconds.
    /// </summary>
    public float ElapsedTime => _elapsedTime;

    /// <summary>
    /// Event collector for the simulation.
    /// </summary>
    public IEventCollector EventCollector => _eventCollector;

    /// <summary>
    /// Creates a new simulation engine.
    /// </summary>
    public SimulationEngine(
        GameState initialState,
        SimulationConfig config,
        IPhysicsSimulation physics,
        RealtimeRefillSystem refill,
        IMatchFinder matchFinder,
        IMatchProcessor matchProcessor,
        IPowerUpHandler powerUpHandler,
        IProjectileSystem? projectileSystem = null,
        IEventCollector? eventCollector = null)
    {
        State = initialState;
        _config = config ?? new SimulationConfig();
        _physics = physics ?? throw new ArgumentNullException(nameof(physics));
        _refill = refill ?? throw new ArgumentNullException(nameof(refill));
        _matchFinder = matchFinder ?? throw new ArgumentNullException(nameof(matchFinder));
        _matchProcessor = matchProcessor ?? throw new ArgumentNullException(nameof(matchProcessor));
        _powerUpHandler = powerUpHandler ?? throw new ArgumentNullException(nameof(powerUpHandler));
        _projectileSystem = projectileSystem ?? new ProjectileSystem();
        _eventCollector = eventCollector ?? NullEventCollector.Instance;

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
        var state = State;

        // 1. Refill empty columns
        _refill.Update(ref state);

        // 2. Update projectiles
        var projectileAffected = _projectileSystem.Update(
            ref state,
            deltaTime,
            _currentTick,
            _elapsedTime,
            _eventCollector);

        // Process tiles affected by projectile impacts
        if (projectileAffected.Count > 0)
        {
            ProcessProjectileImpacts(ref state, projectileAffected);
            _tilesCleared += projectileAffected.Count;
        }
        Pools.Release(projectileAffected);

        // 3. Physics (gravity)
        _physics.Update(ref state, deltaTime);

        // 4. Process stable matches
        var matchCount = ProcessStableMatches(ref state);
        if (matchCount > 0)
        {
            _matchesProcessed += matchCount;
        }

        // 5. Update tick counter
        _currentTick++;
        _elapsedTime += deltaTime;

        State = state;

        var isStable = IsStable();

        return new TickResult
        {
            CurrentTick = _currentTick,
            ElapsedTime = _elapsedTime,
            IsStable = isStable,
            HasActiveProjectiles = _projectileSystem.HasActiveProjectiles,
            HasFallingTiles = !_physics.IsStable(in state),
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

        // Swap tiles in grid
        SwapTiles(ref state, from, to);

        // Emit swap event
        if (_eventCollector.IsEnabled)
        {
            var tileA = state.GetTile(from.X, from.Y);
            var tileB = state.GetTile(to.X, to.Y);

            _eventCollector.Emit(new TilesSwappedEvent
            {
                Tick = _currentTick,
                SimulationTime = _elapsedTime,
                TileAId = tileA.Id,
                TileBId = tileB.Id,
                PositionA = from,
                PositionB = to,
                IsRevert = false
            });
        }

        State = state;
        return true;
    }

    /// <summary>
    /// Activate a bomb at the specified position.
    /// </summary>
    public void ActivateBomb(Position position)
    {
        var state = State;
        _powerUpHandler.ActivateBomb(ref state, position);
        _bombsActivated++;
        State = state;
    }

    /// <summary>
    /// Check if simulation is in stable state.
    /// </summary>
    public bool IsStable()
    {
        var state = State;
        return _physics.IsStable(in state)
            && !_projectileSystem.HasActiveProjectiles
            && !HasPendingMatches();
    }

    /// <summary>
    /// Clone the engine for parallel simulation (AI branching).
    /// </summary>
    public SimulationEngine Clone(Match3.Random.IRandom? newRandom = null)
    {
        var clonedState = State.Clone();
        if (newRandom != null)
        {
            clonedState.Random = newRandom;
        }

        return new SimulationEngine(
            clonedState,
            _config,
            _physics,
            _refill,
            _matchFinder,
            _matchProcessor,
            _powerUpHandler,
            new ProjectileSystem(), // Each clone gets its own projectile system
            NullEventCollector.Instance // Clones always use null collector
        );
    }

    /// <summary>
    /// Launch a projectile into the simulation.
    /// </summary>
    public void LaunchProjectile(Projectile projectile)
    {
        _projectileSystem.Launch(projectile, _currentTick, _elapsedTime, _eventCollector);
    }

    /// <summary>
    /// Gets the projectile system for advanced usage.
    /// </summary>
    public IProjectileSystem ProjectileSystem => _projectileSystem;

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

    private void ProcessProjectileImpacts(ref GameState state, HashSet<Position> affectedPositions)
    {
        foreach (var pos in affectedPositions)
        {
            if (!state.IsValid(pos)) continue;

            var tile = state.GetTile(pos.X, pos.Y);
            if (tile.Type == TileType.None) continue;

            // Emit destruction event
            if (_eventCollector.IsEnabled)
            {
                _eventCollector.Emit(new TileDestroyedEvent
                {
                    Tick = _currentTick,
                    SimulationTime = _elapsedTime,
                    TileId = tile.Id,
                    GridPosition = pos,
                    Type = tile.Type,
                    Bomb = tile.Bomb,
                    Reason = DestroyReason.Projectile
                });
            }

            // Clear the tile
            state.SetTile(pos.X, pos.Y, new Tile());
        }
    }

    private int ProcessStableMatches(ref GameState state)
    {
        var allMatches = _matchFinder.FindMatchGroups(state);
        if (allMatches.Count == 0) return 0;

        var stableGroups = Pools.ObtainList<MatchGroup>();
        int processed = 0;

        try
        {
            foreach (var group in allMatches)
            {
                if (IsGroupStable(ref state, group))
                {
                    stableGroups.Add(group);

                    // Emit match event
                    if (_eventCollector.IsEnabled)
                    {
                        var positions = new List<Position>(group.Positions);
                        _eventCollector.Emit(new MatchDetectedEvent
                        {
                            Tick = _currentTick,
                            SimulationTime = _elapsedTime,
                            Type = group.Type,
                            Positions = positions,
                            Shape = DetermineMatchShape(group),
                            TileCount = group.Positions.Count
                        });
                    }
                }
            }

            if (stableGroups.Count > 0)
            {
                // Emit destruction events before tiles are cleared
                if (_eventCollector.IsEnabled)
                {
                    foreach (var group in stableGroups)
                    {
                        foreach (var pos in group.Positions)
                        {
                            // Skip position that will spawn a bomb
                            if (group.BombOrigin.HasValue && group.BombOrigin.Value == pos)
                                continue;

                            var tile = state.GetTile(pos.X, pos.Y);
                            if (tile.Type == TileType.None) continue;

                            _eventCollector.Emit(new TileDestroyedEvent
                            {
                                Tick = _currentTick,
                                SimulationTime = _elapsedTime,
                                TileId = tile.Id,
                                GridPosition = pos,
                                Type = tile.Type,
                                Bomb = tile.Bomb,
                                Reason = DestroyReason.Match
                            });
                        }
                    }
                }

                processed = stableGroups.Count;
                _matchProcessor.ProcessMatches(ref state, stableGroups);
                _cascadeDepth++;
            }
        }
        finally
        {
            Pools.Release(stableGroups);
        }

        return processed;
    }

    private bool IsGroupStable(ref GameState state, MatchGroup group)
    {
        foreach (var p in group.Positions)
        {
            var tile = state.GetTile(p.X, p.Y);
            if (tile.IsFalling) return false;
        }
        return true;
    }

    private bool HasPendingMatches()
    {
        var state = State;
        return _matchFinder.HasMatches(in state);
    }

    private void SwapTiles(ref GameState state, Position a, Position b)
    {
        var idxA = a.Y * state.Width + a.X;
        var idxB = b.Y * state.Width + b.X;
        var temp = state.Grid[idxA];
        state.Grid[idxA] = state.Grid[idxB];
        state.Grid[idxB] = temp;

        // Update positions
        state.Grid[idxA].Position = new System.Numerics.Vector2(a.X, a.Y);
        state.Grid[idxB].Position = new System.Numerics.Vector2(b.X, b.Y);
    }

    private MatchShape DetermineMatchShape(MatchGroup group)
    {
        // Use the shape from the group if already determined
        if (group.Shape != default)
            return group.Shape;

        // Fallback: Simple heuristic based on count
        return group.Positions.Count switch
        {
            <= 3 => MatchShape.Simple3,
            4 => MatchShape.Line4Horizontal,
            _ => MatchShape.Line5  // 5 or more
        };
    }

    public void Dispose()
    {
        // Cleanup resources if needed
    }
}
