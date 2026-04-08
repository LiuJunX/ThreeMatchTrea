using Match3.Core.Models.Enums;
using Match3.Presentation;
using Match3.Unity.Bridge;
using Match3.Unity.Controllers;
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

        private Vector3 _baseScale = Vector3.one;
        private bool _isHighlighted;
        private bool _isHinted;
        private HintAnimationType _hintType;
        private Vector2 _hintNudgeDir;
        private float _hintTime;
        private bool _wasAnimated;
        private float _bounceTime = -1f;
        private float _highlightTime;

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
        /// Initialize the tile with ID and type.
        /// </summary>
        public void Setup(int id, ElementType type)
        {
            TileId = id;
            ApplyAppearance(type);
        }

        /// <summary>
        /// Update tile from visual state.
        /// </summary>
        public void UpdateFromVisual(TileVisual visual, float cellSize, Vector2 origin, int height, float dt)
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

            // Update scale (store base scale for highlight effect)
            var scale = visual.Scale;
            _baseScale = new Vector3(scale.X * cellSize, scale.Y * cellSize, 1f);

            var finalScale = _baseScale;

            // Landing bounce
            {
                var (newBounce, squash, _) = TileAnimationHelper.CalculateBounceSquash(_bounceTime, dt);
                _bounceTime = newBounce;
                if (squash != 0f)
                {
                    finalScale.x *= 1f + squash;
                    finalScale.y *= 1f - squash;
                }
            }

            // Hint animation (selection overrides hint)
            if (_isHinted && !_isHighlighted)
            {
                var (newHint, pulse, phase) = TileAnimationHelper.CalculateHintPulse(_hintTime, dt, _hintType);
                _hintTime = newHint;
                finalScale *= pulse;

                if (_hintType == HintAnimationType.SwapNudge)
                {
                    var nudge = TileAnimationHelper.CalculateSwapNudgeOffset(phase, cellSize);
                    pos += new Vector3(_hintNudgeDir.x * nudge, _hintNudgeDir.y * nudge, 0f);
                    // Tilt toward movement direction during nudge (2D: Z-axis rotation)
                    var tiltAngle = TileAnimationHelper.CalculateSwapNudgeTiltAngle(phase);
                    var tilt = tiltAngle * (-_hintNudgeDir.x + _hintNudgeDir.y);
                    transform.localEulerAngles = new Vector3(0f, 0f, tilt);
                }
            }

            // Apply final position
            transform.position = pos;

            // Selection pulse
            if (_isHighlighted)
            {
                _highlightTime += dt;
                var pulse = TileAnimationHelper.CalculateSelectionPulse(_highlightTime);
                finalScale *= pulse;
            }

            // Animation-driven rotation (color bomb spin etc.)
            if (visual.Rotation != 0f)
            {
                pos.z = -1f; // Float above other tiles (2D sorting)
                transform.position = pos;
                transform.localEulerAngles = new Vector3(0f, 0f, visual.Rotation);
            }

            transform.localScale = finalScale;

            // Update alpha
            var color = _renderer.color;
            color.a = visual.Alpha;
            _renderer.color = color;

            // Update visibility
            gameObject.SetActive(visual.IsVisible);

            // Immediate mode: sync appearance from visual state every frame
            ApplyAppearance(visual.TileType);
        }

        private void ApplyAppearance(ElementType type)
        {
            ViewHelper.SetSprite(_renderer, SpriteFactory.GetTileSprite(type));
            ViewHelper.SetBombOverlay(_bombOverlay, _bombOverlayGo,
                type.IsBomb() ? SpriteFactory.GetBombOverlay(type) : null);
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
        /// Set hint animation state.
        /// </summary>
        public void SetHinted(bool hinted, HintAnimationType type = HintAnimationType.None, Vector2 nudgeDir = default)
        {
            _isHinted = hinted;
            _hintType = type;
            _hintNudgeDir = nudgeDir;
            if (!hinted)
            {
                _hintTime = 0f;
                transform.localEulerAngles = Vector3.zero;
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
            _baseScale = Vector3.one;
            _isHighlighted = false;
            _isHinted = false;
            _hintType = HintAnimationType.None;
            _hintTime = 0f;
            _wasAnimated = false;
            _bounceTime = -1f;
            _highlightTime = 0f;
            transform.localScale = Vector3.one;
            transform.localEulerAngles = Vector3.zero;
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
