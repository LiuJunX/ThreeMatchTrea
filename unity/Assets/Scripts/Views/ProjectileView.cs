using Match3.Core.Events.Enums;
using Match3.Presentation;
using Match3.Unity.Bridge;
using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views
{
    /// <summary>
    /// 2D visual representation of a projectile (UFO or color bomb beam).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ProjectileView : MonoBehaviour, IPoolable
    {
        private static Material _sharedTrailMaterial;

        private SpriteRenderer _renderer;
        private TrailRenderer _trail;
        private ProjectileType _currentType;

        public int ProjectileId { get; private set; }

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            EnsureTrailMaterial();
            CreateTrail();
            ApplyUfoAppearance();
        }

        private void CreateTrail()
        {
            var trailGo = new GameObject("Trail");
            trailGo.transform.SetParent(transform, false);
            trailGo.transform.localPosition = Vector3.zero;

            _trail = trailGo.AddComponent<TrailRenderer>();
            _trail.sharedMaterial = _sharedTrailMaterial;
            _trail.sortingLayerName = "Projectiles";
            _trail.sortingOrder = 39;
        }

        public void Setup(int id, ProjectileType type)
        {
            ProjectileId = id;
            if (_currentType != type)
            {
                _currentType = type;
                if (type == ProjectileType.ColorBombBeam)
                    ApplyBeamAppearance();
                else
                    ApplyUfoAppearance();
            }
        }

        private void ApplyUfoAppearance()
        {
            _currentType = ProjectileType.Ufo;
            _renderer.sprite = SpriteFactory.GetColorSprite(new Color(0.5f, 1f, 0.5f));
            _renderer.color = new Color(0.5f, 1f, 0.5f);
            transform.localScale = Vector3.one * 0.5f;

            _trail.startWidth = 0.3f;
            _trail.endWidth = 0f;
            _trail.time = 0.2f;
            _trail.startColor = new Color(0.5f, 1f, 0.5f, 0.8f);
            _trail.endColor = new Color(0.5f, 1f, 0.5f, 0f);
        }

        private void ApplyBeamAppearance()
        {
            _renderer.sprite = SpriteFactory.GetColorSprite(new Color(1f, 1f, 0.6f));
            _renderer.color = new Color(1f, 1f, 0.6f);
            transform.localScale = Vector3.one * 0.2f;

            _trail.startWidth = 0.15f;
            _trail.endWidth = 0f;
            _trail.time = 0.15f;
            _trail.startColor = new Color(1f, 1f, 0.7f, 0.9f);
            _trail.endColor = new Color(1f, 0.8f, 0.3f, 0f);
        }

        public void UpdateFromVisual(ProjectileVisual visual, float cellSize, Vector2 origin, int height)
        {
            var worldPos = CoordinateConverter.GridToWorld(visual.Position, cellSize, origin, height);
            transform.position = worldPos;
            transform.rotation = Quaternion.Euler(0, 0, visual.Rotation);
            gameObject.SetActive(visual.IsVisible);
        }

        #region IPoolable

        public void OnSpawn()
        {
            ProjectileId = -1;
            _trail.Clear();
        }

        public void OnDespawn()
        {
            ProjectileId = -1;
            gameObject.SetActive(false);
            _trail.Clear();
        }

        #endregion

        private static void EnsureTrailMaterial()
        {
            if (_sharedTrailMaterial != null) return;
            _sharedTrailMaterial = new Material(Shader.Find("Sprites/Default"));
        }
    }
}
