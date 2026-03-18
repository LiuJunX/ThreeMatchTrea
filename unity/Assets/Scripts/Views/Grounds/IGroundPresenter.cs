namespace Match3.Unity.Views.Grounds
{
    /// <summary>
    /// Strategy interface for ground-type-specific visual behavior.
    /// Pure logic object (not MonoBehaviour). GroundView delegates all visual calls to it.
    /// </summary>
    public interface IGroundPresenter
    {
        /// <summary>
        /// Initialize visual appearance (mesh, materials) for the given health.
        /// Called once when ground view is first set up.
        /// </summary>
        void Setup(GroundView view, byte health);

        /// <summary>
        /// Apply damage animation at the given progress (0->1).
        /// <paramref name="newHealth"/> is the health after damage (already set on GroundVisual).
        /// At progress=1, the view should show the new health appearance.
        /// </summary>
        void ApplyDamage(GroundView view, float progress, byte newHealth);

        /// <summary>
        /// Apply destroy animation at the given progress (0->1).
        /// </summary>
        void ApplyDeath(GroundView view, float progress);

        /// <summary>
        /// Per-frame update for idle/ambient animations.
        /// Not progress-driven — receives real delta time.
        /// </summary>
        void OnUpdate(GroundView view, float dt);
    }
}
