using Match3.Core.Models.Enums;
using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views.Grounds
{
    /// <summary>
    /// Presenter for Leaves ground: uses imported Leaves model + health-based color tint.
    /// Spawned by Flowerpot death effect.
    /// </summary>
    public sealed class LeavesPresenter : IGroundPresenter
    {
        private byte _currentHealth;

        // Health colors: fresh to withered
        private static readonly Color[] HealthColors =
        {
            Color.gray,                              // health 0 (unused)
            new Color(0.60f, 0.70f, 0.25f),          // health 1 — withered (yellow-green)
            new Color(0.35f, 0.60f, 0.15f),          // health 2 — fresh (green)
        };

        public void Setup(GroundView view, byte health)
        {
            _currentHealth = health;

            var mesh = MeshFactory.GetGroundMesh(GroundType.Leaves);
            view.SetMesh(mesh);

            var mats = MeshFactory.GetGroundMaterials(GroundType.Leaves);
            if (mats != null)
                view.SetMaterials(mats);

            ApplyHealthColor(view, health);
        }

        public void ApplyDamage(GroundView view, float progress, byte newHealth)
        {
            float shake = Mathf.Sin(progress * Mathf.PI * 4f) * 0.08f * (1f - progress);
            view.SetLocalScaleMultiplier(1f + shake);

            // Switch color on first frame of new health (no progress gate — matches BoxPresenter pattern)
            if (_currentHealth != newHealth)
            {
                _currentHealth = newHealth;
                ApplyHealthColor(view, newHealth);
            }
        }

        public void ApplyDeath(GroundView view, float progress)
        {
            float scale = 1f - progress;
            view.SetLocalScaleMultiplier(scale);
            view.SetAlpha(1f - progress);
        }

        public void OnUpdate(GroundView view, float dt)
        {
        }

        private static void ApplyHealthColor(GroundView view, byte health)
        {
            int idx = Mathf.Clamp(health, 0, HealthColors.Length - 1);
            view.SetBaseColor(HealthColors[idx]);
        }
    }
}
