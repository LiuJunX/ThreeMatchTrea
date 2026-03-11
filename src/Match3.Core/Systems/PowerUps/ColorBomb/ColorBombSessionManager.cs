using System;
using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Objectives;

namespace Match3.Core.Systems.PowerUps.ColorBomb;

/// <summary>
/// Manages ColorBomb sessions: color reservation, timed beam launching,
/// cell locking, re-scanning, and batch destruction.
///
/// Normal mode:  beam → arrive (shaking) → batch destroy
/// Combo mode:   beam → arrive (transform to bomb + Indestructible) → batch activate
/// </summary>
public sealed class ColorBombSessionManager : IColorBombSessionManager
{
    private readonly ColorBombConfig _config;
    private readonly ICoverSystem _coverSystem;
    private readonly IGroundSystem _groundSystem;
    private readonly ILevelObjectiveSystem? _objectiveSystem;
    private readonly LockScheduler? _lockScheduler;
    private readonly List<ColorBombSession> _sessions = new();
    private readonly HashSet<ElementType> _reservedColors = new();
    private int _nextSessionId;

    /// <summary>
    /// Lock combination applied to beam target cells.
    /// Drop: tile stays in cell. Receive: cell refuses incoming tiles.
    /// Swap: no player interaction. Matching: excluded from match detection.
    /// Targeting: no UFO targeting.
    /// </summary>
    private const CellLockType BeamTargetLock =
        CellLockType.Drop | CellLockType.Receive | CellLockType.Swap | CellLockType.Matching | CellLockType.Targeting;

    public ColorBombSessionManager(ColorBombConfig? config = null,
        ICoverSystem? coverSystem = null,
        IGroundSystem? groundSystem = null,
        ILevelObjectiveSystem? objectiveSystem = null,
        LockScheduler? lockScheduler = null)
    {
        _config = config ?? new ColorBombConfig();
        _coverSystem = coverSystem ?? new CoverSystem();
        _groundSystem = groundSystem ?? new GroundSystem();
        _objectiveSystem = objectiveSystem;
        _lockScheduler = lockScheduler;
    }

    public bool HasActiveSessions => _sessions.Count > 0;

    public bool IsColorReserved(ElementType color) => _reservedColors.Contains(color);

    public void CreateSession(ref GameState state, Position origin, int bombTileId,
        int tick, float simTime, IEventCollector events)
    {
        CreateSessionInternal(ref state, origin, bombTileId, ElementType.None, tick, simTime, events);
    }

    public void CreateComboSession(ref GameState state, Position origin, int bombTileId,
        ElementType comboBombType, int tick, float simTime, IEventCollector events)
    {
        CreateSessionInternal(ref state, origin, bombTileId, comboBombType, tick, simTime, events);
    }

    private void CreateSessionInternal(ref GameState state, Position origin, int bombTileId,
        ElementType comboBombType, int tick, float simTime, IEventCollector events)
    {
        // Pick target color: most frequent color excluding reserved colors
        var targetColor = PickTargetColor(in state);
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
        CollectTargets(in state, session);

        // Lock bomb origin with Receive — prevents drops into the bomb position
        // during the entire session. Released in ExecuteBatchDestroy/ExecuteBatchActivate.
        if (_lockScheduler != null)
        {
            var token = _lockScheduler.Acquire(ref state, origin, CellLockType.Receive);
            session.LockTokens.Add(token);
        }
        else
        {
            state.Lock(origin, CellLockType.Receive);
            session.LockedPositions.Add(origin);
        }

        // Shuffle targets randomly
        ShuffleTargets(session.PendingTargets, 0, state.Random);

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

    public void Update(ref GameState state, float deltaTime, int tick, float simTime,
        IEventCollector events, List<Position>? triggeredBombs = null)
    {
        for (int i = _sessions.Count - 1; i >= 0; i--)
        {
            var session = _sessions[i];

            // 1. Check for externally destroyed targets
            CheckExternalDestructions(ref state, session);

            // 2. Update active beams (flight progress)
            UpdateActiveBeams(ref state, session, deltaTime, tick, simTime, events);

            // 3. Phase-specific logic
            switch (session.Phase)
            {
                case ColorBombPhase.Shooting:
                    UpdateShooting(ref state, session, deltaTime, tick, simTime, events);
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
                    ExecuteBatchDestroy(ref state, session, tick, simTime, events);
                    session.Phase = ColorBombPhase.Done;
                    break;

                case ColorBombPhase.BatchActivate:
                    ExecuteBatchActivate(ref state, session, tick, simTime, events, triggeredBombs);
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

    public void Reset()
    {
        _sessions.Clear();
        _reservedColors.Clear();
        _nextSessionId = 0;
    }

    #region Target Selection

    private ElementType PickTargetColor(in GameState state)
    {
        // Count tiles per color, excluding reserved colors and special types
        Span<int> counts = stackalloc int[7]; // None(0) + Item1-Item6(1-6)

        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var tile = state.GetTile(x, y);
                if (!tile.Type.IsColor()) continue;
                if (_reservedColors.Contains(tile.Type)) continue;
                // Skip tiles already locked by another session (Targeting)
                if (state.IsLocked(x, y, CellLockType.Targeting)) continue;

                int idx = (int)tile.Type;
                if (idx >= 1 && idx <= 6)
                    counts[idx]++;
            }
        }

        // Find most frequent
        ElementType best = ElementType.None;
        int bestCount = 0;
        for (int i = 1; i <= 6; i++)
        {
            if (counts[i] > bestCount)
            {
                bestCount = counts[i];
                best = (ElementType)i;
            }
        }

        return best;
    }

    private void CollectTargets(in GameState state, ColorBombSession session)
    {
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var pos = new Position(x, y);
                if (session.TargetedPositions.Contains(pos)) continue;

                var tile = state.GetTile(x, y);
                if (tile.Type != session.TargetColor) continue;
                // Skip tiles still in motion (only target settled tiles)
                if (tile.IsFalling) continue;
                // Skip tiles already locked by another session
                if (state.IsLocked(x, y, CellLockType.Targeting)) continue;

                session.PendingTargets.Add(new BeamTarget
                {
                    Position = pos,
                    TileId = tile.Id
                });
                session.TargetedPositions.Add(pos);
            }
        }
    }

    private static void ShuffleTargets(List<BeamTarget> targets, int startIndex, Match3.Random.IRandom random)
    {
        // Fisher-Yates shuffle on [startIndex, Count)
        for (int i = targets.Count - 1; i > startIndex; i--)
        {
            int j = random.Next(startIndex, i + 1);
            (targets[i], targets[j]) = (targets[j], targets[i]);
        }
    }

    #endregion

    #region Shooting Phase

    private void UpdateShooting(ref GameState state, ColorBombSession session,
        float deltaTime, int tick, float simTime, IEventCollector events)
    {
        session.ShootTimer -= deltaTime;

        // Fire beams as timer allows
        while (session.ShootTimer <= 0f && session.PendingIndex < session.PendingTargets.Count)
        {
            FireNextBeam(ref state, session, tick, simTime, events);
            session.ShootTimer += _config.BeamInterval;
        }

        // All pending targets fired — try re-scan
        if (session.PendingIndex >= session.PendingTargets.Count)
        {
            if (session.ReScanCount < _config.MaxReScans)
            {
                int beforeCount = session.PendingTargets.Count;
                CollectTargets(in state, session);

                if (session.PendingTargets.Count > beforeCount)
                {
                    // Found new targets — in-place shuffle the new batch
                    ShuffleTargets(session.PendingTargets, beforeCount, state.Random);
                    session.ReScanCount++;
                }
                else
                {
                    TransitionFromShooting(session);
                }
            }
            else
            {
                TransitionFromShooting(session);
            }
        }
    }

    /// <summary>
    /// Transition from Shooting phase to the appropriate next phase.
    /// </summary>
    private void TransitionFromShooting(ColorBombSession session)
    {
        ReleaseColor(session);

        if (session.ActiveBeams.Count > 0)
        {
            session.Phase = ColorBombPhase.WaitingForBeams;
        }
        else
        {
            session.Phase = session.ComboBombType != ElementType.None
                ? ColorBombPhase.BatchActivate
                : ColorBombPhase.BatchDestroy;
        }
    }

    private void FireNextBeam(ref GameState state, ColorBombSession session,
        int tick, float simTime, IEventCollector events)
    {
        var target = session.PendingTargets[session.PendingIndex++];

        // Verify target tile still exists
        var currentTile = state.GetTile(target.Position.X, target.Position.Y);
        if (currentTile.Id != target.TileId || currentTile.Type != session.TargetColor)
        {
            // Target gone — skip (no lock was acquired yet)
            session.TargetedPositions.Remove(target.Position);
            return;
        }

        // Acquire lock when firing beam (prevents target from being moved/matched/targeted)
        if (_lockScheduler != null)
        {
            var token = _lockScheduler.Acquire(ref state, target.Position, BeamTargetLock);
            session.LockTokens.Add(token);
        }
        else
        {
            state.Lock(target.Position, BeamTargetLock);
            session.LockedPositions.Add(target.Position);
        }

        // Calculate flight time from distance
        float dx = target.Position.X - session.BombPosition.X;
        float dy = target.Position.Y - session.BombPosition.Y;
        float distance = MathF.Sqrt(dx * dx + dy * dy);
        float flightTime = MathF.Max(distance / _config.BeamSpeed, _config.MinFlightDuration);

        target.FlightTime = flightTime;
        target.ElapsedTime = 0f;

        session.ActiveBeams.Add(target);

        // Emit beam launched event
        if (events.IsEnabled)
        {
            events.Emit(new ColorBombBeamLaunchedEvent
            {
                Tick = tick,
                SimulationTime = simTime,
                BombTileId = session.BombTileId,
                Origin = new Vector2(session.BombPosition.X, session.BombPosition.Y),
                TargetPosition = target.Position,
                TargetTileId = target.TileId,
                FlightDuration = flightTime,
                BeamIndex = session.FiredBeamCount++
            });
        }
    }

    #endregion

    #region Beam Flight & Arrival

    private void UpdateActiveBeams(ref GameState state, ColorBombSession session,
        float deltaTime, int tick, float simTime, IEventCollector events)
    {
        for (int i = session.ActiveBeams.Count - 1; i >= 0; i--)
        {
            var beam = session.ActiveBeams[i];
            beam.ElapsedTime += deltaTime;
            session.ActiveBeams[i] = beam; // struct copy-back

            if (beam.ElapsedTime >= beam.FlightTime)
            {
                // Beam arrived
                session.ActiveBeams.RemoveAt(i);

                // Check if target tile still exists
                var tile = state.GetTile(beam.Position.X, beam.Position.Y);
                if (tile.Id == beam.TileId && tile.Type == session.TargetColor)
                {
                    // Combo mode: transform tile to bomb type on arrival
                    if (session.ComboBombType != ElementType.None)
                    {
                        // Transform tile — preserve ID for visual tracking
                        state.SetTile(beam.Position.X, beam.Position.Y,
                            new Tile(tile.Id, session.ComboBombType, beam.Position.X, beam.Position.Y)
                            { Position = tile.Position });

                        // Add Indestructible lock to prevent explosion chain-trigger
                        if (_lockScheduler != null)
                        {
                            var token = _lockScheduler.Acquire(ref state, beam.Position, CellLockType.Indestructible);
                            session.LockTokens.Add(token);
                        }
                        else
                        {
                            state.Lock(beam.Position, CellLockType.Indestructible);
                        }

                        // Emit transform event
                        if (events.IsEnabled)
                        {
                            events.Emit(new ColorBombComboTransformEvent
                            {
                                Tick = tick,
                                SimulationTime = simTime,
                                BombTileId = session.BombTileId,
                                TargetPosition = beam.Position,
                                TargetTileId = tile.Id,
                                NewBombType = session.ComboBombType
                            });
                        }
                    }

                    // Target alive — mark as arrived
                    session.ArrivedTargets.Add(beam);
                }
                else
                {
                    // Target destroyed externally — release lock
                    ReleaseAllLocksForPosition(ref state, session, beam.Position);
                    session.TargetedPositions.Remove(beam.Position);
                }
            }
        }
    }

    #endregion

    #region External Destruction Check

    private void CheckExternalDestructions(ref GameState state, ColorBombSession session)
    {
        // Check arrived targets
        for (int i = session.ArrivedTargets.Count - 1; i >= 0; i--)
        {
            var target = session.ArrivedTargets[i];
            var tile = state.GetTile(target.Position.X, target.Position.Y);

            // For combo mode, arrived targets have been transformed to ComboBombType.
            // For normal mode, they still have TargetColor.
            // In both cases, tile.Id must match and tile must not be None.
            if (tile.Id != target.TileId || tile.Type == ElementType.None)
            {
                // Destroyed externally — release all locks for this position
                ReleaseAllLocksForPosition(ref state, session, target.Position);
                session.ArrivedTargets.RemoveAt(i);
                session.TargetedPositions.Remove(target.Position);
            }
        }

        // Check active beams (in-flight targets — still have original color type)
        for (int i = session.ActiveBeams.Count - 1; i >= 0; i--)
        {
            var beam = session.ActiveBeams[i];
            var tile = state.GetTile(beam.Position.X, beam.Position.Y);
            if (tile.Id != beam.TileId || tile.Type == ElementType.None)
            {
                // Target destroyed while beam in flight — release lock
                ReleaseAllLocksForPosition(ref state, session, beam.Position);
                session.ActiveBeams.RemoveAt(i);
                session.TargetedPositions.Remove(beam.Position);
            }
        }
    }

    #endregion

    #region Batch Destruction (Normal Mode)

    private void ExecuteBatchDestroy(ref GameState state, ColorBombSession session,
        int tick, float simTime, IEventCollector events)
    {
        var destroyedPositions = new List<Position>(session.ArrivedTargets.Count);
        var destroyedTileIds = new List<int>(session.ArrivedTargets.Count);

        // First pass: collect valid targets, handle covers
        foreach (var target in session.ArrivedTargets)
        {
            var tile = state.GetTile(target.Position.X, target.Position.Y);
            if (tile.Id != target.TileId || tile.Type == ElementType.None)
                continue;

            // Check cover
            if (_coverSystem.IsTileProtected(in state, target.Position))
            {
                _coverSystem.TryDamageCover(ref state, target.Position, tick, simTime, events);
                continue;
            }

            destroyedPositions.Add(target.Position);
            destroyedTileIds.Add(tile.Id);
        }

        // Emit batch destroy event FIRST — Choreographer uses this to synchronize
        // _beamHitTimes before TileDestroyedEvents are processed
        if (events.IsEnabled)
        {
            events.Emit(new ColorBombBatchDestroyEvent
            {
                Tick = tick,
                SimulationTime = simTime,
                BombTileId = session.BombTileId,
                BombPosition = session.BombPosition,
                DestroyedPositions = destroyedPositions,
                DestroyedTileIds = destroyedTileIds
            });
        }

        // Second pass: destroy tiles and emit per-tile events
        for (int i = 0; i < destroyedPositions.Count; i++)
        {
            var pos = destroyedPositions[i];
            var tile = state.GetTile(pos.X, pos.Y);
            if (tile.Type == ElementType.None) continue;

            if (events.IsEnabled)
            {
                events.Emit(new TileDestroyedEvent
                {
                    Tick = tick,
                    SimulationTime = simTime,
                    TileId = tile.Id,
                    GridPosition = pos,
                    Type = tile.Type,
                    Reason = DestroyReason.BombEffect,
                    IsGoal = _objectiveSystem != null &&
                             _objectiveSystem.IsTarget(in state, ObjectiveTargetLayer.Tile, (int)tile.Type)
                });
            }

            _objectiveSystem?.OnTileDestroyed(ref state, tile.Type, tick, simTime, events);
            state.SetTile(pos.X, pos.Y, new Tile(0, ElementType.None, pos.X, pos.Y));
            _groundSystem.OnTileDestroyed(ref state, pos, tick, simTime, events);
        }

        // Release all remaining session locks
        ReleaseAllSessionLocks(ref state, session);

        // Apply timed Receive locks for destroyed positions (post-destruction grace period)
        if (_lockScheduler != null)
        {
            foreach (var pos in destroyedPositions)
            {
                _lockScheduler.Acquire(ref state, pos, CellLockType.Receive, ReceiveLockTimings.ColorBombBatchClear);
            }
        }

        session.ArrivedTargets.Clear();

        // Release color if still reserved (shouldn't be, but safety)
        ReleaseColor(session);
    }

    #endregion

    #region Batch Activation (Combo Mode)

    /// <summary>
    /// All beams arrived and targets transformed to bombs.
    /// Release locks, emit batch event, output bomb positions for activation by orchestrator.
    /// </summary>
    private void ExecuteBatchActivate(ref GameState state, ColorBombSession session,
        int tick, float simTime, IEventCollector events, List<Position>? triggeredBombs)
    {
        var activatedPositions = new List<Position>(session.ArrivedTargets.Count);
        var activatedTileIds = new List<int>(session.ArrivedTargets.Count);

        // Collect valid transformed bombs
        foreach (var target in session.ArrivedTargets)
        {
            var tile = state.GetTile(target.Position.X, target.Position.Y);
            if (tile.Id != target.TileId || tile.Type == ElementType.None)
                continue;

            activatedPositions.Add(target.Position);
            activatedTileIds.Add(tile.Id);
        }

        // Emit batch activate event
        if (events.IsEnabled)
        {
            events.Emit(new ColorBombComboBatchActivateEvent
            {
                Tick = tick,
                SimulationTime = simTime,
                BombTileId = session.BombTileId,
                BombPosition = session.BombPosition,
                ComboBombType = session.ComboBombType,
                ActivatedPositions = activatedPositions,
                ActivatedTileIds = activatedTileIds
            });
        }

        // Release ALL session locks (BeamTargetLock + Indestructible) before activation
        ReleaseAllSessionLocks(ref state, session);

        // Output bomb positions — orchestrator will call ActivateBomb for each
        if (triggeredBombs != null)
        {
            foreach (var pos in activatedPositions)
                triggeredBombs.Add(pos);
        }

        session.ArrivedTargets.Clear();

        // Release color if still reserved (shouldn't be, but safety)
        ReleaseColor(session);
    }

    #endregion

    #region Helpers

    private void ReleaseColor(ColorBombSession session)
    {
        _reservedColors.Remove(session.TargetColor);
    }

    /// <summary>
    /// Release the first lock token matching a specific cell position.
    /// Used for single-lock cleanup (e.g., beam target lost during flight, normal mode).
    /// </summary>
    private void ReleaseLockForPosition(ref GameState state, ColorBombSession session, Position pos)
    {
        if (_lockScheduler != null)
        {
            int cellIndex = state.Index(pos);
            for (int i = session.LockTokens.Count - 1; i >= 0; i--)
            {
                if (session.LockTokens[i].CellIndex == cellIndex)
                {
                    _lockScheduler.Release(ref state, session.LockTokens[i]);
                    session.LockTokens.RemoveAt(i);
                    break;
                }
            }
        }
        else
        {
            if (session.LockedPositions.Remove(pos))
                state.Unlock(pos, BeamTargetLock);
        }
    }

    /// <summary>
    /// Release ALL lock tokens matching a specific cell position.
    /// Combo mode may have multiple tokens per cell (BeamTargetLock + Indestructible).
    /// </summary>
    private void ReleaseAllLocksForPosition(ref GameState state, ColorBombSession session, Position pos)
    {
        if (_lockScheduler != null)
        {
            int cellIndex = state.Index(pos);
            for (int i = session.LockTokens.Count - 1; i >= 0; i--)
            {
                if (session.LockTokens[i].CellIndex == cellIndex)
                {
                    _lockScheduler.Release(ref state, session.LockTokens[i]);
                    session.LockTokens.RemoveAt(i);
                }
            }
        }
        else
        {
            if (session.LockedPositions.Remove(pos))
            {
                state.Unlock(pos, BeamTargetLock);
                // Combo mode also has Indestructible lock
                if (session.ComboBombType != ElementType.None)
                    state.Unlock(pos, CellLockType.Indestructible);
            }
        }
    }

    /// <summary>
    /// Release all remaining locks for the entire session.
    /// </summary>
    private void ReleaseAllSessionLocks(ref GameState state, ColorBombSession session)
    {
        if (_lockScheduler != null)
        {
            foreach (var token in session.LockTokens)
                _lockScheduler.Release(ref state, token);
            session.LockTokens.Clear();
        }
        else
        {
            var unlockFlags = BeamTargetLock;
            if (session.ComboBombType != ElementType.None)
                unlockFlags |= CellLockType.Indestructible;

            foreach (var pos in session.LockedPositions)
                state.Unlock(pos, unlockFlags);
            session.LockedPositions.Clear();
        }
    }

    #endregion
}
