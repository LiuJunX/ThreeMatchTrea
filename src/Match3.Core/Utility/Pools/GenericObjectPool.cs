using System;
using System.Collections.Generic;
using System.Threading;

namespace Match3.Core.Utility.Pools;

/// <summary>
/// A thread-safe, generic object pool implementation using ThreadLocal storage.
/// </summary>
/// <remarks>
/// @ai-usage-note:
/// - This implementation uses ThreadLocal to avoid lock contention in parallel scenarios.
/// - Each thread has its own pool, enabling efficient parallel execution.
/// - Prefer this for general-purpose pooling in the Core logic.
/// </remarks>
/// <typeparam name="T">The type of object to pool. Must be a reference type.</typeparam>
public class GenericObjectPool<T> : IObjectPool<T> where T : class
{
    private readonly ThreadLocal<Stack<T>> _items;
    private readonly Func<T> _generator;
    private readonly Action<T>? _reset;
    private readonly int _maxSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericObjectPool{T}"/> class.
    /// </summary>
    /// <param name="generator">The function used to create new items when the pool is empty.</param>
    /// <param name="reset">The action used to reset items when they are returned to the pool.</param>
    /// <param name="maxSize">The maximum number of items to keep in the pool per thread.</param>
    /// <exception cref="ArgumentNullException">Thrown if generator is null.</exception>
    public GenericObjectPool(Func<T> generator, Action<T>? reset = null, int maxSize = 10000)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _reset = reset;
        _maxSize = maxSize;
        _items = new ThreadLocal<Stack<T>>(() => new Stack<T>(), trackAllValues: false);
    }

    /// <summary>
    /// Gets an object from the pool.
    /// </summary>
    /// <returns>An instance of <typeparamref name="T"/>.</returns>
    public T Get()
    {
        var stack = _items.Value!;
        if (stack.Count > 0)
        {
            return stack.Pop();
        }
        return _generator();
    }

    /// <summary>
    /// Returns an object to the pool.
    /// </summary>
    /// <param name="item">The object to return.</param>
    public void Return(T item)
    {
        if (item == null) return;

        _reset?.Invoke(item);

        var stack = _items.Value!;
        if (stack.Count < _maxSize)
        {
            stack.Push(item);
        }
        // If the pool is full, we simply drop the item and let GC handle it.
    }
}
