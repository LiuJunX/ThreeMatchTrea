using Match3.Core.Models.Enums;
using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views.Obstacles
{
    /// <summary>
    /// Presenter for Bush obstacle: 5-stage visual progression (dense → sparse → dead).
    /// Subtle sway idle animation. Leaf-scatter shake on damage, shrink+fade on death.
    /// </summary>
    public sealed class BushPresenter : IObstaclePresenter
    {
        private byte _currentStage;
        private float _swayTimer;

        // Stage colors: dense green (5) → sparse pale (1)
        private static readonly Color[] StageColors =
        {
            Color.gray,                              // stage 0 (unused)
            new Color(0.70f, 0.65f, 0.20f),          // stage 1 — withered yellow-green
            new Color(0.55f, 0.70f, 0.25f),          // stage 2 — pale green
            new Color(0.35f, 0.65f, 0.20f),          // stage 3 — medium green
            new Color(0.20f, 0.55f, 0.15f),          // stage 4 — dark green
            new Color(0.10f, 0.45f, 0.10f),          // stage 5 — deep forest green
        };

        public void Setup(ObstacleView view, byte stage, byte state)
        {
            _currentStage = stage;
            _swayTimer = 0f;

            var mesh = MeshFactory.GetObstacleMesh(ObstacleType.Bush);
            view.SetMesh(mesh);

            var mats = MeshFactory.GetObstacleMaterials(ObstacleType.Bush);
            if (mats != null)
                view.SetMaterials(mats);

            ApplyStageColor(view, stage);
        }

        public void ApplyDamage(ObstacleView view, float progress, byte newStage)
        {
            // Shake: rapid oscillation simulating leaf scatter
            float shake = Mathf.Sin(progress * Mathf.PI * 6f) * 0.10f * (1f - progress);
            view.SetLocalScaleMultiplier(1f + shake);

            // Switch color on first frame of new stage
            if (_currentStage != newStage)
            {
                _currentStage = newStage;
                ApplyStageColor(view, newStage);
            }
        }

        public void ApplyDeath(ObstacleView view, float progress)
        {
            // Shrink to zero + fade out
            float scale = 1f - progress;
            view.SetLocalScaleMultiplier(scale);

            float alpha = 1f - progress;
            view.SetAlpha(alpha);
        }

        public void OnUpdate(ObstacleView view, float dt)
        {
            // Gentle sway: subtle scale oscillation
            _swayTimer += dt;
            float sway = 1f + Mathf.Sin(_swayTimer * 1.5f) * 0.01f;
            view.SetLocalScaleMultiplier(sway);
        }

        private static void ApplyStageColor(ObstacleView view, byte stage)
        {
            int idx = Mathf.Clamp(stage, 0, StageColors.Length - 1);
            view.SetBaseColor(StageColors[idx]);
        }
    }
}
