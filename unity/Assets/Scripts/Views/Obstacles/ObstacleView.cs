using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Presentation;
using Match3.Unity.Bridge;
using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views.Obstacles
{
    /// <summary>
    /// MonoBehaviour view for a single obstacle. Manages mesh/material rendering
    /// and delegates type-specific behavior to <see cref="IObstaclePresenter"/>.
    /// Pooled by Board3DView via <see cref="ObjectPool{T}"/>.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ObstacleView : MonoBehaviour, IPoolable
    {
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _propBlock;
        private IObstaclePresenter _presenter;
        private float _baseScale = 1f;

        private static readonly Quaternion BaseTiltQuat = Quaternion.Euler(-10f, 0f, 0f);

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
        /// Initialize this view for a specific obstacle.
        /// </summary>
        public void Setup(Position gridPos, ObstacleType type, byte stage, byte state = 0)
        {
            GridPosition = gridPos;
            _baseScale = 1f;

            // Assign initial material (will be modified via PropertyBlock)
            if (_meshRenderer.sharedMaterial == null)
                _meshRenderer.sharedMaterial = MeshFactory.GetFallbackMaterial();

            _presenter = ObstaclePresenterFactory.Create(type);
            _presenter.Setup(this, stage, state);
        }

        /// <summary>
        /// Update from visual state each frame.
        /// </summary>
        public void UpdateFromVisual(ObstacleVisual visual, float cellSize,
            Vector2 origin, int height, float dt)
        {
            // Position: static, convert grid → world
            var worldPos = CoordinateConverter.GridToWorld(visual.GridPosition, cellSize, origin, height);
            transform.localPosition = worldPos;
            transform.localRotation = BaseTiltQuat;

            // Base scale to fill cell (match tile scale)
            _baseScale = cellSize * 1.05f;
            transform.localScale = new Vector3(_baseScale, _baseScale, _baseScale);

            // Delegate to presenter based on state
            if (visual.IsDestroying)
            {
                _presenter.ApplyDeath(this, visual.DeathProgress);
            }
            else if (visual.DamageProgress > 0f && visual.DamageProgress < 1f)
            {
                _presenter.ApplyDamage(this, visual.DamageProgress, visual.CurrentStage);
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
        /// Multiply current scale by a factor (used for shake/shrink animations).
        /// </summary>
        public void SetLocalScaleMultiplier(float multiplier)
        {
            float s = _baseScale * multiplier;
            transform.localScale = new Vector3(s, s, s);
        }

        /// <summary>
        /// Set non-uniform local scale multipliers (for pull/stretch animations).
        /// </summary>
        public void SetLocalScale(float scaleX, float scaleY, float scaleZ)
        {
            transform.localScale = new Vector3(
                _baseScale * scaleX, _baseScale * scaleY, _baseScale * scaleZ);
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
