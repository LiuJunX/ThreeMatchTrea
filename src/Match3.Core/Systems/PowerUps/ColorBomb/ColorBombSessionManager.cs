using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Layers;

namespace Match3.Core.Systems.PowerUps.ColorBomb;

/// <summary>
/// Manages ColorBomb sessions: color reservation, session lifecycle,
/// and phase orchestration. Delegates responsibilities to:
/// <list type="bullet">
///   <item><see cref="ColorBombTargetSelector"/> — target color picking, tile collection, shuffling</item>
///   <item><see cref="ColorBombBeamController"/> — beam launch timing, flight progress, arrival handling</item>
///   <item><see cref="ColorBombBatchProcessor"/> — batch destruction (normal) and activation (combo)</item>
/// </list>
///
/// Normal mode:  beam → arrive (shaking) → batch destroy
/// Combo mode:   beam → arrive (transform to bomb + Indestructible) → batch activate
/// </summary>
public sealed class ColorBombSessionManager : IColorBombSessionManager
{
    private readonly LockScheduler _lockScheduler;
    private readonly ColorBombBeamController _beamController;
    private readonly ColorBombBatchProcessor _batchProcessor;
    private readonly List<ColorBombSession> _sessions = new();
    private readonly HashSet<ElementType> _reservedColors = new();
    private int _nextSessionId;

    /// <summary>
    /// Creates a new ColorBombSessionManager.
    /// </summary>
    /// <param name="config">Optional configuration; defaults to <see cref="ColorBombConfig"/> defaults.</param>
    /// <param name="cellEliminator">Unified cell elimination pipeline; defaults to a new <see cref="CellEliminator"/>.</param>
    /// <param name="lockScheduler">
    /// Required lock scheduler for cell lock lifecycle management.
    /// Use <see cref="NullLockScheduler.Instance"/> in tests that do not need locking behavior.
    /// </param>
    public ColorBombSessionManager(ColorBombConfig? config = null,
        ICellEliminator? cellEliminator = null,
        LockScheduler? lockScheduler = null)
    {
        var cfg = config ?? new ColorBombConfig();
        _lockScheduler = lockScheduler ?? NullLockScheduler.Instance;
        _beamController = new ColorBombBeamController(cfg, _lockScheduler);
        _batchProcessor = new ColorBombBatchProcessor(
            cellEliminator ?? new CellEliminator(new CoverSystem(), new GroundSystem()),
            _lockScheduler);
    }

    public ColorBombSessionManager(SimulationContext context, ColorBombConfig? config = null)
        : this(config, context.CellEliminator, context.LockScheduler)
    {
    }

    /// <inheritdoc />
    public bool HasActiveSessions => _sessions.Count > 0;

    /// <inheritdoc />
    public bool IsColorReserved(ElementType color) => _reservedColors.Contains(color);

    /// <inheritdoc />
    public void CreateSession(ref GameState state, Position origin, int bombTileId,
        int tick, float simTime, IEventCollector events)
    {
        CreateSessionInternal(ref state, origin, bombTileId, ElementType.None, null, tick, simTime, events);
    }

    /// <inheritdoc />
    public void CreateSession(ref GameState state, Position origin, int bombTileId,
        ElementType targetColor, int tick, float simTime, IEventCollector events)
    {
        CreateSessionInternal(ref state, origin, bombTileId, ElementType.None, targetColor, tick, simTime, events);
    }

    /// <inheritdoc />
    public void CreateComboSession(ref GameState state, Position origin, int bombTileId,
        ElementType comboBombType, int tick, float simTime, IEventCollector events)
    {
        CreateSessionInternal(ref state, origin, bombTileId, comboBombType, null, tick, simTime, events);
    }

    private void CreateSessionInternal(ref GameState state, Position origin, int bombTileId,
        ElementType comboBombType, ElementType? specifiedColor, int tick, float simTime, IEventCollector events)
    {
        // Use specified color (swap with normal tile) or pick most frequent
        var targetColor = specifiedColor
            ?? ColorBombTargetSelector.PickTargetColor(in state, _reservedColors);
        if (targetColor == ElementType.None)
        {
            // No valid color available — ColorBomb does nothing
            return;
        }

        var session = new ColorBombSession
        {
            SessionId = _nextSessionId++,
            BombTileId = bombTileId,
            BombPosition = origin,
            TargetColor = targetColor,
            ComboBombType = comboBombType,
            Phase = ColorBombPhase.Shooting,
            ShootTimer = 0f, // Fire first beam immediately
            ReScanCount = 0
        };

        // Reserve color
        _reservedColors.Add(targetColor);

        // Collect initial targets
        ColorBombTargetSelector.CollectTargets(in state, session);

        // Lock bomb origin with Receive — prevents drops into the bomb position
        // during the entire session. Released in batch destroy/activate.
        var token = _lockScheduler.Acquire(ref state, origin, CellLockType.Receive);
        session.LockTokens.Add(token);

        // Shuffle targets randomly
        ColorBombTargetSelector.ShuffleTargets(session.PendingTargets, 0, state.Random);

        _sessions.Add(session);

        // Emit session start event
        if (events.IsEnabled)
        {
            events.Emit(new ColorBombSessionStartEvent
            {
                Tick = tick,
                SimulationTime = simTime,
                TileId = bombTileId,
                Position = origin,
                TargetColor = targetColor
            });
        }
    }

    /// <inheritdoc />
    public void Update(ref GameState state, float deltaTime, int tick, float simTime,
        IEventCollector events, List<Position>? triggeredBombs = null)
    {
        for (int i = _sessions.Count - 1; i >= 0; i--)
        {
            var session = _sessions[i];

            // 1. Check for externally destroyed targets
            _beamController.CheckExternalDestructions(ref state, session);

            // 2. Update active beams (flight progress)
            _beamController.UpdateActiveBeams(ref state, session, deltaTime, tick, simTime, events);

            // 3. Phase-specific logic
            switch (session.Phase)
            {
                case ColorBombPhase.Shooting:
                    HandleShootingPhase(ref state, session, deltaTime, tick, simTime, events);
                    break;

                case ColorBombPhase.WaitingForBeams:
                    if (session.ActiveBeams.Count == 0)
                    {
                        session.Phase = session.ComboBombType != ElementType.None
                            ? ColorBombPhase.BatchActivate
                            : ColorBombPhase.BatchDestroy;
                    }
                    break;

                case ColorBombPhase.BatchDestroy:
                    _batchProcessor.ExecuteBatchDestroy(ref state, session, tick, simTime, events);
                    ReleaseColor(session);
                    session.Phase = ColorBombPhase.Done;
                    break;

                case ColorBombPhase.BatchActivate:
                    _batchProcessor.ExecuteBatchActivate(ref state, session, tick, simTime, events, triggeredBombs);
                    ReleaseColor(session);
                    session.Phase = ColorBombPhase.Done;
                    break;
            }

            // Remove finished sessions
            if (session.IsFinished)
            {
                _sessions.RemoveAt(i);
            }
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        _sessions.Clear();
        _reservedColors.Clear();
        _nextSessionId = 0;
    }

    #region Shooting Phase

    /// <summary>
    /// Handles the shooting phase by delegating beam firing to <see cref="ColorBombBeamController"/>
    /// and managing re-scan and phase transition based on the result.
    /// </summary>
    private void HandleShootingPhase(ref GameState state, ColorBombSession session,
        float deltaTime, int tick, float simTime, IEventCollector events)
    {
        var result = _beamController.UpdateShooting(ref state, session, deltaTime, tick, simTime, events);

        switch (result)
        {
            case ShootingResult.NeedsReScan:
            {
                int beforeCount = session.PendingTargets.Count;
                ColorBombTargetSelector.CollectTargets(in state, session);

                if (session.PendingTargets.Count > beforeCount)
                {
                    // Found new targets — in-place shuffle the new batch
                    ColorBombTargetSelector.ShuffleTargets(session.PendingTargets, beforeCount, state.Random);
                    session.ReScanCount++;
                }
                else
                {
                    // Color stays reserved until BatchDestroy/BatchActivate
                    _beamController.TransitionFromShooting(session);
                }
                break;
            }
            case ShootingResult.Finished:
                // Color stays reserved until BatchDestroy/BatchActivate
                _beamController.TransitionFromShooting(session);
                break;
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Releases the reserved color for the given session.
    /// </summary>
    private void ReleaseColor(ColorBombSession session)
    {
        _reservedColors.Remove(session.TargetColor);
    }

    #endregion
}
