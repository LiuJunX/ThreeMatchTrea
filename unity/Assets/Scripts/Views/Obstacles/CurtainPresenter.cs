using Match3.Core.Models.Enums;
using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views.Obstacles
{
    /// <summary>
    /// Presenter for Curtain obstacle: color-coded curtain, 1 HP (default).
    /// Color determined by state (ElementType). Damaged only by global color elimination.
    /// Death animation: fabric "pull-open" effect (horizontal stretch + vertical compress + fade).
    /// </summary>
    public sealed class CurtainPresenter : IObstaclePresenter
    {
        private float _idleTimer;

        public void Setup(ObstacleView view, byte stage, byte state)
        {
            _idleTimer = 0f;

            var mesh = MeshFactory.GetObstacleMesh(ObstacleType.Curtain);
            view.SetMesh(mesh);

            var mats = MeshFactory.GetObstacleMaterials(ObstacleType.Curtain);
            if (mats != null)
                view.SetMaterials(mats);

            view.SetBaseColor(GetColorForState(state));
        }

        public void ApplyDamage(ObstacleView view, float progress, byte newStage)
        {
            // Stage=1 default won't trigger this, but safe for multi-HP variants
            float shake = Mathf.Sin(progress * Mathf.PI * 4f) * 0.06f * (1f - progress);
            view.SetLocalScaleMultiplier(1f + shake);
        }

        public void ApplyDeath(ObstacleView view, float progress)
        {
            // Curtain "pull-open": horizontal stretch + vertical compress + fade
            float scaleX = 1f + progress * 0.8f;
            float scaleY = 1f - progress * 0.6f;
            view.SetLocalScale(scaleX, scaleY, 1f);
            view.SetAlpha(1f - progress);
        }

        public void OnUpdate(ObstacleView view, float dt)
        {
            // Subtle fabric sway on Y axis
            _idleTimer += dt;
            float sway = Mathf.Sin(_idleTimer * 0.8f) * 0.005f;
            view.SetLocalScaleMultiplier(1f + sway);
        }

        private static Color GetColorForState(byte state) => (ElementType)state switch
        {
            ElementType.Item1 => new Color(0.85f, 0.15f, 0.12f), // Red   — deep velvet
            ElementType.Item2 => new Color(0.10f, 0.65f, 0.28f), // Green — emerald
            ElementType.Item3 => new Color(0.12f, 0.35f, 0.82f), // Blue  — royal blue
            ElementType.Item4 => new Color(0.90f, 0.70f, 0.08f), // Yellow — gold
            ElementType.Item5 => new Color(0.50f, 0.12f, 0.75f), // Purple — violet
            _ => Color.gray,
        };
    }
}
