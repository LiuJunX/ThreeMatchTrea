using Match3.Core.Models.Enums;
using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views.Obstacles
{
    /// <summary>
    /// Presenter for Owl obstacle: stone statue, 1 stage (one-hit destroy).
    /// Only damaged by PowerUp sources. No damage animation (1HP → direct destroy).
    /// </summary>
    public sealed class OwlPresenter : IObstaclePresenter
    {
        private static readonly Color OwlColor = new Color(0.65f, 0.65f, 0.60f);

        public void Setup(ObstacleView view, byte stage, byte state)
        {
            var mesh = MeshFactory.GetObstacleMesh(ObstacleType.Owl);
            view.SetMesh(mesh);

            var mats = MeshFactory.GetObstacleMaterials(ObstacleType.Owl);
            if (mats != null)
                view.SetMaterials(mats);

            view.SetBaseColor(OwlColor);
        }

        public void ApplyDamage(ObstacleView view, float progress, byte newStage)
        {
            // 1HP — never called; Owl goes directly to Destroyed
        }

        public void ApplyDeath(ObstacleView view, float progress)
        {
            // Shatter: brief expand then rapid shrink + fade
            float scale = progress < 0.15f
                ? 1f + progress * 1.0f
                : Mathf.Lerp(1.15f, 0f, (progress - 0.15f) / 0.85f);
            float alpha = 1f - progress;

            view.SetLocalScaleMultiplier(scale);
            view.SetAlpha(alpha);
        }

        public void OnUpdate(ObstacleView view, float dt)
        {
            // No idle animation
        }
    }
}
