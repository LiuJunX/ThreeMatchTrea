using System;
using Match3.Core.Progress;
using Match3.Unity.Bridge;
using Match3.Unity.Services;
using Match3.Unity.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace Match3.Unity.Controllers
{
    /// <summary>
    /// Top-level flow controller.
    /// MainMenu → LevelSelect → Playing → Result → LevelSelect
    /// Replaces GameBootstrap as the scene entry point.
    /// </summary>
    public sealed class GameFlowController : MonoBehaviour
    {
        [Header("Rendering")]
        [SerializeField] private RenderMode _renderMode = RenderMode.View2D;

        private enum FlowState { None, MainMenu, LevelSelect, Playing, Result }

        private FlowState _currentState = FlowState.None;
        private GameController _gameController;
        private MainMenuPanel _mainMenuPanel;
        private LevelSelectPanel _levelSelectPanel;
        private CameraSetup _cameraSetup;

        private PlayerProgressService _progressService;
        private PlayerProgress _progress;
        private string[] _allLevelIds;
        private string _currentLevelId;

        private void Awake()
        {
            Application.runInBackground = true;

            _progressService = new PlayerProgressService();
            _progress = _progressService.Load();

            // Load level IDs
            _allLevelIds = UnityConfigProvider.Instance.GetLevelIds();
            Array.Sort(_allLevelIds, StringComparer.Ordinal);

            // Ensure first level is always unlocked
            if (_allLevelIds.Length > 0)
                _progress.UnlockedLevels.Add(_allLevelIds[0]);

            SetupCamera();
            CreateGameController();
            CreateFlowPanels();

            TransitionTo(FlowState.MainMenu);
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

            _cameraSetup = mainCamera.GetComponent<CameraSetup>();
            if (_cameraSetup == null)
                _cameraSetup = mainCamera.gameObject.AddComponent<CameraSetup>();

            mainCamera.orthographic = true;
            mainCamera.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
            mainCamera.clearFlags = CameraClearFlags.SolidColor;

            // URP MSAA
            var rpAsset = GraphicsSettings.currentRenderPipeline;
            if (rpAsset != null)
            {
                var msaaProp = rpAsset.GetType().GetProperty("msaaSampleCount");
                msaaProp?.SetValue(rpAsset, 4);
            }

            // SMAA
            var camData = mainCamera.GetComponent("UniversalAdditionalCameraData");
            if (camData != null)
            {
                var aaMode = camData.GetType().GetProperty("antialiasing");
                var aaQuality = camData.GetType().GetProperty("antialiasingQuality");
                aaMode?.SetValue(camData, 2);
                aaQuality?.SetValue(camData, 2);
            }
        }

        private void CreateGameController()
        {
            var gameRoot = new GameObject("Match3Game");
            _gameController = gameRoot.AddComponent<GameController>();
            _gameController.RenderMode = _renderMode;
            gameRoot.SetActive(false);
        }

        private void CreateFlowPanels()
        {
            var menuGo = new GameObject("MainMenuPanel");
            menuGo.transform.SetParent(transform, false);
            _mainMenuPanel = menuGo.AddComponent<MainMenuPanel>();
            _mainMenuPanel.Initialize();
            _mainMenuPanel.OnPlayClicked += () => TransitionTo(FlowState.LevelSelect);
            _mainMenuPanel.Hide();

            var selectGo = new GameObject("LevelSelectPanel");
            selectGo.transform.SetParent(transform, false);
            _levelSelectPanel = selectGo.AddComponent<LevelSelectPanel>();
            _levelSelectPanel.Initialize();
            _levelSelectPanel.OnLevelSelected += OnLevelSelected;
            _levelSelectPanel.OnBackClicked += () => TransitionTo(FlowState.MainMenu);
            _levelSelectPanel.Hide();
        }

        private void TransitionTo(FlowState newState)
        {
            ExitState(_currentState);
            _currentState = newState;
            EnterState(newState);
        }

        private void ExitState(FlowState state)
        {
            switch (state)
            {
                case FlowState.MainMenu:
                    _mainMenuPanel.Hide();
                    break;
                case FlowState.LevelSelect:
                    _levelSelectPanel.Hide();
                    break;
                case FlowState.Playing:
                    _gameController.Bridge.OnGameEnded -= OnGameEnded;
                    break;
                case FlowState.Result:
                    // Result panel hidden by UIManager when transitioning away
                    break;
            }
        }

        private void EnterState(FlowState state)
        {
            switch (state)
            {
                case FlowState.MainMenu:
                    _gameController.gameObject.SetActive(false);
                    _mainMenuPanel.Show();
                    break;

                case FlowState.LevelSelect:
                    _gameController.gameObject.SetActive(false);
                    _levelSelectPanel.Populate(_allLevelIds, _progress);
                    _levelSelectPanel.Show();
                    break;

                case FlowState.Playing:
                    StartLevel(_currentLevelId);
                    break;

                case FlowState.Result:
                    // ResultPanel is shown by UIManager via OnGameEnded bridge event;
                    // we just need to wire the flow buttons
                    WireResultButtons();
                    break;
            }
        }

        private void OnLevelSelected(string levelId)
        {
            _currentLevelId = levelId;
            TransitionTo(FlowState.Playing);
        }

        private void StartLevel(string levelId)
        {
            _gameController.gameObject.SetActive(true);

            int seed = Environment.TickCount;
            _gameController.InitializeWithLevel(levelId, seed);

            // Wire game-end callback
            _gameController.Bridge.OnGameEnded += OnGameEnded;

            // Setup camera to frame the board
            _cameraSetup.SetBridge(_gameController.Bridge);
            _cameraSetup.SetupCamera();

            Debug.Log($"[GameFlow] Playing level '{levelId}', seed={seed}");
        }

        private void OnGameEnded(bool isVictory, int score)
        {
            int stars = 0;
            if (isVictory)
            {
                var bridge = _gameController.Bridge;
                stars = PlayerProgress.CalculateStars(bridge.MovesRemaining, bridge.MoveLimit);
                _progress.SetBestStars(_currentLevelId, stars);

                // Unlock next level
                int idx = Array.IndexOf(_allLevelIds, _currentLevelId);
                if (idx >= 0 && idx + 1 < _allLevelIds.Length)
                    _progress.UnlockedLevels.Add(_allLevelIds[idx + 1]);

                _progressService.Save(_progress);
            }

            // Show result with stars (overrides UIManager's auto-show)
            _gameController.UI?.ShowResult(isVictory, score, stars);

            TransitionTo(FlowState.Result);
        }

        private void WireResultButtons()
        {
            var ui = _gameController.UI;
            if (ui == null) return;

            var resultPanel = ui.ResultPanel;
            if (resultPanel == null) return;

            resultPanel.SetFlowButtonsVisible(true);

            // Temporarily wire flow actions
            resultPanel.OnRestartClicked += OnResultRestart;
            resultPanel.OnNextLevelClicked += OnResultNextLevel;
            resultPanel.OnLevelSelectClicked += OnResultLevelSelect;
        }

        private void UnwireResultButtons()
        {
            var resultPanel = _gameController.UI?.ResultPanel;
            if (resultPanel == null) return;

            resultPanel.OnRestartClicked -= OnResultRestart;
            resultPanel.OnNextLevelClicked -= OnResultNextLevel;
            resultPanel.OnLevelSelectClicked -= OnResultLevelSelect;
        }

        private void OnResultRestart()
        {
            UnwireResultButtons();
            TransitionTo(FlowState.Playing);
        }

        private void OnResultNextLevel()
        {
            UnwireResultButtons();
            int idx = Array.IndexOf(_allLevelIds, _currentLevelId);
            if (idx >= 0 && idx + 1 < _allLevelIds.Length)
                _currentLevelId = _allLevelIds[idx + 1];
            TransitionTo(FlowState.Playing);
        }

        private void OnResultLevelSelect()
        {
            UnwireResultButtons();
            TransitionTo(FlowState.LevelSelect);
        }

        private void OnDestroy()
        {
            UnwireResultButtons();
        }
    }
}
