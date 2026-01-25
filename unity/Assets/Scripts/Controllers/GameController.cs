using Match3.Unity.Bridge;
using Match3.Unity.Views;
using UnityEngine;

namespace Match3.Unity.Controllers
{
    /// <summary>
    /// Main game controller.
    /// Manages game loop: tick simulation, render board.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private Match3Bridge _bridge;
        [SerializeField] private BoardView _boardView;
        [SerializeField] private InputController _inputController;
        [SerializeField] private EffectManager _effectManager;

        [Header("Auto Initialize")]
        [SerializeField] private bool _autoInitialize = true;

        private bool _initialized;

        /// <summary>
        /// Bridge instance for external access.
        /// </summary>
        public Match3Bridge Bridge => _bridge;

        /// <summary>
        /// Board view instance.
        /// </summary>
        public BoardView BoardView => _boardView;

        private void Awake()
        {
            // Auto-create components if not assigned
            if (_bridge == null)
            {
                _bridge = GetComponentInChildren<Match3Bridge>();
                if (_bridge == null)
                {
                    var bridgeGo = new GameObject("Match3Bridge");
                    bridgeGo.transform.SetParent(transform, false);
                    _bridge = bridgeGo.AddComponent<Match3Bridge>();
                }
            }

            if (_boardView == null)
            {
                _boardView = GetComponentInChildren<BoardView>();
                if (_boardView == null)
                {
                    var boardGo = new GameObject("BoardView");
                    boardGo.transform.SetParent(transform, false);
                    _boardView = boardGo.AddComponent<BoardView>();
                }
            }

            if (_inputController == null)
            {
                _inputController = GetComponentInChildren<InputController>();
                if (_inputController == null)
                {
                    var inputGo = new GameObject("InputController");
                    inputGo.transform.SetParent(transform, false);
                    _inputController = inputGo.AddComponent<InputController>();
                }
            }

            if (_effectManager == null)
            {
                _effectManager = GetComponentInChildren<EffectManager>();
                if (_effectManager == null)
                {
                    var effectGo = new GameObject("EffectManager");
                    effectGo.transform.SetParent(transform, false);
                    _effectManager = effectGo.AddComponent<EffectManager>();
                }
            }
        }

        private void Start()
        {
            if (_autoInitialize)
            {
                Initialize();
            }
        }

        /// <summary>
        /// Initialize the game.
        /// </summary>
        public void Initialize()
        {
            if (_initialized) return;

            // Initialize bridge
            _bridge.Initialize();

            // Initialize views
            _boardView.Initialize(_bridge);
            _effectManager.Initialize(_bridge);

            // Initialize input
            _inputController.Initialize(_bridge, _boardView);

            _initialized = true;

            Debug.Log("GameController initialized");
        }

        /// <summary>
        /// Initialize with specific parameters.
        /// </summary>
        public void Initialize(int width, int height, int seed)
        {
            if (_initialized)
            {
                Reset();
            }

            // Initialize bridge with parameters
            _bridge.Initialize(width, height, seed);

            // Initialize views
            _boardView.Initialize(_bridge);
            _effectManager.Initialize(_bridge);

            // Initialize input
            _inputController.Initialize(_bridge, _boardView);

            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized || !_bridge.IsInitialized) return;

            // Tick simulation
            _bridge.Tick(Time.deltaTime);

            // Render board
            _boardView.Render(_bridge.VisualState);

            // Update effects
            _effectManager.UpdateEffects(_bridge.VisualState);
        }

        /// <summary>
        /// Reset the game.
        /// </summary>
        public void Reset()
        {
            _boardView.Clear();
            _effectManager.Clear();
            _initialized = false;
        }
    }
}
