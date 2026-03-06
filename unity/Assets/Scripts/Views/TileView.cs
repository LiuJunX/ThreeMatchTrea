using Match3.Core.Models.Enums;
using Match3.Presentation;
using Match3.Unity.Bridge;
using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views
{
    /// <summary>
    /// Visual representation of a single tile.
    /// Handles rendering tile color and bomb overlays.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TileView : MonoBehaviour, IPoolable
    {
        private SpriteRenderer _renderer;
        private SpriteRenderer _bombOverlay;
        private GameObject _bombOverlayGo;

        /// <summary>
        /// Unique tile ID from the game state.
        /// </summary>
        public int TileId { get; private set; }

        /// <summary>
        /// Current tile type.
        /// </summary>
        public TileType TileType { get; private set; }

        /// <summary>
        /// Current bomb type.
        /// </summary>
        public BombType BombType { get; private set; }

        private Vector3 _baseScale = Vector3.one;
        private bool _isHighlighted;
        private bool _wasAnimated;
        private float _bounceTime = -1f;
        private float _highlightTime;

        private const float BounceEndTime = 0.15f;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            CreateBombOverlay();
        }

        private void CreateBombOverlay()
        {
            _bombOverlayGo = new GameObject("BombOverlay");
            _bombOverlayGo.transform.SetParent(transform, false);
            _bombOverlayGo.transform.localPosition = Vector3.zero;

            _bombOverlay = _bombOverlayGo.AddComponent<SpriteRenderer>();
            _bombOverlay.sortingLayerName = "Tiles";
            _bombOverlay.sortingOrder = 11; // Above tile
            _bombOverlayGo.SetActive(false);
        }

        /// <summary>
        /// Initialize the tile with ID, type, and bomb.
        /// </summary>
        public void Setup(int id, TileType type, BombType bomb)
        {
            TileId = id;
            TileType = type;
            BombType = bomb;

            UpdateSprites();
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

            // Update position (with Y-flip for Unity coordinate system)
            var worldPos = CoordinateConverter.GridToWorld(visual.Position, cellSize, origin, height);
            var pos = (Vector3)worldPos;
            // NOTE: Idle breathing intentionally disabled (keep tiles perfectly still when idle).

            transform.position = pos;

            // Update scale (store base scale for highlight effect)
            var scale = visual.Scale;
            _baseScale = new Vector3(scale.X * cellSize, scale.Y * cellSize, 1f);

            var finalScale = _baseScale;

            // Landing bounce
            if (_bounceTime >= 0f && _bounceTime < BounceEndTime)
            {
                _bounceTime += Time.deltaTime;
                var t = _bounceTime / BounceEndTime;
                var squash = Mathf.Sin(t * Mathf.PI) * 0.1f;
                finalScale.x *= 1f + squash;
                finalScale.y *= 1f - squash;
            }
            else if (_bounceTime >= BounceEndTime)
            {
                _bounceTime = -1f;
            }

            // Selection pulse
            if (_isHighlighted)
            {
                _highlightTime += Time.deltaTime;
                var pulse = 1f + Mathf.Sin(_highlightTime * 8f) * 0.08f;
                finalScale *= pulse;
            }

            transform.localScale = finalScale;

            // Update alpha
            var color = _renderer.color;
            color.a = visual.Alpha;
            _renderer.color = color;

            // Update visibility
            gameObject.SetActive(visual.IsVisible);

            // Update tile type if changed (e.g., after board shuffle)
            if (TileType != visual.TileType)
            {
                TileType = visual.TileType;
                UpdateSprites();
            }

            // Update bomb overlay if type changed
            if (BombType != visual.BombType)
            {
                BombType = visual.BombType;
                UpdateBombOverlay();
            }
        }

        private void UpdateSprites()
        {
            _renderer.sprite = SpriteFactory.GetTileSprite(TileType);
            UpdateBombOverlay();
        }

        private void UpdateBombOverlay()
        {
            if (BombType == BombType.None)
            {
                _bombOverlayGo.SetActive(false);
                return;
            }

            var overlaySprite = SpriteFactory.GetBombOverlay(BombType);
            if (overlaySprite != null)
            {
                _bombOverlay.sprite = overlaySprite;
                _bombOverlayGo.SetActive(true);
            }
        }

        /// <summary>
        /// Set highlight state for selection feedback.
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            if (_isHighlighted == highlighted) return;
            _isHighlighted = highlighted;

            if (!highlighted)
            {
                _highlightTime = 0f;
                transform.localScale = _baseScale;
            }
        }

        /// <summary>
        /// Update base scale (called after UpdateFromVisual sets the scale).
        /// </summary>
        public void SetBaseScale(Vector3 scale)
        {
            _baseScale = scale;
            if (!_isHighlighted)
            {
                transform.localScale = _baseScale;
            }
        }

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
            transform.localScale = Vector3.one;
            _renderer.color = Color.white;
            _bombOverlayGo.SetActive(false);
        }

        public void OnDespawn()
        {
            TileId = -1;
            gameObject.SetActive(false);
        }

        #endregion
    }
}
