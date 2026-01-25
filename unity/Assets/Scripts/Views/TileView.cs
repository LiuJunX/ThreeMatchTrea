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
        public long TileId { get; private set; }

        /// <summary>
        /// Current tile type.
        /// </summary>
        public TileType TileType { get; private set; }

        /// <summary>
        /// Current bomb type.
        /// </summary>
        public BombType BombType { get; private set; }

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
        public void Setup(long id, TileType type, BombType bomb)
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
            // Update position (with Y-flip for Unity coordinate system)
            var worldPos = CoordinateConverter.GridToWorld(visual.Position, cellSize, origin, height);
            transform.position = worldPos;

            // Update scale
            var scale = visual.Scale;
            transform.localScale = new Vector3(scale.X * cellSize, scale.Y * cellSize, 1f);

            // Update alpha
            var color = _renderer.color;
            color.a = visual.Alpha;
            _renderer.color = color;

            // Update visibility
            gameObject.SetActive(visual.IsVisible);

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
            if (highlighted)
            {
                transform.localScale *= 1.1f;
            }
        }

        #region IPoolable

        public void OnSpawn()
        {
            TileId = -1;
            TileType = TileType.None;
            BombType = BombType.None;
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
