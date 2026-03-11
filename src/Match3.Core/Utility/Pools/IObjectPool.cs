using System;

namespace Match3.Core.Utility.Pools;

/// <summary>
/// Defines a generic object pool.
/// </summary>
/// <remarks>
/// @ai-usage-note: 
/// - High-frequency objects (TileMove, MatchGroup, etc.) MUST be retrieved via this interface.
/// - DO NOT instantiate pooled objects using 'new' in the game loop.
/// </remarks>
/// <typeparam name="T">The type of the objects in the pool.</typeparam>
public interface IObjectPool<T>
{
    /// <summary>
    /// Gets an object from the pool.
    /// </summary>
    /// <returns>An instance of <typeparamref name="T"/>.</returns>
    T Get();

    /// <summary>
    /// Returns an object to the pool.
    /// </summary>
    /// <param name="item">The object to return.</param>
    void Return(T item);
}
