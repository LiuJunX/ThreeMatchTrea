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
///     TileMovedEvent, TileDestroyedEvent, TileSpawnedEvent, TilesSwappedEvent
///
///   BombChoreographer (delegates color bomb effects to ColorBombEffectsChoreographer):
///     BombCreatedEvent, BombActivatedEvent, BombComboEvent
///
///   UfoChoreographer:
///     ProjectileLaunchedEvent, ProjectileMovedEvent, ProjectileRetargetedEvent, ProjectileImpactEvent
///
///   ColorBombChoreographer (session-based multi-tick events):
///     ColorBombSessionStartEvent, ColorBombBeamLaunchedEvent, ColorBombBatchDestroyEvent,
///     ColorBombComboTransformEvent, ColorBombComboBatchActivateEvent
///
///   ColorBombEffectsChoreographer (instant tap/swap color bomb + double color bomb):
///     (internal helper, not directly routed)
///
///   LayerChoreographer:
///     MatchDetectedEvent, CoverDestroyedEvent, GroundDestroyedEvent, ScoreAddedEvent,
///     ComboChangedEvent, MoveCompletedEvent, DeadlockDetectedEvent, BoardShuffledEvent,
///     ObjectiveProgressEvent, LevelCompletedEvent
/// </summary>
public sealed class Choreographer : IEventVisitor
{
    private readonly ChoreographerContext _ctx = new();
    private readonly TileChoreographer _tile;
    private readonly BombChoreographer _bomb;
    private readonly UfoChoreographer _ufo;
    private readonly ColorBombChoreographer _colorBomb;
    private readonly LayerChoreographer _layer;

    /// <summary>
    /// Initializes a new Choreographer with all domain sub-processors.
    /// </summary>
    public Choreographer()
    {
        _tile = new TileChoreographer(_ctx);
        _colorBomb = new ColorBombChoreographer(_ctx);
        var colorBombEffects = new ColorBombEffectsChoreographer(_ctx);
        _bomb = new BombChoreographer(_ctx, colorBombEffects);
        _ufo = new UfoChoreographer(_ctx);
        _layer = new LayerChoreographer(_ctx);
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
        _ctx.NextBeamId = -1;
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
    public void Visit(ProjectileLaunchedEvent evt) => _ufo.Visit(evt);

    /// <inheritdoc />
    public void Visit(ProjectileMovedEvent evt) => _ufo.Visit(evt);

    /// <inheritdoc />
    public void Visit(ProjectileRetargetedEvent evt) => _ufo.Visit(evt);

    /// <inheritdoc />
    public void Visit(ProjectileImpactEvent evt) => _ufo.Visit(evt);

    #endregion

    #region IEventVisitor — Color bomb session events

    /// <inheritdoc />
    public void Visit(ColorBombSessionStartEvent evt) => _colorBomb.Visit(evt);

    /// <inheritdoc />
    public void Visit(ColorBombBeamLaunchedEvent evt) => _colorBomb.Visit(evt);

    /// <inheritdoc />
    public void Visit(ColorBombBatchDestroyEvent evt) => _colorBomb.Visit(evt);

    /// <inheritdoc />
    public void Visit(ColorBombComboTransformEvent evt) => _colorBomb.Visit(evt);

    /// <inheritdoc />
    public void Visit(ColorBombComboBatchActivateEvent evt) => _colorBomb.Visit(evt);

    #endregion

    #region IEventVisitor — Layer / state events

    /// <inheritdoc />
    public void Visit(MatchDetectedEvent evt) => _layer.Visit(evt);

    /// <inheritdoc />
    public void Visit(CoverDestroyedEvent evt) => _layer.Visit(evt);

    /// <inheritdoc />
    public void Visit(GroundDestroyedEvent evt) => _layer.Visit(evt);

    /// <inheritdoc />
    public void Visit(ScoreAddedEvent evt) => _layer.Visit(evt);

    /// <inheritdoc />
    public void Visit(ComboChangedEvent evt) => _layer.Visit(evt);

    /// <inheritdoc />
    public void Visit(MoveCompletedEvent evt) => _layer.Visit(evt);

    /// <inheritdoc />
    public void Visit(DeadlockDetectedEvent evt) => _layer.Visit(evt);

    /// <inheritdoc />
    public void Visit(BoardShuffledEvent evt) => _layer.Visit(evt);

    /// <inheritdoc />
    public void Visit(ObjectiveProgressEvent evt) => _layer.Visit(evt);

    /// <inheritdoc />
    public void Visit(LevelCompletedEvent evt) => _layer.Visit(evt);

    #endregion
}
