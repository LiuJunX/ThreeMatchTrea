using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views.Grounds
{
    /// <summary>
    /// Presenter for Grass ground: health-based color, shake on damage, shrink+fade on death.
    /// Falls back to a tinted cube when art meshes are not yet available.
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
            view.SetMesh(MeshFactory.GetFallbackMesh());
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
