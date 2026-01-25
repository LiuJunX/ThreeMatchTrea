    using Match3.Unity.Bridge;
using Match3.Unity.Views;
using UnityEngine;

namespace Match3.Unity.Controllers
{
    /// <summary>
    /// Bootstrap script that creates the entire game hierarchy at runtime.
    /// Attach this to an empty GameObject in your scene.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Game Configuration")]
        [SerializeField] private int _boardWidth = 8;
        [SerializeField] private int _boardHeight = 8;
        [SerializeField] private int _seed = 0;

        private GameController _gameController;

        private void Awake()
        {
            // Create game root
            var gameRoot = new GameObject("Match3Game");

            // Create and configure bridge
            var bridgeGo = new GameObject("Match3Bridge");
            bridgeGo.transform.SetParent(gameRoot.transform, false);
            var bridge = bridgeGo.AddComponent<Match3Bridge>();

            // Set bridge configuration via serialized fields reflection
            // (Alternative: expose public setters or use Initialize overload)

            // Create board view
            var boardGo = new GameObject("BoardView");
            boardGo.transform.SetParent(gameRoot.transform, false);
            var boardView = boardGo.AddComponent<BoardView>();

            // Create effect manager
            var effectGo = new GameObject("EffectManager");
            effectGo.transform.SetParent(gameRoot.transform, false);
            var effectManager = effectGo.AddComponent<EffectManager>();

            // Create input controller
            var inputGo = new GameObject("InputController");
            inputGo.transform.SetParent(gameRoot.transform, false);
            var inputController = inputGo.AddComponent<InputController>();

            // Create game controller
            var controllerGo = new GameObject("GameController");
            controllerGo.transform.SetParent(gameRoot.transform, false);
            _gameController = controllerGo.AddComponent<GameController>();

            // Setup camera
            SetupCamera(bridge);
        }

        private void Start()
        {
            // Initialize with configured parameters
            int seed = _seed != 0 ? _seed : System.Environment.TickCount;
            _gameController.Initialize(_boardWidth, _boardHeight, seed);

            // Setup camera after initialization
            var cameraSetup = Camera.main.GetComponent<CameraSetup>();
            if (cameraSetup != null)
            {
                cameraSetup.SetBridge(_gameController.Bridge);
                cameraSetup.SetupCamera();
            }

            Debug.Log($"Match3 Game Started: {_boardWidth}x{_boardHeight}, seed={seed}");
        }

        private void SetupCamera(Match3Bridge bridge)
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var cameraGo = new GameObject("Main Camera");
                cameraGo.tag = "MainCamera";
                mainCamera = cameraGo.AddComponent<Camera>();
                cameraGo.AddComponent<AudioListener>();
            }

            // Add camera setup if not present
            var cameraSetup = mainCamera.GetComponent<CameraSetup>();
            if (cameraSetup == null)
            {
                cameraSetup = mainCamera.gameObject.AddComponent<CameraSetup>();
            }

            // Configure camera
            mainCamera.orthographic = true;
            mainCamera.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
        }
    }
}
