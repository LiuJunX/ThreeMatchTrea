using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views.Covers
{
    /// <summary>
    /// Visual presenter for Frost cover.
    /// Semi-transparent ice blue overlay on top of tile.
    /// Destroy animation: brief expansion then rapid shrink + fade.
    /// </summary>
    public sealed class FrostPresenter : ICoverPresenter
    {
        private static readonly Color FrostColor = new Color(0.7f, 0.85f, 1.0f, 0.6f);

        public void Setup(CoverView view, byte health)
        {
            view.SetMesh(MeshFactory.GetFallbackMesh());
            view.SetBaseColor(FrostColor);
        }

        public void ApplyDeath(CoverView view, float progress)
        {
            // Shatter feel: brief expansion then rapid shrink
            float scale = progress < 0.15f
                ? 1f + progress * 1.0f                                  // expand to ~1.15
                : Mathf.Lerp(1.15f, 0f, (progress - 0.15f) / 0.85f);  // shrink to 0
            float alpha = 1f - progress;

            view.SetLocalScaleMultiplier(scale);
            view.SetAlpha(alpha);
        }

        public void OnUpdate(CoverView view, float dt)
        {
            // No idle animation for Frost
        }
    }
}
