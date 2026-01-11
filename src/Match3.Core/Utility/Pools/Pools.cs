using System;
using System.Collections.Generic;

namespace Match3.Core.Utility.Pools
{
    /// <summary>
    /// Provides global access to shared object pools.
    /// </summary>
    public static class Pools
    {
        // Cache for List<T> pools to avoid recreating them.
        private static class ListPoolCache<T>
        {
            // Use the specialized BucketedListPool instead of GenericObjectPool
            public static readonly BucketedListPool<T> Instance = new BucketedListPool<T>();
        }

        // Cache for HashSet<T> pools.
        private static class HashSetPoolCache<T>
        {
            public static readonly IObjectPool<HashSet<T>> Instance = new GenericObjectPool<HashSet<T>>(
                generator: () => new HashSet<T>(),
                reset: set => set.Clear(),
                maxSize: 128
            );
        }

        // Cache for Queue<T> pools.
        private static class QueuePoolCache<T>
        {
            public static readonly IObjectPool<Queue<T>> Instance = new GenericObjectPool<Queue<T>>(
                generator: () => new Queue<T>(),
                reset: queue => queue.Clear(),
                maxSize: 128
            );
        }

        /// <summary>
        /// Gets a List&lt;T&gt; from the global pool.
        /// </summary>
        /// <typeparam name="T">The type of elements in the list.</typeparam>
        /// <param name="capacity">Optional initial capacity. Use this for large lists to ensure performance.</param>
        /// <returns>A cleared list ready for use.</returns>
        public static List<T> ObtainList<T>(int capacity = 0)
        {
            return ListPoolCache<T>.Instance.Get(capacity);
        }

        /// <summary>
        /// Gets a disposable wrapper for a pooled list.
        /// Use with 'using' statement to ensure the list is returned to the pool.
        /// </summary>
        /// <example>
        /// using var handle = Pools.ObtainDisposableList&lt;int&gt;(out var list);
        /// list.Add(1);
        /// </example>
        public static PooledList<T> ObtainDisposableList<T>(out List<T> list, int capacity = 0)
        {
            list = ObtainList<T>(capacity);
            return new PooledList<T>(list);
        }

        /// <summary>
        /// Returns a List&lt;T&gt; to the global pool.
        /// </summary>
        /// <typeparam name="T">The type of elements in the list.</typeparam>
        /// <param name="list">The list to return.</param>
        public static void Release<T>(List<T> list)
        {
            ListPoolCache<T>.Instance.Return(list);
        }

        /// <summary>
        /// Gets a HashSet&lt;T&gt; from the global pool.
        /// </summary>
        /// <typeparam name="T">The type of elements in the set.</typeparam>
        /// <returns>A cleared set ready for use.</returns>
        public static HashSet<T> ObtainHashSet<T>()
        {
            return HashSetPoolCache<T>.Instance.Get();
        }

        /// <summary>
        /// Returns a HashSet&lt;T&gt; to the global pool.
        /// </summary>
        /// <typeparam name="T">The type of elements in the set.</typeparam>
        /// <param name="set">The set to return.</param>
        public static void Release<T>(HashSet<T> set)
        {
            HashSetPoolCache<T>.Instance.Return(set);
        }

        /// <summary>
        /// Gets a Queue&lt;T&gt; from the global pool.
        /// </summary>
        /// <typeparam name="T">The type of elements in the queue.</typeparam>
        /// <returns>A cleared queue ready for use.</returns>
        public static Queue<T> ObtainQueue<T>()
        {
            return QueuePoolCache<T>.Instance.Get();
        }

        /// <summary>
        /// Returns a Queue&lt;T&gt; to the global pool.
        /// </summary>
        /// <typeparam name="T">The type of elements in the queue.</typeparam>
        /// <param name="queue">The queue to return.</param>
        public static void Release<T>(Queue<T> queue)
        {
            QueuePoolCache<T>.Instance.Return(queue);
        }

        // Cache for generic object pools.
        private static class GenericPoolCache<T> where T : class, new()
        {
            public static readonly IObjectPool<T> Instance = new GenericObjectPool<T>(
                generator: () => new T(),
                reset: null, // Caller is responsible for reset
                maxSize: 1024
            );
        }

        /// <summary>
        /// Gets an object of type T from the global pool.
        /// Caller is responsible for resetting the object state.
        /// </summary>
        /// <typeparam name="T">The type of object to retrieve. Must have a parameterless constructor.</typeparam>
        /// <returns>An instance of T.</returns>
        public static T Obtain<T>() where T : class, new()
        {
            return GenericPoolCache<T>.Instance.Get();
        }

        /// <summary>
        /// Returns an object of type T to the global pool.
        /// </summary>
        /// <typeparam name="T">The type of object to return.</typeparam>
        /// <param name="obj">The object to return.</param>
        public static void Release<T>(T obj) where T : class, new()
        {
            GenericPoolCache<T>.Instance.Return(obj);
        }

        /// <summary>
        /// Creates a new generic object pool.
        /// </summary>
        /// <typeparam name="T">The type of object to pool.</typeparam>
        /// <param name="generator">The function to create new objects.</param>
        /// <param name="reset">The action to reset objects on return.</param>
        /// <param name="maxSize">The maximum size of the pool.</param>
        /// <returns>A new object pool instance.</returns>
        public static IObjectPool<T> Create<T>(Func<T> generator, Action<T>? reset = null, int maxSize = 10000) where T : class
        {
            return new GenericObjectPool<T>(generator, reset, maxSize);
        }
    }
}
