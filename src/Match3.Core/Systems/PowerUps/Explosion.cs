using System.Collections.Generic;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.PowerUps;

/// <summary>
/// Represents an active explosion that expands in waves.
/// </summary>
public class Explosion
{
    public Position Origin;
    public int MaxRadius;
    public int CurrentWaveRadius;
    public float Timer;
    public float WaveInterval;

    /// <summary>
    /// Per-wave multiplier applied to WaveInterval after each wave.
    /// Values &lt; 1 make later waves faster (acceleration).
    /// Default 1.0 = constant speed.
    /// </summary>
    public float Acceleration;

    /// <summary>
    /// Receive lock duration applied to each destroyed cell.
    /// Defaults to <see cref="ReceiveLockTimings.ExplosionClear"/>.
    /// </summary>
    public float ReceiveLockDuration;

    /// <summary>
    /// All tiles affected by this explosion.
    /// Calculated at initialization.
    /// </summary>
    public HashSet<Position> AffectedArea;

    /// <summary>
    /// Lock tokens acquired for suspended tiles. Released when wave processes the tile.
    /// </summary>
    public List<LockToken> LockTokens;

    /// <summary>
    /// Positions that were actually locked by this explosion (used for null-scheduler path).
    /// Prevents spurious unlock of cells that were empty at creation time.
    /// </summary>
    public HashSet<Position> LockedPositions;

    public bool IsFinished => CurrentWaveRadius > MaxRadius;

    public Explosion()
    {
        AffectedArea = Pools.ObtainHashSet<Position>();
        LockTokens = Pools.ObtainList<LockToken>();
        LockedPositions = Pools.ObtainHashSet<Position>();
    }

    public void Initialize(Position origin, int radius, float interval, float acceleration = 1f)
    {
        Origin = origin;
        MaxRadius = radius;
        WaveInterval = interval;
        Acceleration = acceleration;
        ReceiveLockDuration = ReceiveLockTimings.ExplosionClear;
        CurrentWaveRadius = 0;
        Timer = 0f;
        AffectedArea.Clear();
        LockTokens.Clear();
        LockedPositions.Clear();
    }

    public void Release()
    {
        AffectedArea.Clear();
        LockTokens.Clear();
        LockedPositions.Clear();
    }
}
