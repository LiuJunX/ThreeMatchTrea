using System;
using System.Numerics;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.PowerUps.ColorBomb;

/// <summary>
/// Handles the beam flight and shooting phase mechanics for ColorBomb sessions.
/// Manages beam launch timing, in-flight beam progress, arrival handling (including
/// combo transformation), and detection of externally destroyed targets.
/// </summary>
internal sealed class ColorBombBeamController
{
    private readonly ColorBombConfig _config;
    private readonly LockScheduler _lockScheduler;

    /// <summary>
    /// Lock combination applied to beam target cells.
    /// Drop: tile stays in cell. Receive: cell refuses incoming tiles.
    /// Swap: no player interaction. Matching: excluded from match detection.
    /// Targeting: no UFO targeting.
    /// </summary>
    internal const CellLockType BeamTargetLock =
        CellLockType.Drop | CellLockType.Receive | CellLockType.Swap | CellLockType.Matching | CellLockType.Targeting;

    /// <summary>
    /// Creates a new <see cref="ColorBombBeamController"/>.
    /// </summary>
    /// <param name="config">ColorBomb configuration for beam speed, interval, etc.</param>
    /// <param name="lockScheduler">Lock scheduler for cell lock lifecycle management.</param>
    public ColorBombBeamController(ColorBombConfig config, LockScheduler lockScheduler)
    {
        _config = config;
        _lockScheduler = lockScheduler;
    }

    /// <summary>
    /// Advances the shooting phase: fires beams as the timer allows and returns
    /// a <see cref="ShootingResult"/> indicating what happened.
    /// The caller is responsible for handling re-scan and color release based on the result.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="session">The active ColorBomb session.</param>
    /// <param name="deltaTime">Time elapsed since last update.</param>
    /// <param name="tick">Current simulation tick.</param>
    /// <param name="simTime">Current simulation time.</param>
    /// <param name="events">Event collector for beam launch events.</param>
    /// <returns>Result indicating whether shooting continues, needs re-scan, or is finished.</returns>
    internal ShootingResult UpdateShooting(ref GameState state, ColorBombSession session,
        float deltaTime, int tick, float simTime, IEventCollector events)
    {
        session.ShootTimer -= deltaTime;

        // Fire beams as timer allows
        while (session.ShootTimer <= 0f && session.PendingIndex < session.PendingTargets.Count)
        {
            FireNextBeam(ref state, session, tick, simTime, events);
            session.ShootTimer += _config.BeamInterval;
        }

        // All pending targets fired — signal caller
        if (session.PendingIndex >= session.PendingTargets.Count)
        {
            if (session.ReScanCount < _config.MaxReScans)
            {
                return ShootingResult.NeedsReScan;
            }

            return ShootingResult.Finished;
        }

        return ShootingResult.Continuing;
    }

    /// <summary>
    /// Transitions the session from the Shooting phase to the appropriate next phase
    /// (WaitingForBeams, BatchDestroy, or BatchActivate) based on whether any beams
    /// are still in flight and whether this is a combo session.
    /// </summary>
    /// <param name="session">The active ColorBomb session.</param>
    internal void TransitionFromShooting(ColorBombSession session)
    {
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

    /// <summary>
    /// Fires a beam at the next pending target. Validates the target still exists,
    /// acquires cell locks, calculates flight time based on distance, and emits
    /// a <see cref="ColorBombBeamLaunchedEvent"/>.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="session">The active ColorBomb session.</param>
    /// <param name="tick">Current simulation tick.</param>
    /// <param name="simTime">Current simulation time.</param>
    /// <param name="events">Event collector for beam launch events.</param>
    internal void FireNextBeam(ref GameState state, ColorBombSession session,
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
        var lockToken = _lockScheduler.Acquire(ref state, target.Position, BeamTargetLock);
        session.LockTokens.Add(lockToken);

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

    /// <summary>
    /// Updates all active (in-flight) beams: advances elapsed time, handles arrival,
    /// performs combo transformation on arrival if applicable, and cleans up beams
    /// whose targets were destroyed externally.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="session">The active ColorBomb session.</param>
    /// <param name="deltaTime">Time elapsed since last update.</param>
    /// <param name="tick">Current simulation tick.</param>
    /// <param name="simTime">Current simulation time.</param>
    /// <param name="events">Event collector for combo transform events.</param>
    internal void UpdateActiveBeams(ref GameState state, ColorBombSession session,
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
                        var indestructibleToken = _lockScheduler.Acquire(ref state, beam.Position, CellLockType.Indestructible);
                        session.LockTokens.Add(indestructibleToken);

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

    /// <summary>
    /// Detects targets that were destroyed externally (by other game systems) and
    /// cleans up their locks and tracking data. Checks both arrived targets and
    /// in-flight beams.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="session">The active ColorBomb session.</param>
    internal void CheckExternalDestructions(ref GameState state, ColorBombSession session)
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

    /// <summary>
    /// Releases all lock tokens associated with a specific cell position.
    /// Combo mode may have multiple tokens per cell (BeamTargetLock + Indestructible).
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="session">The active ColorBomb session.</param>
    /// <param name="pos">The cell position whose locks should be released.</param>
    internal void ReleaseAllLocksForPosition(ref GameState state, ColorBombSession session, Position pos)
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
}

/// <summary>
/// Result of <see cref="ColorBombBeamController.UpdateShooting"/> indicating
/// what the caller should do next.
/// </summary>
internal enum ShootingResult
{
    /// <summary>Beams are still being fired; shooting phase continues.</summary>
    Continuing,

    /// <summary>All pending targets fired but re-scans remain; caller should collect new targets.</summary>
    NeedsReScan,

    /// <summary>All pending targets fired and no more re-scans; shooting phase is done.</summary>
    Finished
}
