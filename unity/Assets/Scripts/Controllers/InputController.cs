using Match3.Core.Models.Grid;
using Match3.Unity.Bridge;
using Match3.Unity.Views;
using UnityEngine;

namespace Match3.Unity.Controllers
{
    /// <summary>
    /// Handles player input (touch/mouse).
    /// Delegates to Core's SimulationEngine.HandleTap for game logic.
    /// Only handles gesture detection (tap vs swipe).
    /// </summary>
    public sealed class InputController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private float _swipeThreshold = 0.3f;

        private Match3Bridge _bridge;
        private BoardView _boardView;
        private Camera _mainCamera;

        private Vector3 _pointerDownWorld;
        private Position _pointerDownGrid;
        private bool _isPointerDown;

        /// <summary>
        /// Initialize the input controller.
        /// </summary>
        public void Initialize(Match3Bridge bridge, BoardView boardView)
        {
            _bridge = bridge;
            _boardView = boardView;
            _mainCamera = Camera.main;
            _isPointerDown = false;
        }

        private void Update()
        {
            if (_bridge == null || !_bridge.IsInitialized) return;
            if (_mainCamera == null) _mainCamera = Camera.main;

            HandleInput();
        }

        private void HandleInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                OnPointerDown(GetWorldPosition());
            }

            if (Input.GetMouseButtonUp(0))
            {
                OnPointerUp(GetWorldPosition());
            }
        }

        private void OnPointerDown(Vector3 worldPos)
        {
            var gridPos = WorldToGrid(worldPos);
            if (!gridPos.IsValid) return;

            _pointerDownWorld = worldPos;
            _pointerDownGrid = gridPos;
            _isPointerDown = true;
        }

        private void OnPointerUp(Vector3 worldPos)
        {
            if (!_isPointerDown) return;
            _isPointerDown = false;

            if (!_bridge.IsIdle()) return;

            var delta = worldPos - _pointerDownWorld;
            var cellSize = _bridge.CellSize;

            // Check if this is a swipe or a tap
            if (delta.magnitude > cellSize * _swipeThreshold)
            {
                // Swipe - determine direction and apply move
                var targetPos = GetSwipeTarget(_pointerDownGrid, delta);
                if (targetPos.IsValid)
                {
                    _bridge.ApplyMove(_pointerDownGrid, targetPos);
                }
            }
            else
            {
                // Tap - delegate to Core's HandleTap
                _bridge.HandleTap(_pointerDownGrid);
            }
        }

        private Position GetSwipeTarget(Position from, Vector3 delta)
        {
            // Determine primary direction (horizontal or vertical)
            // This handles 45-degree diagonal swipes by choosing dominant axis
            Position target;
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                // Horizontal swipe
                target = delta.x > 0
                    ? new Position(from.X + 1, from.Y)  // Right
                    : new Position(from.X - 1, from.Y); // Left
            }
            else
            {
                // Vertical swipe
                // Unity Y+ is up, but Core Y+ is down
                // So Unity swipe up (delta.y > 0) = Core Y-1 (up in grid)
                target = delta.y > 0
                    ? new Position(from.X, from.Y - 1)  // Up in Unity = Up in Grid (Y-1)
                    : new Position(from.X, from.Y + 1); // Down in Unity = Down in Grid (Y+1)
            }

            // Validate target is within bounds and has a tile
            if (_bridge.GetTileIdAt(target) >= 0)
            {
                return target;
            }

            return Position.Invalid;
        }

        private Position WorldToGrid(Vector3 worldPos)
        {
            return CoordinateConverter.WorldToGrid(
                worldPos, _bridge.CellSize, _bridge.BoardOrigin,
                _bridge.Width, _bridge.Height);
        }

        private Vector3 GetWorldPosition()
        {
            var screenPos = Input.mousePosition;
            screenPos.z = -_mainCamera.transform.position.z;
            return _mainCamera.ScreenToWorldPoint(screenPos);
        }
    }
}
