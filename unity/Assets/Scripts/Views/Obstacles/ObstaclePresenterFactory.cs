using Match3.Core.Models.Enums;

namespace Match3.Unity.Views.Obstacles
{
    /// <summary>
    /// Creates the appropriate <see cref="IObstaclePresenter"/> for an obstacle type.
    /// Hardcoded switch — matches Core layer style (no registry needed yet).
    /// </summary>
    internal static class ObstaclePresenterFactory
    {
        public static IObstaclePresenter Create(ObstacleType type)
        {
            return type switch
            {
                ObstacleType.Box => new BoxPresenter(),
                // Future: ObstacleType.Bush => new BushPresenter(),
                _ => new BoxPresenter(), // fallback: all types look like boxes until art is ready
            };
        }
    }
}
