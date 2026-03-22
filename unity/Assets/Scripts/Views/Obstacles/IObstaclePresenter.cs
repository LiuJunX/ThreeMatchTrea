namespace Match3.Unity.Views.Obstacles
{
    /// <summary>
    /// Strategy interface for obstacle-type-specific visual behavior.
    /// Pure logic object (not MonoBehaviour). ObstacleView delegates all visual calls to it.
    /// </summary>
    public interface IObstaclePresenter
    {
        /// <summary>
        /// Initialize visual appearance (mesh, materials) for the given stage and state.
        /// Called once when obstacle view is first set up.
        /// <paramref name="state"/> carries obstacle-specific data (e.g., ColorBox color variant).
        /// </summary>
        void Setup(ObstacleView view, byte stage, byte state);

        /// <summary>
        /// Apply damage animation at the given progress (0→1).
        /// <paramref name="newStage"/> is the stage after damage (already set on ObstacleVisual).
        /// At progress=1, the view should show the new stage appearance.
        /// </summary>
        void ApplyDamage(ObstacleView view, float progress, byte newStage);

        /// <summary>
        /// Apply destroy animation at the given progress (0→1).
        /// </summary>
        void ApplyDeath(ObstacleView view, float progress);

        /// <summary>
        /// Per-frame update for idle/ambient animations (shimmer, sway, etc.).
        /// Not progress-driven — receives real delta time.
        /// </summary>
        void OnUpdate(ObstacleView view, float dt);
    }
}
