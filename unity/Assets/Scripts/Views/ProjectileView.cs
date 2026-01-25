using Match3.Presentation;
using Match3.Unity.Bridge;
using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views
{
    /// <summary>
    /// Visual representation of a projectile (UFO, etc).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ProjectileView : MonoBehaviour, IPoolable
    {
        private SpriteRenderer _renderer;
        private TrailRenderer _trail;

        /// <summary>
        /// Unique projectile ID from the game state.
        /// </summary>
        public long ProjectileId { get; private set; }

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _renderer.sprite = SpriteFactory.GetColorSprite(new Color(0.5f, 1f, 0.5f));

            CreateTrail();
        }

        private void CreateTrail()
        {
            var trailGo = new GameObject("Trail");
            trailGo.transform.SetParent(transform, false);
            trailGo.transform.localPosition = Vector3.zero;

            _trail = trailGo.AddComponent<TrailRenderer>();
            _trail.startWidth = 0.3f;
            _trail.endWidth = 0f;
            _trail.time = 0.2f;
            _trail.material = new Material(Shader.Find("Sprites/Default"));
            _trail.startColor = new Color(0.5f, 1f, 0.5f, 0.8f);
            _trail.endColor = new Color(0.5f, 1f, 0.5f, 0f);
            _trail.sortingLayerName = "Projectiles";
            _trail.sortingOrder = 39;
        }

        /// <summary>
        /// Initialize the projectile with an ID.
        /// </summary>
        public void Setup(long id)
        {
            ProjectileId = id;
        }

        /// <summary>
        /// Update projectile from visual state.
        /// </summary>
        public void UpdateFromVisual(ProjectileVisual visual, float cellSize, Vector2 origin, int height)
        {
            // Update position (with Y-flip for Unity coordinate system)
            var worldPos = CoordinateConverter.GridToWorld(visual.Position, cellSize, origin, height);
            transform.position = worldPos;

            // Update rotation
            transform.rotation = Quaternion.Euler(0, 0, visual.Rotation);

            // Update visibility
            gameObject.SetActive(visual.IsVisible);
        }

        #region IPoolable

        public void OnSpawn()
        {
            ProjectileId = -1;
            transform.localScale = Vector3.one * 0.5f;
            _renderer.color = new Color(0.5f, 1f, 0.5f);
            _trail.Clear();
        }

        public void OnDespawn()
        {
            ProjectileId = -1;
            gameObject.SetActive(false);
            _trail.Clear();
        }

        #endregion
    }
}
