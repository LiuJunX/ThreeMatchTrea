using Match3.Core.Events.Enums;
using Match3.Presentation;
using Match3.Unity.Bridge;
using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views
{
    /// <summary>
    /// 3D visual representation of a projectile (UFO missile or color bomb beam).
    /// Uses MeshFilter + MeshRenderer with trail effect.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class Projectile3DView : MonoBehaviour, IPoolable
    {
        private static Material _sharedTrailMaterial;
        private static Material _sharedBeamMaterial;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private TrailRenderer _trail;
        private ProjectileType _currentType;

        public int ProjectileId { get; private set; }

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
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
            _trail.minVertexDistance = 0.05f;
            _trail.sharedMaterial = _sharedTrailMaterial;
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
            _meshFilter.sharedMesh = MeshFactory.GetBombMesh(Core.Models.Enums.ElementType.Ufo);
            _meshRenderer.sharedMaterial = MeshFactory.GetFallbackMaterial();
            transform.localScale = Vector3.one * 0.4f;

            _trail.startWidth = 0.25f;
            _trail.endWidth = 0f;
            _trail.time = 0.2f;
            _trail.startColor = new Color(0.5f, 1f, 0.5f, 0.8f);
            _trail.endColor = new Color(0.5f, 1f, 0.5f, 0f);
        }

        private void ApplyBeamAppearance()
        {
            // Small bright sphere with rainbow trail
            _meshFilter.sharedMesh = MeshFactory.GetSphereMesh();
            _meshRenderer.sharedMaterial = GetOrCreateBeamMaterial();
            transform.localScale = Vector3.one * 0.15f;

            _trail.startWidth = 0.12f;
            _trail.endWidth = 0f;
            _trail.time = 0.15f;
            _trail.startColor = new Color(1f, 1f, 0.7f, 0.9f);
            _trail.endColor = new Color(1f, 0.8f, 0.3f, 0f);
        }

        public void UpdateFromVisual(ProjectileVisual visual, float cellSize, Vector2 origin, int height)
        {
            var worldPos = CoordinateConverter.GridToWorld(visual.Position, cellSize, origin, height);
            transform.position = new Vector3(worldPos.x, worldPos.y, 0f);
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
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Particles/Standard Unlit")
                         ?? Shader.Find("Sprites/Default");
            _sharedTrailMaterial = new Material(shader);
        }

        private static Material GetOrCreateBeamMaterial()
        {
            if (_sharedBeamMaterial != null) return _sharedBeamMaterial;
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Sprites/Default");
            _sharedBeamMaterial = new Material(shader);
            _sharedBeamMaterial.color = new Color(1f, 1f, 0.6f);
            return _sharedBeamMaterial;
        }
    }
}
