using Match3.Core.Models.Enums;
using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views.Obstacles
{
    /// <summary>
    /// Presenter for MagicHat generator: permanent, accumulates 3 adjacent hits to spawn Diamond.
    /// ApplyDamage is reinterpreted as accumulation progress (newStage = accumulation count 1/2/3).
    /// </summary>
    public sealed class MagicHatPresenter : IObstaclePresenter
    {
        private float _idleTimer;

        public void Setup(ObstacleView view, byte stage, byte state)
        {
            _idleTimer = 0f;

            var mesh = MeshFactory.GetObstacleMesh(ObstacleType.MagicHat);
            view.SetMesh(mesh);

            var mats = MeshFactory.GetObstacleMaterials(ObstacleType.MagicHat);
            if (mats != null)
                view.SetMaterials(mats);
        }

        public void ApplyDamage(ObstacleView view, float progress, byte newStage)
        {
            // newStage carries accumulation count (1/2/3), not HP
            float intensity = newStage / 3f;
            float shake = Mathf.Sin(progress * Mathf.PI * 4f) * 0.08f * intensity * (1f - progress);
            view.SetLocalScaleMultiplier(1f + shake);
        }

        public void ApplyDeath(ObstacleView view, float progress)
        {
            // MagicHat is never destroyed — no-op
        }

        public void OnUpdate(ObstacleView view, float dt)
        {
            // Subtle breathing effect hinting at magical energy
            _idleTimer += dt;
            float breath = 1f + Mathf.Sin(_idleTimer * 1.5f) * 0.01f;
            view.SetLocalScaleMultiplier(breath);
        }
    }
}
