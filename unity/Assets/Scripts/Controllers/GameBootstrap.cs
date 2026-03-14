using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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

        [Header("Rendering")]
        [SerializeField] private RenderMode _renderMode = RenderMode.View2D;

        private GameController _gameController;

        private void Awake()
        {
            // Skip if disabled (GameFlowController may coexist on same object)
            if (!enabled) return;

            // Ensure game runs when editor loses focus (needed for MCP automation)
            Application.runInBackground = true;

            // Create game root with GameController
            // GameController.Awake auto-creates Bridge, EffectManager, InputController as children
            var gameRoot = new GameObject("Match3Game");
            _gameController = gameRoot.AddComponent<GameController>();
            _gameController.RenderMode = _renderMode;

            // Setup camera
            SetupCamera();
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

        private void SetupCamera()
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

            // MSAA disabled — Fresnel soft edges handle aliasing
            var rpAsset = GraphicsSettings.currentRenderPipeline;
            if (rpAsset != null)
            {
                var msaaProp = rpAsset.GetType().GetProperty("msaaSampleCount");
                if (msaaProp != null)
                    msaaProp.SetValue(rpAsset, 2);
            }

            // SMAA + post-processing
            var camData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
            if (camData != null)
            {
                camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                camData.antialiasingQuality = AntialiasingQuality.High;
                camData.renderPostProcessing = true;
            }

            // Global Volume with Bloom
            SetupBloom();
        }

        private static void SetupBloom()
        {
            var volumeGo = new GameObject("PostProcessVolume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var bloom = volume.profile.Add<Bloom>();
            bloom.threshold.value = 0.9f;
            bloom.intensity.value = 0.4f;
            bloom.scatter.value = 0.7f;
        }
    }
}
