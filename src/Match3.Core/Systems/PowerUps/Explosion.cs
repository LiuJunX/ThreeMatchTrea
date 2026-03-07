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
        CurrentWaveRadius = 0;
        Timer = 0f;
        AffectedArea.Clear();
    }

    public void Release()
    {
        AffectedArea.Clear();
        // We don't release the HashSet itself back to the pool here because the Explosion object itself 
        // might be pooled or reused, and it keeps the reference.
        // If Explosion is pooled, we just clear the set.
        // If Explosion is NOT pooled (GC'd), we should release the set.
        // For now, let's assume Explosion is reused or we release the set manually.
    }
}
