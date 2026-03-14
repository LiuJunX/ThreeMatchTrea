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
        private int _lastStars;
        private bool _lastVictory;

        private void Awake()
        {
            Application.runInBackground = true;

            _progressService = new PlayerProgressService();
            _progress = _progressService.Load();
            Debug.Log($"[GameFlow] Loaded progress: {_progress.UnlockedLevels.Count} levels unlocked, {_progress.BestStars.Count} with stars");

            // Load level IDs
            _allLevelIds = UnityConfigProvider.Instance.GetLevelIds();
            if (_allLevelIds == null) _allLevelIds = Array.Empty<string>();
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

            // MSAA disabled — Fresnel soft edges handle aliasing
            var rpAsset = GraphicsSettings.currentRenderPipeline;
            if (rpAsset != null)
            {
                var msaaProp = rpAsset.GetType().GetProperty("msaaSampleCount");
                msaaProp?.SetValue(rpAsset, 1);
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
                    var topPanel = _gameController.UI?.TopPanel;
                    if (topPanel != null)
                    {
                        topPanel.OnQuitClicked -= OnQuitLevel;
                        topPanel.SetQuitButtonVisible(false);
                    }
                    break;
                case FlowState.Result:
                    UnwireResultButtons();
                    _gameController.UI?.HideResult();
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

            // Disable UIManager's auto-result (flow mode handles it)
            _gameController.UI?.SetAutoResultEnabled(false);

            // Show quit button and wire it
            var topPanel = _gameController.UI?.TopPanel;
            if (topPanel != null)
            {
                topPanel.SetQuitButtonVisible(true);
                topPanel.OnQuitClicked += OnQuitLevel;
            }

            // Wire game-end callback
            _gameController.Bridge.OnGameEnded += OnGameEnded;

            // Setup camera to frame the board
            _cameraSetup.SetBridge(_gameController.Bridge);
            _cameraSetup.SetupCamera();

            Debug.Log($"[GameFlow] Playing level '{levelId}', seed={seed}");
        }

        private void OnGameEnded(bool isVictory, int score)
        {
            // Stop auto-play when game ends
            _gameController.Bridge.IsAutoPlaying = false;
            _gameController.UI?.SetAutoPlay(false);

            _lastStars = 0;
            _lastVictory = isVictory;
            if (isVictory)
            {
                var bridge = _gameController.Bridge;
                _lastStars = PlayerProgress.CalculateStars(bridge.MovesRemaining, bridge.MoveLimit);
                _progress.SetBestStars(_currentLevelId, _lastStars);

                // Unlock next level
                int idx = Array.IndexOf(_allLevelIds, _currentLevelId);
                if (idx >= 0 && idx + 1 < _allLevelIds.Length)
                {
                    var nextId = _allLevelIds[idx + 1];
                    _progress.UnlockedLevels.Add(nextId);
                    Debug.Log($"[GameFlow] Unlocked '{nextId}'");
                }

                _progressService.Save(_progress);
                Debug.Log($"[GameFlow] Progress saved: {_progress.UnlockedLevels.Count} levels unlocked");
            }

            // Show result with stars
            _gameController.UI?.ShowResult(isVictory, score, _lastStars);

            TransitionTo(FlowState.Result);
        }

        private bool HasNextLevel()
        {
            int idx = Array.IndexOf(_allLevelIds, _currentLevelId);
            return idx >= 0 && idx + 1 < _allLevelIds.Length;
        }

        private void WireResultButtons()
        {
            var resultPanel = _gameController.UI?.ResultPanel;
            if (resultPanel == null) return;

            // Show Level Select always; show Next Level only on victory AND if there IS a next level
            resultPanel.SetFlowButtonsVisible(true);
            if (!_lastVictory || !HasNextLevel())
                resultPanel.SetNextLevelVisible(false);

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
            TransitionTo(FlowState.Playing);
        }

        private void OnResultNextLevel()
        {
            int idx = Array.IndexOf(_allLevelIds, _currentLevelId);
            if (idx >= 0 && idx + 1 < _allLevelIds.Length)
                _currentLevelId = _allLevelIds[idx + 1];
            TransitionTo(FlowState.Playing);
        }

        private void OnResultLevelSelect()
        {
            TransitionTo(FlowState.LevelSelect);
        }

        private void OnQuitLevel()
        {
            TransitionTo(FlowState.LevelSelect);
        }

        private void OnDestroy()
        {
            // Clean up bridge subscription if destroyed while playing
            if (_currentState == FlowState.Playing && _gameController != null)
                _gameController.Bridge.OnGameEnded -= OnGameEnded;

            UnwireResultButtons();

            // Destroy GameController (it's not a child of this transform)
            if (_gameController != null)
                Destroy(_gameController.gameObject);
        }
    }
}
