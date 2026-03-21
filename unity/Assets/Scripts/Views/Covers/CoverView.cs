using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Presentation;
using Match3.Unity.Bridge;
using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views.Covers
{
    /// <summary>
    /// MonoBehaviour view for a single cover element. Manages mesh/material rendering
    /// and delegates type-specific behavior to <see cref="ICoverPresenter"/>.
    /// Pooled by Board3DView via <see cref="ObjectPool{T}"/>.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class CoverView : MonoBehaviour, IPoolable
    {
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _propBlock;
        private ICoverPresenter _presenter;
        private float _baseScale = 1f;

        // Shader property IDs
        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProp = Shader.PropertyToID("_Color");

        /// <summary>Grid position this view is bound to.</summary>
        public Position GridPosition { get; private set; }

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _propBlock = new MaterialPropertyBlock();
        }

        /// <summary>
        /// Initialize this view for a specific cover element.
        /// </summary>
        public void Setup(Position gridPos, CoverType type, byte health)
        {
            GridPosition = gridPos;
            _baseScale = 1f;

            if (_meshRenderer.sharedMaterial == null)
                _meshRenderer.sharedMaterial = MeshFactory.GetFallbackMaterial();

            _presenter = CoverPresenterFactory.Create(type);
            _presenter.Setup(this, health);
        }

        /// <summary>
        /// Update from visual state each frame.
        /// </summary>
        public void UpdateFromVisual(CoverVisual visual, float cellSize,
            Vector2 origin, int height, float dt)
        {
            // Position: static, convert grid -> world. Cover sits above tiles.
            var worldPos = CoordinateConverter.GridToWorld(visual.GridPosition, cellSize, origin, height);
            // Pull cover in front of tiles (negative Z = closer to camera)
            transform.localPosition = new Vector3(worldPos.x, worldPos.y, worldPos.z - 0.1f);

            // Scale to fill cell
            _baseScale = cellSize * 1.05f;
            transform.localScale = new Vector3(_baseScale, _baseScale, _baseScale);

            // Delegate to presenter based on state
            if (visual.IsDestroying)
            {
                _presenter.ApplyDeath(this, visual.DestroyProgress);
            }
            else
            {
                _presenter.OnUpdate(this, dt);
            }

            gameObject.SetActive(visual.IsVisible);
        }

        #region Helper methods for presenters

        /// <summary>Set the mesh displayed by this view.</summary>
        public void SetMesh(Mesh mesh)
        {
            if (_meshFilter.sharedMesh != mesh)
                _meshFilter.sharedMesh = mesh;
        }

        /// <summary>Set shared materials from FBX model.</summary>
        public void SetMaterials(Material[] materials)
        {
            _meshRenderer.sharedMaterials = materials;
        }

        /// <summary>
        /// Multiply current scale by a factor (used for shrink animations).
        /// </summary>
        public void SetLocalScaleMultiplier(float multiplier)
        {
            float s = _baseScale * multiplier;
            transform.localScale = new Vector3(s, s, s);
        }

        /// <summary>Set the base color via MaterialPropertyBlock.</summary>
        public void SetBaseColor(Color color)
        {
            _meshRenderer.GetPropertyBlock(_propBlock);
            if (_meshRenderer.sharedMaterial.HasProperty(BaseColorProp))
                _propBlock.SetColor(BaseColorProp, color);
            else
                _propBlock.SetColor(ColorProp, color);
            _meshRenderer.SetPropertyBlock(_propBlock);
        }

        /// <summary>Set alpha via MaterialPropertyBlock (modifies existing base color).</summary>
        public void SetAlpha(float alpha)
        {
            _meshRenderer.GetPropertyBlock(_propBlock);
            var propId = _meshRenderer.sharedMaterial.HasProperty(BaseColorProp)
                ? BaseColorProp : ColorProp;
            var color = _propBlock.GetColor(propId);
            color.a = alpha;
            _propBlock.SetColor(propId, color);
            _meshRenderer.SetPropertyBlock(_propBlock);
        }

        #endregion

        #region IPoolable

        public void OnSpawn()
        {
            _presenter = null;
            GridPosition = Position.Invalid;
            _baseScale = 1f;
            _meshRenderer.SetPropertyBlock(null);
        }

        public void OnDespawn()
        {
            _presenter = null;
            GridPosition = Position.Invalid;
            gameObject.SetActive(false);
        }

        #endregion
    }
}
