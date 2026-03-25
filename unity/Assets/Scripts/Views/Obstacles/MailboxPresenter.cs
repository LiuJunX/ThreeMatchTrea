using Match3.Core.Models.Enums;
using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views.Obstacles
{
    /// <summary>
    /// Presenter for Mailbox obstacle: permanent generator, indestructible.
    /// Loads FBX model, no damage/death animations needed.
    /// </summary>
    public sealed class MailboxPresenter : IObstaclePresenter
    {
        public void Setup(ObstacleView view, byte stage, byte state)
        {
            var mesh = MeshFactory.GetObstacleMesh(ObstacleType.Mailbox);
            view.SetMesh(mesh);

            var mats = MeshFactory.GetObstacleMaterials(ObstacleType.Mailbox);
            if (mats != null)
                view.SetMaterials(mats);
        }

        public void ApplyDamage(ObstacleView view, float progress, byte newStage)
        {
            // Mailbox is indestructible — react with subtle shake on hit
            float shake = Mathf.Sin(progress * Mathf.PI * 4f) * 0.04f * (1f - progress);
            view.SetLocalScaleMultiplier(1f + shake);
        }

        public void ApplyDeath(ObstacleView view, float progress)
        {
            // Should not be called — Mailbox is indestructible
        }

        public void OnUpdate(ObstacleView view, float dt)
        {
            // No idle animation
        }
    }
}
