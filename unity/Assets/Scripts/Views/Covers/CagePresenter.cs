using Match3.Core.Models.Enums;
using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views.Covers
{
    /// <summary>
    /// Presenter for Cage cover: golden metallic cage overlay on tile.
    /// Blocks matching and swap. Shake on death with shrink + fade.
    /// </summary>
    public sealed class CagePresenter : ICoverPresenter
    {
        private static readonly Color CageColor = new Color(0.72f, 0.55f, 0.25f, 0.9f);

        public void Setup(CoverView view, byte health)
        {
            var mesh = MeshFactory.GetCoverMesh(CoverType.Cage);
            view.SetMesh(mesh);

            var mats = MeshFactory.GetCoverMaterials(CoverType.Cage);
            if (mats != null)
                view.SetMaterials(mats);
            else
                view.SetBaseColor(CageColor);
        }

        public void ApplyDeath(CoverView view, float progress)
        {
            // Shake then shrink + fade
            float shake = Mathf.Sin(progress * Mathf.PI * 8f) * 0.08f * (1f - progress);
            float scale = progress < 0.3f
                ? 1f + shake
                : Mathf.Lerp(1f, 0f, (progress - 0.3f) / 0.7f);
            float alpha = 1f - progress;

            view.SetLocalScaleMultiplier(scale);
            view.SetAlpha(alpha);
        }

        public void OnUpdate(CoverView view, float dt)
        {
            // No idle animation for cage
        }
    }
}
