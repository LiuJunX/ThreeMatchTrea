using System.Collections.Generic;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.PowerUps;

/// <summary>
/// Represents an active explosion that expands in waves.
/// No pre-locking — cells are locked only when the wave reaches them.
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

    public bool IsFinished => CurrentWaveRadius > MaxRadius;

    public Explosion()
    {
        AffectedArea = Pools.ObtainHashSet<Position>();
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
    }

    public void Release()
    {
        AffectedArea.Clear();
    }
}
