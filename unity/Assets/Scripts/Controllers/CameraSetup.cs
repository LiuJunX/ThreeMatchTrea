using Match3.Unity.Bridge;
using UnityEngine;

namespace Match3.Unity.Controllers
{
    /// <summary>
    /// Sets up the camera to frame the game board.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraSetup : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Match3Bridge _bridge;

        [Header("Configuration")]
        [SerializeField] private float _padding = 1f;
        [SerializeField] private Color _backgroundColor = new Color(0.15f, 0.15f, 0.2f);

        private Camera _camera;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.backgroundColor = _backgroundColor;
            _camera.clearFlags = CameraClearFlags.SolidColor;
        }

        private void Start()
        {
            if (_bridge == null)
            {
                _bridge = FindObjectOfType<Match3Bridge>();
            }

            // Setup will be called after bridge initialization
        }

        private void LateUpdate()
        {
            if (_bridge != null && _bridge.IsInitialized)
            {
                SetupCamera();
                enabled = false; // Only run once after initialization
            }
        }

        /// <summary>
        /// Setup camera to frame the board.
        /// </summary>
        public void SetupCamera()
        {
            if (_bridge == null) return;

            var width = _bridge.Width;
            var height = _bridge.Height;
            var cellSize = _bridge.CellSize;
            var origin = _bridge.BoardOrigin;

            // Calculate board center
            float boardWidth = width * cellSize;
            float boardHeight = height * cellSize;
            float centerX = origin.x + boardWidth * 0.5f;
            float centerY = origin.y + boardHeight * 0.5f;

            // Position camera
            transform.position = new Vector3(centerX, centerY, -10f);

            // Calculate orthographic size to fit board with padding
            float targetWidth = boardWidth + _padding * 2;
            float targetHeight = boardHeight + _padding * 2;

            float screenAspect = (float)Screen.width / Screen.height;
            float boardAspect = targetWidth / targetHeight;

            if (screenAspect >= boardAspect)
            {
                // Screen is wider than board - fit to height
                _camera.orthographicSize = targetHeight * 0.5f;
            }
            else
            {
                // Screen is taller than board - fit to width
                _camera.orthographicSize = targetWidth * 0.5f / screenAspect;
            }
        }

        /// <summary>
        /// Manually set the bridge reference.
        /// </summary>
        public void SetBridge(Match3Bridge bridge)
        {
            _bridge = bridge;
        }
    }
}
