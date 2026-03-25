using Match3.Core.Models.Enums;

namespace Match3.Unity.Views.Grounds
{
    /// <summary>
    /// Creates the appropriate <see cref="IGroundPresenter"/> for a ground type.
    /// Hardcoded switch — matches Core layer style (no registry needed yet).
    /// </summary>
    internal static class GroundPresenterFactory
    {
        public static IGroundPresenter Create(GroundType type)
        {
            return type switch
            {
                GroundType.Ice => new IcePresenter(),
                GroundType.Grass => new GrassPresenter(),
                GroundType.Leaves => new LeavesPresenter(),
                _ => new GrassPresenter(),
            };
        }
    }
}
