using Match3.Core.Models.Enums;
using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views.Obstacles
{
    /// <summary>
    /// Presenter for Stone obstacle: stone block, 3 stages.
    /// Only damaged by PowerUp sources. Heavy impact shake on damage.
    /// </summary>
    public sealed class StonePresenter : IObstaclePresenter
    {
        private byte _currentStage;

        // Stage colors: intact light gray (3) → cracked dark brown (1)
        private static readonly Color[] StageColors =
        {
            Color.gray,                              // stage 0 (unused)
            new Color(0.45f, 0.40f, 0.35f),          // stage 1 — cracked dark brown
            new Color(0.55f, 0.50f, 0.45f),          // stage 2 — cracked gray-brown
            new Color(0.65f, 0.60f, 0.55f),          // stage 3 — intact light gray
        };

        public void Setup(ObstacleView view, byte stage)
        {
            _currentStage = stage;

            var mesh = MeshFactory.GetObstacleMesh(ObstacleType.Stone);
            view.SetMesh(mesh);

            var mats = MeshFactory.GetObstacleMaterials(ObstacleType.Stone);
            if (mats != null)
                view.SetMaterials(mats);

            ApplyStageColor(view, stage);
        }

        public void ApplyDamage(ObstacleView view, float progress, byte newStage)
        {
            // Heavy impact shake (same style as Safe)
            float shake = Mathf.Sin(progress * Mathf.PI * 3f) * 0.12f * (1f - progress);
            view.SetLocalScaleMultiplier(1f + shake);

            if (_currentStage != newStage)
            {
                _currentStage = newStage;
                ApplyStageColor(view, newStage);
            }
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

        private static void ApplyStageColor(ObstacleView view, byte stage)
        {
            int idx = Mathf.Clamp(stage, 0, StageColors.Length - 1);
            view.SetBaseColor(StageColors[idx]);
        }
    }
}
