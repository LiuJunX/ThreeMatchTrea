using System.Collections.Generic;
using Match3.Unity.Pools;
using UnityEngine;
using UnityEngine.Rendering;

namespace Match3.Unity.Views
{
    /// <summary>
    /// Reusable outline effect for any MeshFilter+MeshRenderer object.
    /// Uses inverted-hull method: child object renders back-faces with vertex extrusion.
    /// Attach to any GameObject with MeshFilter to get toggleable outlines.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public sealed class OutlineEffect : MonoBehaviour
    {
        private static bool s_enabled;
        private static readonly List<OutlineEffect> s_instances = new();
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int ClipYMinId = Shader.PropertyToID("_ClipYMin");
        private static readonly int ClipYMaxId = Shader.PropertyToID("_ClipYMax");

        private MeshFilter _source;
        private GameObject _outlineGo;
        private MeshFilter _outlineFilter;
        private MeshRenderer _outlineRenderer;
        private MaterialPropertyBlock _propBlock;

        // Cached state to skip redundant updates
        private Mesh _syncedMesh;
        private Color _syncedColor;
        private Color _color = Color.white;
        private bool _forceVisible;

        // Clip bounds for hole portal effect
        private float _clipYMin = -9999f;
        private float _clipYMax = 9999f;
        private float _syncedClipYMin = -9999f;
        private float _syncedClipYMax = 9999f;

        /// <summary>
        /// Global on/off toggle for all outline instances.
        /// </summary>
        public static bool Enabled
        {
            get => s_enabled;
            set
            {
                if (s_enabled == value) return;
                s_enabled = value;
                for (int i = s_instances.Count - 1; i >= 0; i--)
                    s_instances[i].RefreshVisibility();
            }
        }

        /// <summary>
        /// Global outline width. Modifies the shared material directly.
        /// </summary>
        public static float Width
        {
            get
            {
                var mat = MeshFactory.GetOutlineMaterial();
                return mat != null && mat.HasProperty(OutlineWidthId)
                    ? mat.GetFloat(OutlineWidthId)
                    : 0.025f;
            }
            set
            {
                var mat = MeshFactory.GetOutlineMaterial();
                if (mat != null)
                    mat.SetFloat(OutlineWidthId, value);
            }
        }

        /// <summary>
        /// Per-instance outline color. Only applied when changed (cached).
        /// </summary>
        public Color OutlineColor
        {
            get => _color;
            set => _color = value;
        }

        private void OnEnable() => s_instances.Add(this);
        private void OnDisable() => s_instances.Remove(this);

        private void Awake()
        {
            _source = GetComponent<MeshFilter>();

            _outlineGo = new GameObject("Outline");
            _outlineGo.transform.SetParent(transform, false);

            _outlineFilter = _outlineGo.AddComponent<MeshFilter>();
            _outlineRenderer = _outlineGo.AddComponent<MeshRenderer>();
            var mat = MeshFactory.GetOutlineMaterial();
            if (mat != null)
                _outlineRenderer.sharedMaterial = mat;
            _outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _outlineRenderer.receiveShadows = false;
            _propBlock = new MaterialPropertyBlock();

            _outlineGo.SetActive(s_enabled);
            enabled = s_enabled;
        }

        /// <summary>
        /// Force this instance visible regardless of global toggle (for hint outlines etc.).
        /// </summary>
        public void SetForceVisible(bool visible)
        {
            if (_forceVisible == visible) return;
            _forceVisible = visible;
            RefreshVisibility();
        }

        /// <summary>
        /// Set Y-axis clip bounds for hole portal effect.
        /// </summary>
        public void SetClipBounds(float yMin, float yMax)
        {
            _clipYMin = yMin;
            _clipYMax = yMax;
        }

        /// <summary>
        /// Reset clip bounds to default (no clipping).
        /// </summary>
        public void ResetClipBounds()
        {
            _clipYMin = -9999f;
            _clipYMax = 9999f;
        }

        private void LateUpdate()
        {
            if (!s_enabled && !_forceVisible) return;
            if (_outlineFilter == null) return;

            // Sync mesh from source (only when changed)
            var mesh = _source.sharedMesh;
            if (mesh != _syncedMesh)
            {
                _outlineFilter.sharedMesh = mesh;
                _syncedMesh = mesh;
            }

            // Sync properties (color + clip bounds) — single SetPropertyBlock call
            bool colorChanged = _color != _syncedColor;
            bool clipChanged = _clipYMin != _syncedClipYMin || _clipYMax != _syncedClipYMax;

            if (colorChanged || clipChanged)
            {
                _outlineRenderer.GetPropertyBlock(_propBlock);
                if (colorChanged)
                {
                    _propBlock.SetColor(OutlineColorId, _color);
                    _syncedColor = _color;
                }
                if (clipChanged)
                {
                    _propBlock.SetFloat(ClipYMinId, _clipYMin);
                    _propBlock.SetFloat(ClipYMaxId, _clipYMax);
                    _syncedClipYMin = _clipYMin;
                    _syncedClipYMax = _clipYMax;
                }
                _outlineRenderer.SetPropertyBlock(_propBlock);
            }
        }

        private void RefreshVisibility()
        {
            bool visible = s_enabled || _forceVisible;
            if (_outlineGo != null)
                _outlineGo.SetActive(visible);
            // Disable LateUpdate when not needed to avoid 64 empty managed callbacks per frame
            enabled = visible;
        }

        /// <summary>
        /// Reset cached state. Call on pool spawn to ensure
        /// mesh and color are re-synced on next LateUpdate.
        /// </summary>
        public void ResetState()
        {
            _syncedMesh = null;
            _syncedColor = default;
            _color = Color.white;
            _forceVisible = false;
            _clipYMin = -9999f;
            _clipYMax = 9999f;
            _syncedClipYMin = -9999f;
            _syncedClipYMax = 9999f;
            RefreshVisibility();
        }
    }
}
