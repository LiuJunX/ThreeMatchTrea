using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views.Obstacles
{
    /// <summary>
    /// Presenter for Box obstacle: multi-stage mesh swap + shake on damage + shrink on death.
    /// Falls back to a tinted cube when art meshes are not yet available.
    /// </summary>
    public sealed class BoxPresenter : IObstaclePresenter
    {
        private byte _currentStage;

        // Stage colors: darker as HP decreases (placeholder until real meshes)
        private static readonly Color[] StageColors =
        {
            Color.gray,                              // stage 0 (unused)
            new Color(0.55f, 0.35f, 0.15f),          // stage 1 — dark wood
            new Color(0.65f, 0.45f, 0.20f),          // stage 2
            new Color(0.75f, 0.55f, 0.25f),          // stage 3
            new Color(0.85f, 0.65f, 0.30f),          // stage 4 — light wood
        };

        public void Setup(ObstacleView view, byte stage)
        {
            _currentStage = stage;
            view.SetMesh(MeshFactory.GetFallbackMesh());
            ApplyStageColor(view, stage);
        }

        public void ApplyDamage(ObstacleView view, float progress, byte newStage)
        {
            // Shake: oscillating scale
            float shake = Mathf.Sin(progress * Mathf.PI * 4f) * 0.08f * (1f - progress);
            view.SetLocalScaleMultiplier(1f + shake);

            // Swap mesh/color at the moment damage progress completes
            if (_currentStage != newStage && progress >= 1f)
            {
                _currentStage = newStage;
                ApplyStageColor(view, newStage);
            }
        }

        public void ApplyDeath(ObstacleView view, float progress)
        {
            // Shrink to zero
            float scale = 1f - progress;
            view.SetLocalScaleMultiplier(scale);

            // Fade via material alpha
            float alpha = 1f - progress;
            view.SetAlpha(alpha);
        }

        public void OnUpdate(ObstacleView view, float dt)
        {
            // Box has no idle animation
        }

        private static void ApplyStageColor(ObstacleView view, byte stage)
        {
            int idx = Mathf.Clamp(stage, 0, StageColors.Length - 1);
            view.SetBaseColor(StageColors[idx]);
        }
    }
}
