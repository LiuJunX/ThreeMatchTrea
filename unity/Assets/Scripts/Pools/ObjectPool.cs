using System;
using System.Collections.Generic;
using UnityEngine;

namespace Match3.Unity.Pools
{
    /// <summary>
    /// Generic object pool for Unity components.
    /// </summary>
    public sealed class ObjectPool<T> where T : Component, IPoolable
    {
        private readonly Queue<T> _available = new();
        private readonly Func<T> _factory;
        private readonly Transform _parent;
        private readonly int _maxSize;

        /// <summary>
        /// Number of objects currently available in the pool.
        /// </summary>
        public int AvailableCount => _available.Count;

        /// <summary>
        /// Creates a new object pool.
        /// </summary>
        /// <param name="factory">Factory function to create new instances.</param>
        /// <param name="parent">Parent transform for pooled objects.</param>
        /// <param name="initialSize">Initial pool size.</param>
        /// <param name="maxSize">Maximum pool size (0 = unlimited).</param>
        public ObjectPool(Func<T> factory, Transform parent, int initialSize = 10, int maxSize = 0)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _parent = parent;
            _maxSize = maxSize;

            // Pre-warm pool
            for (int i = 0; i < initialSize; i++)
            {
                var item = CreateNew();
                item.gameObject.SetActive(false);
                _available.Enqueue(item);
            }
        }

        /// <summary>
        /// Rent an object from the pool.
        /// </summary>
        public T Rent()
        {
            T item;
            if (_available.Count > 0)
            {
                item = _available.Dequeue();
            }
            else
            {
                item = CreateNew();
            }

            item.gameObject.SetActive(true);
            item.OnSpawn();
            return item;
        }

        /// <summary>
        /// Return an object to the pool.
        /// </summary>
        public void Return(T item)
        {
            if (item == null) return;

            item.OnDespawn();
            item.gameObject.SetActive(false);

            if (_maxSize > 0 && _available.Count >= _maxSize)
            {
                UnityEngine.Object.Destroy(item.gameObject);
                return;
            }

            _available.Enqueue(item);
        }

        /// <summary>
        /// Clear the pool and destroy all objects.
        /// </summary>
        public void Clear()
        {
            while (_available.Count > 0)
            {
                var item = _available.Dequeue();
                if (item != null && item.gameObject != null)
                {
                    UnityEngine.Object.Destroy(item.gameObject);
                }
            }
        }

        private T CreateNew()
        {
            var item = _factory();
            if (_parent != null)
            {
                item.transform.SetParent(_parent, false);
            }
            return item;
        }
    }
}
