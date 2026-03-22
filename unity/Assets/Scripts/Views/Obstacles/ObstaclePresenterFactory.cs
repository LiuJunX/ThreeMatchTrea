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
                ObstacleType.Bush => new BushPresenter(),
                ObstacleType.Safe => new SafePresenter(),
                ObstacleType.Cupboard => new CupboardPresenter(),
                ObstacleType.Owl => new OwlPresenter(),
                ObstacleType.Stone => new StonePresenter(),
                ObstacleType.ColorBox => new ColorBoxPresenter(),
                ObstacleType.MagicHat => new MagicHatPresenter(),
                _ => new BoxPresenter(), // fallback: all types look like boxes until art is ready
            };
        }
    }
}
