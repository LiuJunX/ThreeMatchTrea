using Match3.Presentation;
using Match3.Unity.Bridge;

namespace Match3.Unity.Views
{
    /// <summary>
    /// Board view interface for 2D/3D rendering abstraction.
    /// </summary>
    public interface IBoardView
    {
        int ActiveTileCount { get; }
        void Initialize(Match3Bridge bridge);
        void Render(VisualState state, float dt);
        void Clear();
    }
}
