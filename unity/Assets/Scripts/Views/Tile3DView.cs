using Match3.Core.Models.Enums;
using Match3.Presentation;
using Match3.Unity.Bridge;
using Match3.Unity.Pools;
using UnityEngine;
using UnityEngine.Rendering;

namespace Match3.Unity.Views
{
    /// <summary>
    /// 3D visual representation of a single tile.
    /// Uses MeshFilter + MeshRenderer instead of SpriteRenderer.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class Tile3DView : MonoBehaviour, IPoolable
    {
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _propBlock;
        private MeshRenderer _shadowRenderer;
        private MaterialPropertyBlock _shadowPropBlock;
        private Transform _shadowTransform;

        // Cached shadow state so blob tweaks can refresh instantly
        private bool _hasLastShadowState;
        private Vector3 _lastShadowWorldPos;
        private float _lastShadowCellSize;
        private Vector3 _lastShadowTileScale;
        private float _lastShadowTileZ;

        public int TileId { get; private set; }
        public TileType TileType { get; private set; }
        public BombType BombType { get; private set; }

        private Vector3 _baseScale = Vector3.one;
        private bool _isHighlighted;
        private bool _wasAnimated;
        private float _bounceTime = -1f;
        private float _highlightTime;

        private static readonly int ColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropFallback = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");
        private static readonly int ShadowColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int ShadowColorPropFallback = Shader.PropertyToID("_Color");

        private const float BounceEndTime = 0.15f;

        // Scale multiplier: makes tiles fill more of the cell
        private const float TileScaleMultiplier = 1.05f;

        // Blob shadow constants
        private const float BlobShadowZ = 0.08f;

        // Runtime-tweakable blob shadow params (defaults tuned for ceramic look).
        private static float s_blobShadowSize = 0.54f;
        private static float s_blobShadowBaseAlpha = 0.11f;
        private static float s_blobShadowOffset = 0.025f;
        private static float s_blobShadowLiftAlphaReduce = 0.55f;
        private static float s_blobShadowLiftSizeIncrease = 0.18f;

        public static void ApplyRenderTuning(RenderTuningSettings settings)
        {
            if (settings == null) return;
            s_blobShadowSize = settings.BlobSize;
            s_blobShadowBaseAlpha = settings.BlobBaseAlpha;
            s_blobShadowOffset = settings.BlobOffset;
            s_blobShadowLiftAlphaReduce = settings.BlobLiftAlphaReduce;
            s_blobShadowLiftSizeIncrease = settings.BlobLiftSizeIncrease;

            // Apply immediately to existing tiles (so blob shadow tweaks feel "live").
            RefreshAllBlobShadows();
        }

        private static void RefreshAllBlobShadows()
        {
            var tiles = Object.FindObjectsOfType<Tile3DView>();
            foreach (var t in tiles)
                t.RefreshBlobShadowNow();
        }

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _propBlock = new MaterialPropertyBlock();
            CreateBlobShadow();
        }

        private void CreateBlobShadow()
        {
            var shadowGo = new GameObject("BlobShadow");
            _shadowTransform = shadowGo.transform;
            _shadowTransform.SetParent(transform, false);

            shadowGo.AddComponent<MeshFilter>().sharedMesh = MeshFactory.GetBlobShadowMesh();
            _shadowRenderer = shadowGo.AddComponent<MeshRenderer>();
            _shadowRenderer.sharedMaterial = MeshFactory.GetBlobShadowMaterial();
            _shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _shadowRenderer.receiveShadows = false;
            _shadowPropBlock = new MaterialPropertyBlock();
        }

        /// <summary>
        /// Initialize the tile with ID, type, and bomb.
        /// </summary>
        public void Setup(int id, TileType type, BombType bomb)
        {
            TileId = id;
            TileType = type;
            BombType = bomb;

            // Use bomb mesh when applicable, otherwise per-type tile mesh
            _meshFilter.sharedMesh = bomb != BombType.None
                ? MeshFactory.GetBombMesh(bomb)
                : MeshFactory.GetTileMesh(type);

            // Bombs use their own multi-material design from the FBX model
            ApplyMaterial(type, bomb);

            // 棋子在棋盘上投射并接收阴影
            _meshRenderer.shadowCastingMode = ShadowCastingMode.On;
            _meshRenderer.receiveShadows = true;
        }

        private void ApplyMaterial(TileType type, BombType bomb)
        {
            // Bombs use their own multi-material design from the FBX model
            var bombMats = bomb != BombType.None ? MeshFactory.GetBombMaterials(bomb) : null;
            if (bombMats != null && bombMats.Length > 0)
            {
                _meshRenderer.sharedMaterials = bombMats;
                return;
            }

            // 6 color types get their own material; others get fallback
            var isColorType = (type & (TileType.Red | TileType.Green | TileType.Blue |
                                       TileType.Yellow | TileType.Purple | TileType.Orange)) != 0;
            _meshRenderer.sharedMaterials = new[]
            {
                isColorType ? MeshFactory.GetTileMaterial(type) : MeshFactory.GetFallbackMaterial()
            };
        }

        /// <summary>
        /// Update tile from visual state.
        /// </summary>
        public void UpdateFromVisual(TileVisual visual, float cellSize, Vector2 origin, int height)
        {
            var isAnimated = visual.IsBeingAnimated;

            // Detect landing: was animated, now idle
            if (_wasAnimated && !isAnimated)
            {
                _bounceTime = 0f;
            }
            _wasAnimated = isAnimated;

            // Position (with Y-flip)
            var worldPos = CoordinateConverter.GridToWorld(visual.Position, cellSize, origin, height);
            var pos = new Vector3(worldPos.x, worldPos.y, 0f);
            // NOTE: Idle breathing intentionally disabled (keep tiles perfectly still when idle).

            if (_isHighlighted)
            {
                _highlightTime = (_highlightTime + Time.deltaTime) % 628f; // wrap to avoid float precision loss
                // 选中呼吸：相对棋盘的上下（沿棋盘法线 Z 轴轻微浮动）
                var floatZ = Mathf.Sin(_highlightTime * 5f) * 0.04f * cellSize;
                pos.z += floatZ;
            }
            transform.position = pos;

            // Scale (uniform 3D, Z tracks min of X/Y for natural shrink)
            var scaleFactor = cellSize * TileScaleMultiplier;
            var zScale = Mathf.Min(visual.Scale.X, visual.Scale.Y);
            _baseScale = new Vector3(
                visual.Scale.X * scaleFactor,
                visual.Scale.Y * scaleFactor,
                zScale * scaleFactor);

            var finalScale = _baseScale;

            // Landing bounce
            if (_bounceTime >= 0f && _bounceTime < BounceEndTime)
            {
                _bounceTime += Time.deltaTime;
                var t = _bounceTime / BounceEndTime;
                var squash = Mathf.Sin(t * Mathf.PI) * 0.1f;
                finalScale.x *= 1f + squash;
                finalScale.z *= 1f + squash;
                finalScale.y *= 1f - squash;
            }
            else if (_bounceTime >= BounceEndTime)
            {
                _bounceTime = -1f;
            }

            // Selection pulse (scale + rotation; _highlightTime already updated above)
            if (_isHighlighted)
            {
                var pulse = 1f + Mathf.Sin(_highlightTime * 8f) * 0.08f;
                finalScale *= pulse;

                // 3D mode: Y-axis rotation
                var rot = transform.localEulerAngles;
                rot.y += 30f * Time.deltaTime;
                transform.localEulerAngles = rot;
            }

            transform.localScale = finalScale;

            // Update blob shadow
            UpdateBlobShadow(worldPos, cellSize, finalScale, pos.z);

            // Alpha via MaterialPropertyBlock
            if (visual.Alpha < 1f)
            {
                _meshRenderer.GetPropertyBlock(_propBlock);
                var mat = _meshRenderer.sharedMaterial;
                var color = mat.HasProperty(ColorProp)
                    ? mat.GetColor(ColorProp)
                    : mat.GetColor(ColorPropFallback);
                color.a = visual.Alpha;
                _propBlock.SetColor(ColorProp, color);
                _propBlock.SetColor(ColorPropFallback, color);
                _meshRenderer.SetPropertyBlock(_propBlock);
            }
            else
            {
                _meshRenderer.SetPropertyBlock(null);
            }

            // Visibility
            gameObject.SetActive(visual.IsVisible);

            // Sync appearance when tile type or bomb type changes
            if (TileType != visual.TileType || BombType != visual.BombType)
            {
                TileType = visual.TileType;
                BombType = visual.BombType;
                _meshFilter.sharedMesh = BombType != BombType.None
                    ? MeshFactory.GetBombMesh(BombType)
                    : MeshFactory.GetTileMesh(TileType);
                ApplyMaterial(TileType, BombType);
            }
        }

        /// <summary>
        /// Set highlight state for selection feedback.
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            if (_isHighlighted == highlighted) return;
            _isHighlighted = highlighted;

            // Emission glow: on when selected, off when deselected
            _meshRenderer.GetPropertyBlock(_propBlock);
            if (highlighted)
            {
                var mat = _meshRenderer.sharedMaterial;
                var baseColor = mat.HasProperty(ColorProp)
                    ? mat.GetColor(ColorProp)
                    : mat.GetColor(ColorPropFallback);
                _propBlock.SetColor(EmissionColorProp, baseColor * 0.3f);
            }
            else
            {
                _propBlock.SetColor(EmissionColorProp, Color.black);
                _highlightTime = 0f;
                transform.localEulerAngles = Vector3.zero;
                transform.localScale = _baseScale;
            }
            _meshRenderer.SetPropertyBlock(_propBlock);
        }

        private void UpdateBlobShadow(Vector3 worldPos, float cellSize, Vector3 tileScale, float tileZ)
        {
            if (_shadowTransform == null) return;

            _hasLastShadowState = true;
            _lastShadowWorldPos = worldPos;
            _lastShadowCellSize = cellSize;
            _lastShadowTileScale = tileScale;
            _lastShadowTileZ = tileZ;

            // Directional offset along sun direction
            Vector2 dir2 = new Vector2(-0.35f, -0.70f).normalized;
            var sun = RenderSettings.sun;
            if (sun != null)
            {
                var d = sun.transform.forward;
                var v = new Vector2(d.x, d.y);
                if (v.sqrMagnitude > 0.0001f)
                    dir2 = v.normalized;
            }

            // Lift factor: floating tile → softer, larger shadow
            float lift01 = Mathf.Clamp01(Mathf.Abs(tileZ) / Mathf.Max(cellSize * 0.06f, 0.0001f));
            float alpha = s_blobShadowBaseAlpha * (1f - lift01 * s_blobShadowLiftAlphaReduce);
            float sizeMul = 1f + lift01 * s_blobShadowLiftSizeIncrease;

            float off = s_blobShadowOffset * cellSize;
            _shadowTransform.position = new Vector3(
                worldPos.x + dir2.x * off,
                worldPos.y + dir2.y * off,
                BlobShadowZ);
            _shadowTransform.rotation = Quaternion.identity;

            float shadowWorldSize = cellSize * s_blobShadowSize * sizeMul;
            _shadowTransform.localScale = new Vector3(
                shadowWorldSize / Mathf.Max(Mathf.Abs(tileScale.x), 0.001f),
                shadowWorldSize / Mathf.Max(Mathf.Abs(tileScale.y), 0.001f),
                1f / Mathf.Max(Mathf.Abs(tileScale.z), 0.001f));

            if (_shadowRenderer != null && _shadowPropBlock != null)
            {
                var c = new Color(1f, 1f, 1f, alpha);
                _shadowRenderer.GetPropertyBlock(_shadowPropBlock);
                _shadowPropBlock.SetColor(ShadowColorProp, c);
                _shadowPropBlock.SetColor(ShadowColorPropFallback, c);
                _shadowRenderer.SetPropertyBlock(_shadowPropBlock);
            }
        }

        private void RefreshBlobShadowNow()
        {
            if (!_hasLastShadowState) return;
            UpdateBlobShadow(_lastShadowWorldPos, _lastShadowCellSize, _lastShadowTileScale, _lastShadowTileZ);
        }

        #region Portal Effects

        private bool _portalActive;

        /// <summary>
        /// Set portal clip scale for hole zone entry/exit effect.
        /// </summary>
        /// <param name="scaleY">0~1 visible ratio.</param>
        /// <param name="anchorTop">True = shrink from bottom (entering), false = grow from bottom (exiting).</param>
        /// <param name="cellSize">Cell size for offset calculation.</param>
        public void SetPortalScale(float scaleY, bool anchorTop, float cellSize)
        {
            _portalActive = true;
            scaleY = Mathf.Clamp01(scaleY);
            var s = transform.localScale;
            float fullY = _baseScale.y;
            if (fullY < 0.001f) fullY = cellSize * TileScaleMultiplier;
            s.y = fullY * scaleY;
            transform.localScale = s;

            // Offset position to anchor the visible part at top or bottom
            float offset = fullY * (1f - scaleY) * 0.5f;
            var pos = transform.position;
            if (anchorTop)
                pos.y += offset; // keep top edge, shrink downward
            else
                pos.y -= offset; // keep bottom edge, grow upward
            transform.position = pos;
        }

        /// <summary>
        /// Reset portal scale effect to normal rendering.
        /// </summary>
        public void ResetPortalScale()
        {
            if (!_portalActive) return;
            _portalActive = false;
            transform.localScale = _baseScale;
        }

        #endregion

        #region IPoolable

        public void OnSpawn()
        {
            TileId = -1;
            TileType = TileType.None;
            BombType = BombType.None;
            _baseScale = Vector3.one;
            _isHighlighted = false;
            _wasAnimated = false;
            _bounceTime = -1f;
            _highlightTime = 0f;
            _portalActive = false;
            transform.localScale = Vector3.one;
            transform.localEulerAngles = Vector3.zero;
            _meshRenderer.SetPropertyBlock(null);
        }

        public void OnDespawn()
        {
            TileId = -1;
            gameObject.SetActive(false);
        }

        #endregion
    }
}
