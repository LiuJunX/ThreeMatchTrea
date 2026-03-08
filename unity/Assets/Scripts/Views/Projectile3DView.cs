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
        private static Material[] _beamMaterials;

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

        public void Setup(int id, ProjectileType type, byte colorIndex = 0)
        {
            ProjectileId = id;
            _currentType = type;
            if (type == ProjectileType.ColorBombBeam)
                ApplyBeamAppearance(colorIndex);
            else
                ApplyUfoAppearance();
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

        private void ApplyBeamAppearance(byte colorIndex)
        {
            var color = GetBeamColor(colorIndex);
            _meshFilter.sharedMesh = MeshFactory.GetSphereMesh();
            _meshRenderer.sharedMaterial = GetOrCreateBeamMaterial(colorIndex);
            transform.localScale = Vector3.one * 0.25f;

            _trail.startWidth = 0.3f;
            _trail.endWidth = 0.04f;
            _trail.time = 0.5f;
            _trail.startColor = new Color(color.r, color.g, color.b, 1f);
            _trail.endColor = new Color(color.r, color.g, color.b, 0f);
        }

        public void UpdateFromVisual(ProjectileVisual visual, float cellSize, Vector2 origin, int height)
        {
            var worldPos = CoordinateConverter.GridToWorld(visual.Position, cellSize, origin, height);
            transform.position = new Vector3(worldPos.x, worldPos.y, -0.5f);
            transform.rotation = Quaternion.Euler(0, 0, visual.Rotation);
            gameObject.SetActive(visual.IsVisible);

            // Re-enable trail emission after first position update (prevents flash)
            if (!_trail.emitting)
            {
                _trail.Clear();
                _trail.emitting = true;
            }
        }

        #region IPoolable

        public void OnSpawn()
        {
            ProjectileId = -1;
            // Suppress trail until first UpdateFromVisual sets correct position
            _trail.emitting = false;
            _trail.Clear();
        }

        public void OnDespawn()
        {
            ProjectileId = -1;
            gameObject.SetActive(false);
            _trail.emitting = false;
            _trail.Clear();
        }

        #endregion

        private static readonly Color[] BeamColors =
        {
            new Color(1f, 0.3f, 0.3f),    // Red
            new Color(0.3f, 1f, 0.4f),     // Green
            new Color(0.3f, 0.5f, 1f),     // Blue
            new Color(1f, 0.95f, 0.3f),    // Yellow
            new Color(0.8f, 0.4f, 1f),     // Purple
            new Color(1f, 0.6f, 0.2f),     // Orange
        };

        private static Color GetBeamColor(byte index)
        {
            return index < BeamColors.Length ? BeamColors[index] : BeamColors[0];
        }

        private static void EnsureTrailMaterial()
        {
            if (_sharedTrailMaterial != null) return;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Particles/Standard Unlit")
                         ?? Shader.Find("Sprites/Default");
            _sharedTrailMaterial = new Material(shader);
        }

        private static Material GetOrCreateBeamMaterial(byte colorIndex)
        {
            if (_beamMaterials == null)
                _beamMaterials = new Material[BeamColors.Length];

            int idx = colorIndex < _beamMaterials.Length ? colorIndex : 0;
            if (_beamMaterials[idx] != null) return _beamMaterials[idx];

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Sprites/Default");
            _beamMaterials[idx] = new Material(shader);
            _beamMaterials[idx].color = GetBeamColor((byte)idx);
            return _beamMaterials[idx];
        }
    }
}
