using Match3.Core.Models.Enums;
using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views.Obstacles
{
    /// <summary>
    /// Presenter for Safe obstacle: reinforced metal look, 5 stages.
    /// Only damaged by PowerUp sources. Heavy impact shake on damage,
    /// metallic flash idle shimmer.
    /// </summary>
    public sealed class SafePresenter : IObstaclePresenter
    {
        private byte _currentStage;
        private float _shimmerTimer;

        // Stage colors: shiny metal (5) → dull scratched (1)
        private static readonly Color[] StageColors =
        {
            Color.gray,                              // stage 0 (unused)
            new Color(0.40f, 0.40f, 0.42f),          // stage 1 — dull scratched steel
            new Color(0.48f, 0.48f, 0.50f),          // stage 2
            new Color(0.55f, 0.55f, 0.58f),          // stage 3
            new Color(0.62f, 0.62f, 0.66f),          // stage 4
            new Color(0.70f, 0.70f, 0.75f),          // stage 5 — polished steel
        };

        public void Setup(ObstacleView view, byte stage)
        {
            _currentStage = stage;
            _shimmerTimer = 0f;

            var mesh = MeshFactory.GetObstacleMesh(ObstacleType.Safe);
            view.SetMesh(mesh);

            var mats = MeshFactory.GetObstacleMaterials(ObstacleType.Safe);
            if (mats != null)
                view.SetMaterials(mats);

            ApplyStageColor(view, stage);
        }

        public void ApplyDamage(ObstacleView view, float progress, byte newStage)
        {
            // Heavy impact shake: lower frequency, higher amplitude than Box
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
            // Burst open: brief expand then rapid shrink
            float expand = Mathf.Clamp01(progress * 3f); // 0-0.33 -> 0-1
            float shrink = Mathf.Clamp01(progress * 1.5f - 0.5f); // 0.33-1 -> 0-1

            float scale = 1f + expand * 0.2f;
            scale *= 1f - shrink;
            view.SetLocalScaleMultiplier(scale);

            float alpha = 1f - shrink;
            view.SetAlpha(alpha);
        }

        public void OnUpdate(ObstacleView view, float dt)
        {
            // Subtle metallic shimmer
            _shimmerTimer += dt;
            float shimmer = 1f + Mathf.Sin(_shimmerTimer * 2f) * 0.008f;
            view.SetLocalScaleMultiplier(shimmer);
        }

        private static void ApplyStageColor(ObstacleView view, byte stage)
        {
            int idx = Mathf.Clamp(stage, 0, StageColors.Length - 1);
            view.SetBaseColor(StageColors[idx]);
        }
    }
}
