using Match3.Core.Models.Enums;

namespace Match3.Unity.Views.Covers
{
    /// <summary>
    /// Creates the appropriate <see cref="ICoverPresenter"/> for a given cover type.
    /// </summary>
    public static class CoverPresenterFactory
    {
        public static ICoverPresenter Create(CoverType type) => type switch
        {
            CoverType.Cage => new CagePresenter(),
            CoverType.Frost => new FrostPresenter(),
            _ => new FrostPresenter() // Fallback until other presenters are implemented
        };
    }
}
