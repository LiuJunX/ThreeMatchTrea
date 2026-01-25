namespace Match3.Unity.Pools
{
    /// <summary>
    /// Interface for poolable components.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// Called when the object is taken from the pool.
        /// </summary>
        void OnSpawn();

        /// <summary>
        /// Called when the object is returned to the pool.
        /// </summary>
        void OnDespawn();
    }
}
