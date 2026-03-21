namespace Match3.Unity.Views.Covers
{
    /// <summary>
    /// Strategy interface for cover-type-specific visual behavior.
    /// Pure logic object (not MonoBehaviour). CoverView delegates all visual calls to it.
    /// </summary>
    public interface ICoverPresenter
    {
        /// <summary>
        /// Initialize visual appearance (mesh, materials, color) for the given health.
        /// Called once when cover view is first set up.
        /// </summary>
        void Setup(CoverView view, byte health);

        /// <summary>
        /// Apply destroy animation at the given progress (0→1).
        /// </summary>
        void ApplyDeath(CoverView view, float progress);

        /// <summary>
        /// Per-frame update for idle/ambient animations.
        /// Not progress-driven — receives real delta time.
        /// </summary>
        void OnUpdate(CoverView view, float dt);
    }
}
