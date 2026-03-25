using Match3.Core.Models.Enums;
using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views.Grounds
{
    /// <summary>
    /// Presenter for Ice ground: uses imported Ice model + health-based color tint.
    /// </summary>
    public sealed class IcePresenter : IGroundPresenter
    {
        private byte _currentHealth;

        // Health colors: progressively cracked look (lighter/more transparent)
        private static readonly Color[] HealthColors =
        {
            Color.gray,                              // health 0 (unused)
            new Color(0.70f, 0.85f, 0.95f),          // health 1 — thin ice (pale blue)
            new Color(0.50f, 0.75f, 0.90f),          // health 2 — solid ice (blue)
        };

        public void Setup(GroundView view, byte health)
        {
            _currentHealth = health;

            var mesh = MeshFactory.GetGroundMesh(GroundType.Ice);
            view.SetMesh(mesh);

            var mats = MeshFactory.GetGroundMaterials(GroundType.Ice);
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
