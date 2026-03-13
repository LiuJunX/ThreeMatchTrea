using UnityEngine;

namespace Match3.Unity.Views
{
    /// <summary>
    /// Shared lifecycle utilities for projectile views (2D and 3D).
    /// Eliminates duplicated spawn/despawn/trail logic.
    /// </summary>
    public static class ProjectileLifecycleHelper
    {
        /// <summary>
        /// Resets trail state on spawn — suppresses emission until the first
        /// position update to prevent a flash at the origin.
        /// </summary>
        public static void OnSpawn(TrailRenderer trail)
        {
            trail.emitting = false;
            trail.Clear();
        }

        /// <summary>
        /// Cleans up trail and deactivates the GameObject on despawn.
        /// </summary>
        public static void OnDespawn(GameObject go, TrailRenderer trail)
        {
            go.SetActive(false);
            trail.emitting = false;
            trail.Clear();
        }

        /// <summary>
        /// Re-enables trail emission after the first position update,
        /// clearing stale vertices to avoid a visual flash.
        /// </summary>
        public static void UpdateTrailEmission(TrailRenderer trail)
        {
            if (!trail.emitting)
            {
                trail.Clear();
                trail.emitting = true;
            }
        }
    }
}
