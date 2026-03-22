using Match3.Core.Models.Enums;
using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views.Obstacles
{
    /// <summary>
    /// Presenter for ColorBox obstacle: color-coded box, 3 stages.
    /// Color determined by state (ElementType), brightness varies by stage.
    /// Damaged by matching-color adjacent eliminations or power-up direct hits.
    /// </summary>
    public sealed class ColorBoxPresenter : IObstaclePresenter
    {
        private byte _currentStage;
        private Color _baseColor;

        public void Setup(ObstacleView view, byte stage, byte state)
        {
            _currentStage = stage;
            _baseColor = GetColorForState(state);

            var mesh = MeshFactory.GetObstacleMesh(ObstacleType.ColorBox);
            view.SetMesh(mesh);

            var mats = MeshFactory.GetObstacleMaterials(ObstacleType.ColorBox);
            if (mats != null)
                view.SetMaterials(mats);

            ApplyStageColor(view, stage);
        }

        public void ApplyDamage(ObstacleView view, float progress, byte newStage)
        {
            // Shake: oscillating scale (same as Box)
            float shake = Mathf.Sin(progress * Mathf.PI * 4f) * 0.08f * (1f - progress);
            view.SetLocalScaleMultiplier(1f + shake);

            if (_currentStage != newStage)
            {
                _currentStage = newStage;
                ApplyStageColor(view, newStage);
            }
        }

        public void ApplyDeath(ObstacleView view, float progress)
        {
            // Shrink + fade
            float scale = 1f - progress;
            view.SetLocalScaleMultiplier(scale);
            view.SetAlpha(1f - progress);
        }

        public void OnUpdate(ObstacleView view, float dt)
        {
            // No idle animation
        }

        private void ApplyStageColor(ObstacleView view, byte stage)
        {
            // Stage 3 = brightest, stage 1 = darkest
            float brightness = 0.4f + 0.2f * stage;
            var color = _baseColor * brightness;
            color.a = 1f; // Color * float also multiplies alpha — restore to opaque
            view.SetBaseColor(color);
        }

        private static Color GetColorForState(byte state) => (ElementType)state switch
        {
            ElementType.Item1 => new Color(0.90f, 0.15f, 0.10f),  // Red
            ElementType.Item2 => new Color(0.10f, 0.75f, 0.30f),  // Green
            ElementType.Item3 => new Color(0.10f, 0.40f, 0.90f),  // Blue
            ElementType.Item4 => new Color(0.95f, 0.75f, 0.10f),  // Yellow
            ElementType.Item5 => new Color(0.55f, 0.15f, 0.80f),  // Purple
            _ => Color.gray,
        };
    }
}
