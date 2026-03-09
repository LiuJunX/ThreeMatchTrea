using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.PowerUps.ColorBomb;

/// <summary>
/// Tracks the lifecycle of a single ColorBomb activation:
/// Shooting → WaitingForBeams → BatchDestroy → Done.
/// </summary>
public sealed class ColorBombSession
{
    public int SessionId;
    public int BombTileId;
    public Position BombPosition;
    public ElementType TargetColor;
    public ColorBombPhase Phase;

    /// <summary>Targets not yet fired (shuffled order).</summary>
    public readonly List<BeamTarget> PendingTargets = new();

    /// <summary>Beams in flight (tracking elapsed time).</summary>
    public readonly List<BeamTarget> ActiveBeams = new();

    /// <summary>Beams that arrived and target is still alive (shaking).</summary>
    public readonly List<BeamTarget> ArrivedTargets = new();

    /// <summary>Lock tokens for cleanup on session end or external destroy.</summary>
    public readonly List<LockToken> LockTokens = new();

    /// <summary>Cursor into PendingTargets (replaces RemoveAt(0)).</summary>
    public int PendingIndex;

    /// <summary>Independent counter for BeamIndex in events.</summary>
    public int FiredBeamCount;

    /// <summary>Countdown to next beam launch.</summary>
    public float ShootTimer;

    /// <summary>How many re-scans have been performed.</summary>
    public int ReScanCount;

    /// <summary>Set of already-targeted positions (avoid duplicates across re-scans).</summary>
    public readonly HashSet<Position> TargetedPositions = new();

    public bool IsFinished => Phase == ColorBombPhase.Done;

    public void Reset()
    {
        SessionId = 0;
        BombTileId = 0;
        BombPosition = default;
        TargetColor = ElementType.None;
        Phase = ColorBombPhase.Done;
        PendingTargets.Clear();
        ActiveBeams.Clear();
        ArrivedTargets.Clear();
        LockTokens.Clear();
        PendingIndex = 0;
        FiredBeamCount = 0;
        ShootTimer = 0f;
        ReScanCount = 0;
        TargetedPositions.Clear();
    }
}

/// <summary>
/// A beam fired from a ColorBomb toward a target cell.
/// </summary>
public struct BeamTarget
{
    public Position Position;
    public int TileId;
    public float FlightTime;
    public float ElapsedTime;
}

/// <summary>
/// Lifecycle phases of a ColorBomb session.
/// </summary>
public enum ColorBombPhase
{
    /// <summary>Firing beams one by one with fixed interval.</summary>
    Shooting,
    /// <summary>All beams fired, waiting for in-flight beams to arrive.</summary>
    WaitingForBeams,
    /// <summary>All beams arrived, executing batch destruction.</summary>
    BatchDestroy,
    /// <summary>Session complete.</summary>
    Done
}
