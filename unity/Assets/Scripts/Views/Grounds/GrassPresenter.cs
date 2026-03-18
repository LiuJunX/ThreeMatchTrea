using Match3.Core.Models.Enums;
using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views.Grounds
{
    /// <summary>
    /// Presenter for Grass ground: uses imported model + health-based color tint.
    /// </summary>
    public sealed class GrassPresenter : IGroundPresenter
    {
        private byte _currentHealth;

        // Health colors: lighter as HP decreases
        private static readonly Color[] HealthColors =
        {
            Color.gray,                              // health 0 (unused)
            new Color(0.45f, 0.75f, 0.30f),          // health 1 — light green
            new Color(0.25f, 0.55f, 0.15f),          // health 2 — dark green
        };

        public void Setup(GroundView view, byte health)
        {
            _currentHealth = health;

            var mesh = MeshFactory.GetGroundMesh(GroundType.Grass);
            view.SetMesh(mesh);

            var mats = MeshFactory.GetGroundMaterials(GroundType.Grass);
            if (mats != null)
                view.SetMaterials(mats);

            ApplyHealthColor(view, health);
        }

        public void ApplyDamage(GroundView view, float progress, byte newHealth)
        {
            // Shake: oscillating scale
            float shake = Mathf.Sin(progress * Mathf.PI * 4f) * 0.08f * (1f - progress);
            view.SetLocalScaleMultiplier(1f + shake);

            // Swap color at the moment damage progress completes
            if (_currentHealth != newHealth && progress >= 1f)
            {
                _currentHealth = newHealth;
                ApplyHealthColor(view, newHealth);
            }
        }

        public void ApplyDeath(GroundView view, float progress)
        {
            // Shrink to zero
            float scale = 1f - progress;
            view.SetLocalScaleMultiplier(scale);

            // Fade via material alpha
            view.SetAlpha(1f - progress);
        }

        public void OnUpdate(GroundView view, float dt)
        {
            // Grass has no idle animation (per spec)
        }

        private static void ApplyHealthColor(GroundView view, byte health)
        {
            int idx = Mathf.Clamp(health, 0, HealthColors.Length - 1);
            view.SetBaseColor(HealthColors[idx]);
        }
    }
}
