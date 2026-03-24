using System.Collections.Generic;
using Match3.Core.Events;

namespace Match3.Core.Choreography;

/// <summary>
/// Converts GameEvent sequences into RenderCommand sequences with pre-calculated timing.
/// This enables deterministic replay and easy serialization.
///
/// Routing table — each Visit method delegates to the responsible sub-processor:
///
///   TileChoreographer:
///     TileMovedEvent, TileDamagedEvent, TileDestroyedEvent, TileSpawnedEvent, TilesSwappedEvent
///
///   BombChoreographer (delegates color bomb effects to ColorBombEffectsChoreographer):
///     BombCreatedEvent, BombActivatedEvent, BombComboEvent
///
///   ProjectileChoreographer:
///     ProjectileLaunchedEvent, ProjectileMovedEvent, ProjectileRetargetedEvent, ProjectileImpactEvent
///
///   ColorBombSessionChoreographer (session-based multi-tick events):
///     ColorBombSessionStartEvent, ColorBombBeamLaunchedEvent, ColorBombBatchDestroyEvent,
///     ColorBombComboTransformEvent, ColorBombComboBatchActivateEvent
///
///   ColorBombEffectsChoreographer (instant tap/swap color bomb + double color bomb):
///     (internal helper, not directly routed)
///
///   SurfaceChoreographer:
///     CoverDestroyedEvent, GroundDamagedEvent, GroundDestroyedEvent, GroundSpawnedEvent
///
///   ObstacleChoreographer:
///     ObstacleDamagedEvent, ObstacleDestroyedEvent, GeneratorActivatedEvent
///
///   BoardChoreographer:
///     MatchDetectedEvent, BoardShuffledEvent,
///     ScoreAddedEvent, ComboChangedEvent, MoveCompletedEvent, DeadlockDetectedEvent,
///     ObjectiveProgressEvent, LevelCompletedEvent
/// </summary>
public sealed class Choreographer : IEventVisitor
{
    private readonly ChoreographerContext _ctx = new();
    private readonly TileChoreographer _tile;
    private readonly BombChoreographer _bomb;
    private readonly ProjectileChoreographer _projectile;
    private readonly ColorBombSessionChoreographer _colorBombSession;
    private readonly SurfaceChoreographer _surface;
    private readonly ObstacleChoreographer _obstacle;
    private readonly BoardChoreographer _board;

    /// <summary>
    /// Initializes a new Choreographer with all domain sub-processors.
    /// </summary>
    public Choreographer()
    {
        _tile = new TileChoreographer(_ctx);
        _colorBombSession = new ColorBombSessionChoreographer(_ctx);
        var colorBombEffects = new ColorBombEffectsChoreographer(_ctx);
        _bomb = new BombChoreographer(_ctx, colorBombEffects);
        _projectile = new ProjectileChoreographer(_ctx);
        _surface = new SurfaceChoreographer(_ctx);
        _obstacle = new ObstacleChoreographer(_ctx);
        _board = new BoardChoreographer(_ctx);
    }

    /// <summary>
    /// Timing configuration for all choreography animations.
    /// </summary>
    public ChoreographyConfig Config
    {
        get => _ctx.Config;
        set => _ctx.Config = value;
    }

    /// <summary>
    /// Convert a sequence of game events into render commands.
    /// </summary>
    /// <param name="events">Events from simulation.</param>
    /// <param name="baseTime">Base timeline time for command scheduling.</param>
    /// <returns>List of render commands with pre-calculated timing.</returns>
    public IReadOnlyList<RenderCommand> Choreograph(IReadOnlyList<GameEvent> events, float baseTime = 0f)
    {
        _ctx.Commands.Clear();
        _ctx.BaseTime = baseTime;
        // Note: NextBeamId is NOT reset here — it must persist across batches
        // to avoid ID collisions when ColorBomb sessions fire beams across multiple ticks.
        // Note: BeamHitTimes is NOT cleared here — it must persist across batches
        // because ExplosionSystem emits BombActivatedEvent and TileDestroyedEvents
        // in separate ticks. Entries are consumed (Remove) in EmitBeamTargetDestroy.

        // Calculate minimum simulation time to use relative offsets
        // This ensures events start at baseTime, not baseTime + cumulative engine time
        _ctx.MinSimulationTime = 0f;
        if (events.Count > 0)
        {
            _ctx.MinSimulationTime = float.MaxValue;
            foreach (var evt in events)
            {
                if (evt.SimulationTime < _ctx.MinSimulationTime)
                    _ctx.MinSimulationTime = evt.SimulationTime;
            }
        }

        // Note: ActiveUfoFlights persists across batches because ProjectileSystem
        // may emit launch/retarget/impact events across multiple ticks.

        // Process each event
        foreach (var evt in events)
        {
            evt.Accept(this);
        }

        return _ctx.Commands;
    }

    #region IEventVisitor — Tile events

    /// <inheritdoc />
    public void Visit(TileMovedEvent evt) => _tile.Visit(evt);

    /// <inheritdoc />
    public void Visit(TileDamagedEvent evt) => _tile.Visit(evt);

    /// <inheritdoc />
    public void Visit(TileDestroyedEvent evt) => _tile.Visit(evt);

    /// <inheritdoc />
    public void Visit(TileSpawnedEvent evt) => _tile.Visit(evt);

    /// <inheritdoc />
    public void Visit(TilesSwappedEvent evt) => _tile.Visit(evt);

    #endregion

    #region IEventVisitor — Bomb events

    /// <inheritdoc />
    public void Visit(BombCreatedEvent evt) => _bomb.Visit(evt);

    /// <inheritdoc />
    public void Visit(BombActivatedEvent evt) => _bomb.Visit(evt);

    /// <inheritdoc />
    public void Visit(BombComboEvent evt) => _bomb.Visit(evt);

    #endregion

    #region IEventVisitor — Projectile / UFO events

    /// <inheritdoc />
    public void Visit(ProjectileLaunchedEvent evt) => _projectile.Visit(evt);

    /// <inheritdoc />
    public void Visit(ProjectileMovedEvent evt) => _projectile.Visit(evt);

    /// <inheritdoc />
    public void Visit(ProjectileRetargetedEvent evt) => _projectile.Visit(evt);

    /// <inheritdoc />
    public void Visit(ProjectileImpactEvent evt) => _projectile.Visit(evt);

    #endregion

    #region IEventVisitor — Color bomb session events

    /// <inheritdoc />
    public void Visit(ColorBombSessionStartEvent evt) => _colorBombSession.Visit(evt);

    /// <inheritdoc />
    public void Visit(ColorBombBeamLaunchedEvent evt) => _colorBombSession.Visit(evt);

    /// <inheritdoc />
    public void Visit(ColorBombBatchDestroyEvent evt) => _colorBombSession.Visit(evt);

    /// <inheritdoc />
    public void Visit(ColorBombComboTransformEvent evt) => _colorBombSession.Visit(evt);

    /// <inheritdoc />
    public void Visit(ColorBombComboBatchActivateEvent evt) => _colorBombSession.Visit(evt);

    #endregion

    #region IEventVisitor — Surface events

    /// <inheritdoc />
    public void Visit(CoverDestroyedEvent evt) => _surface.Visit(evt);

    /// <inheritdoc />
    public void Visit(GroundDamagedEvent evt) => _surface.Visit(evt);

    /// <inheritdoc />
    public void Visit(GroundDestroyedEvent evt) => _surface.Visit(evt);

    /// <inheritdoc />
    public void Visit(GroundSpawnedEvent evt) => _surface.Visit(evt);

    #endregion

    #region IEventVisitor — Obstacle events

    /// <inheritdoc />
    public void Visit(ObstacleDamagedEvent evt) => _obstacle.Visit(evt);

    /// <inheritdoc />
    public void Visit(ObstacleDestroyedEvent evt) => _obstacle.Visit(evt);

    /// <inheritdoc />
    public void Visit(GeneratorActivatedEvent evt) => _obstacle.Visit(evt);

    #endregion

    #region IEventVisitor — Board events

    /// <inheritdoc />
    public void Visit(MatchDetectedEvent evt) => _board.Visit(evt);

    /// <inheritdoc />
    public void Visit(BoardShuffledEvent evt) => _board.Visit(evt);

    /// <inheritdoc />
    public void Visit(ScoreAddedEvent evt) => _board.Visit(evt);

    /// <inheritdoc />
    public void Visit(ComboChangedEvent evt) => _board.Visit(evt);

    /// <inheritdoc />
    public void Visit(MoveCompletedEvent evt) => _board.Visit(evt);

    /// <inheritdoc />
    public void Visit(DeadlockDetectedEvent evt) => _board.Visit(evt);

    /// <inheritdoc />
    public void Visit(ObjectiveProgressEvent evt) => _board.Visit(evt);

    /// <inheritdoc />
    public void Visit(LevelCompletedEvent evt) => _board.Visit(evt);

    #endregion
}
