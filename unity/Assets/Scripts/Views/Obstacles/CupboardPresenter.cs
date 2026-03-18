using Match3.Core.Models.Enums;
using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views.Obstacles
{
    /// <summary>
    /// Presenter for Cupboard obstacle: door-opening animation on death,
    /// wobble on damage. Uses imported model + color tint per stage.
    /// </summary>
    public sealed class CupboardPresenter : IObstaclePresenter
    {
        private byte _currentStage;

        // Stage colors: darker when closed, lighter when nearly open
        private static readonly Color[] StageColors =
        {
            Color.gray,                              // stage 0 (unused)
            new Color(0.50f, 0.30f, 0.20f),          // stage 1 — nearly open, warm brown
            new Color(0.65f, 0.40f, 0.25f),          // stage 2 — closed, lighter wood
        };

        public void Setup(ObstacleView view, byte stage)
        {
            _currentStage = stage;

            var mesh = MeshFactory.GetObstacleMesh(ObstacleType.Cupboard);
            view.SetMesh(mesh);

            var mats = MeshFactory.GetObstacleMaterials(ObstacleType.Cupboard);
            if (mats != null)
                view.SetMaterials(mats);

            ApplyStageColor(view, stage);
        }

        public void ApplyDamage(ObstacleView view, float progress, byte newStage)
        {
            // Wobble: door rattling effect (wider oscillation than Box shake)
            float wobble = Mathf.Sin(progress * Mathf.PI * 3f) * 0.10f * (1f - progress);
            view.SetLocalScaleMultiplier(1f + wobble);

            if (_currentStage != newStage && progress >= 1f)
            {
                _currentStage = newStage;
                ApplyStageColor(view, newStage);
            }
        }

        public void ApplyDeath(ObstacleView view, float progress)
        {
            // Door opens: scale X grows (door swings open), then shrink away
            float openPhase = Mathf.Clamp01(progress * 2f); // 0-0.5 -> 0-1
            float vanishPhase = Mathf.Clamp01(progress * 2f - 1f); // 0.5-1 -> 0-1

            float scale = 1f + openPhase * 0.15f; // slight grow as door opens
            scale *= 1f - vanishPhase; // then shrink to zero
            view.SetLocalScaleMultiplier(scale);

            float alpha = 1f - vanishPhase;
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
