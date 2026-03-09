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
/// </summary>
public sealed class ColorBombSessionManager : IColorBombSessionManager
{
    private readonly ColorBombConfig _config;
    private readonly ICoverSystem _coverSystem;
    private readonly IGroundSystem _groundSystem;
    private readonly ILevelObjectiveSystem? _objectiveSystem;
    private readonly List<ColorBombSession> _sessions = new();
    private readonly HashSet<ElementType> _reservedColors = new();
    private int _nextSessionId;

    /// <summary>
    /// Lock combination applied to beam target cells.
    /// Drop: tile stays in cell. Swap: no player interaction.
    /// Matching: excluded from match detection. Targeting: no UFO targeting.
    /// </summary>
    private const CellLockType BeamTargetLock =
        CellLockType.Drop | CellLockType.Swap | CellLockType.Matching | CellLockType.Targeting;

    public ColorBombSessionManager(ColorBombConfig? config = null,
        ICoverSystem? coverSystem = null,
        IGroundSystem? groundSystem = null,
        ILevelObjectiveSystem? objectiveSystem = null)
    {
        _config = config ?? new ColorBombConfig();
        _coverSystem = coverSystem ?? new CoverSystem();
        _groundSystem = groundSystem ?? new GroundSystem();
        _objectiveSystem = objectiveSystem;
    }

    public bool HasActiveSessions => _sessions.Count > 0;

    public bool IsColorReserved(ElementType color) => _reservedColors.Contains(color);

    public void CreateSession(ref GameState state, Position origin, int bombTileId,
        int tick, float simTime, IEventCollector events)
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
            Phase = ColorBombPhase.Shooting,
            ShootTimer = 0f, // Fire first beam immediately
            ReScanCount = 0
        };

        // Reserve color
        _reservedColors.Add(targetColor);

        // Collect initial targets
        CollectTargets(in state, session);

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
        IEventCollector events)
    {
        for (int i = _sessions.Count - 1; i >= 0; i--)
        {
            var session = _sessions[i];

            // 1. Check for externally destroyed targets
            CheckExternalDestructions(ref state, session);

            // 2. Update active beams (flight progress)
            UpdateActiveBeams(ref state, session, deltaTime);

            // 3. Phase-specific logic
            switch (session.Phase)
            {
                case ColorBombPhase.Shooting:
                    UpdateShooting(ref state, session, deltaTime, tick, simTime, events);
                    break;

                case ColorBombPhase.WaitingForBeams:
                    if (session.ActiveBeams.Count == 0)
                    {
                        session.Phase = ColorBombPhase.BatchDestroy;
                    }
                    break;

                case ColorBombPhase.BatchDestroy:
                    ExecuteBatchDestroy(ref state, session, tick, simTime, events);
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
                    // No new targets — release color and transition
                    ReleaseColor(session);
                    session.Phase = session.ActiveBeams.Count > 0
                        ? ColorBombPhase.WaitingForBeams
                        : ColorBombPhase.BatchDestroy;
                }
            }
            else
            {
                // Max re-scans reached — release color and transition
                ReleaseColor(session);
                session.Phase = session.ActiveBeams.Count > 0
                    ? ColorBombPhase.WaitingForBeams
                    : ColorBombPhase.BatchDestroy;
            }
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
            // Target gone — skip
            session.TargetedPositions.Remove(target.Position);
            return;
        }

        // Calculate flight time from distance
        float dx = target.Position.X - session.BombPosition.X;
        float dy = target.Position.Y - session.BombPosition.Y;
        float distance = MathF.Sqrt(dx * dx + dy * dy);
        float flightTime = MathF.Max(distance / _config.BeamSpeed, _config.MinFlightDuration);

        target.FlightTime = flightTime;
        target.ElapsedTime = 0f;

        // Lock the target cell
        var token = state.AcquireLock(target.Position.X, target.Position.Y, BeamTargetLock);
        session.LockTokens.Add(token);

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

    private void UpdateActiveBeams(ref GameState state, ColorBombSession session, float deltaTime)
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
                    // Target alive — mark as arrived (shaking)
                    session.ArrivedTargets.Add(beam);
                }
                else
                {
                    // Target destroyed externally — release lock
                    ReleaseLockForPosition(ref state, session, beam.Position);
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
            if (tile.Id != target.TileId || tile.Type == ElementType.None)
            {
                // Destroyed externally — release lock
                ReleaseLockForPosition(ref state, session, target.Position);
                session.ArrivedTargets.RemoveAt(i);
                session.TargetedPositions.Remove(target.Position);
            }
        }

        // Check active beams (in-flight targets)
        for (int i = session.ActiveBeams.Count - 1; i >= 0; i--)
        {
            var beam = session.ActiveBeams[i];
            var tile = state.GetTile(beam.Position.X, beam.Position.Y);
            if (tile.Id != beam.TileId || tile.Type == ElementType.None)
            {
                // Target destroyed while beam in flight — release lock
                ReleaseLockForPosition(ref state, session, beam.Position);
                session.ActiveBeams.RemoveAt(i);
                session.TargetedPositions.Remove(beam.Position);
            }
        }
    }

    #endregion

    #region Batch Destruction

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

        // Release all remaining locks
        foreach (var token in session.LockTokens)
        {
            // Only release if the lock is still held (check ref-count > 0)
            if (CellLockOps.AllLockedAboveZero(state.CellLocks[token.CellIndex], token.Types))
            {
                state.ReleaseLock(token);
            }
        }
        session.LockTokens.Clear();
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

    private void ReleaseLockForPosition(ref GameState state, ColorBombSession session, Position pos)
    {
        int cellIndex = pos.Y * state.Width + pos.X;
        for (int i = session.LockTokens.Count - 1; i >= 0; i--)
        {
            if (session.LockTokens[i].CellIndex == cellIndex)
            {
                var token = session.LockTokens[i];
                if (CellLockOps.AllLockedAboveZero(state.CellLocks[token.CellIndex], token.Types))
                {
                    state.ReleaseLock(token);
                }
                session.LockTokens.RemoveAt(i);
                break;
            }
        }
    }

    #endregion
}
