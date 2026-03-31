using System;
using System.Collections.Generic;
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
        private string[] _officialLevelIds;
        private string[] _testLevelIds;
        private string _currentLevelId;
        private int _lastStars;
        private bool _lastVictory;

        private void Awake()
        {
            Application.runInBackground = true;

            // Target 120fps on high-refresh-rate devices
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 120;

            _progressService = new PlayerProgressService();
            _progress = _progressService.Load();
            Debug.Log($"[GameFlow] Loaded progress: {_progress.UnlockedLevels.Count} levels unlocked, {_progress.BestStars.Count} with stars");

            // Load and split level IDs into official vs test
            var allIds = UnityConfigProvider.Instance.GetLevelIds() ?? Array.Empty<string>();
            Array.Sort(allIds, StringComparer.Ordinal);

            var officialList = new List<string>();
            var testList = new List<string>();
            foreach (var id in allIds)
            {
                if (id.StartsWith("test_", StringComparison.Ordinal))
                    testList.Add(id);
                else
                    officialList.Add(id);
            }
            _officialLevelIds = officialList.ToArray();
            _testLevelIds = testList.ToArray();

            // Ensure first official level is always unlocked
            if (_officialLevelIds.Length > 0)
                _progress.UnlockedLevels.Add(_officialLevelIds[0]);

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
                msaaProp?.SetValue(rpAsset, 2);
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
                        topPanel.OnPrevLevelClicked -= OnPrevLevel;
                        topPanel.OnReplayClicked -= OnReplayLevel;
                        topPanel.OnNextLevelClicked -= OnNextLevel;
                        topPanel.SetQuitButtonVisible(false);
                        topPanel.SetNavButtonsVisible(false);
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
                    _levelSelectPanel.Populate(_officialLevelIds, _testLevelIds, _progress);
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

            // Show quit button and nav buttons, wire them
            var topPanel = _gameController.UI?.TopPanel;
            if (topPanel != null)
            {
                topPanel.SetQuitButtonVisible(true);
                topPanel.OnQuitClicked += OnQuitLevel;

                topPanel.SetNavButtonsVisible(true);
                topPanel.SetPrevEnabled(HasPrevLevel());
                topPanel.SetNextEnabled(HasNextLevel());
                topPanel.OnPrevLevelClicked += OnPrevLevel;
                topPanel.OnReplayClicked += OnReplayLevel;
                topPanel.OnNextLevelClicked += OnNextLevel;
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

                // Unlock next official level (test levels are always unlocked)
                if (!_currentLevelId.StartsWith("test_", StringComparison.Ordinal))
                {
                    int idx = Array.IndexOf(_officialLevelIds, _currentLevelId);
                    if (idx >= 0 && idx + 1 < _officialLevelIds.Length)
                    {
                        var nextId = _officialLevelIds[idx + 1];
                        _progress.UnlockedLevels.Add(nextId);
                        Debug.Log($"[GameFlow] Unlocked '{nextId}'");
                    }
                }

                _progressService.Save(_progress);
                Debug.Log($"[GameFlow] Progress saved: {_progress.UnlockedLevels.Count} levels unlocked");
            }

            // Show result with stars
            _gameController.UI?.ShowResult(isVictory, score, _lastStars);

            TransitionTo(FlowState.Result);
        }

        /// <summary>
        /// Returns the level list that contains the current level (official or test).
        /// </summary>
        private string[] GetCurrentLevelList()
        {
            if (_currentLevelId != null && _currentLevelId.StartsWith("test_", StringComparison.Ordinal))
                return _testLevelIds;
            return _officialLevelIds;
        }

        private bool HasNextLevel()
        {
            var list = GetCurrentLevelList();
            int idx = Array.IndexOf(list, _currentLevelId);
            return idx >= 0 && idx + 1 < list.Length;
        }

        private bool HasPrevLevel()
        {
            var list = GetCurrentLevelList();
            int idx = Array.IndexOf(list, _currentLevelId);
            return idx > 0;
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
            var list = GetCurrentLevelList();
            int idx = Array.IndexOf(list, _currentLevelId);
            if (idx >= 0 && idx + 1 < list.Length)
                _currentLevelId = list[idx + 1];
            TransitionTo(FlowState.Playing);
        }

        private void OnPrevLevel()
        {
            if (!HasPrevLevel()) return;
            var list = GetCurrentLevelList();
            int idx = Array.IndexOf(list, _currentLevelId);
            _currentLevelId = list[idx - 1];
            TransitionTo(FlowState.Playing);
        }

        private void OnReplayLevel()
        {
            // Restart the same level with a new seed
            TransitionTo(FlowState.Playing);
        }

        private void OnNextLevel()
        {
            if (!HasNextLevel()) return;
            var list = GetCurrentLevelList();
            int idx = Array.IndexOf(list, _currentLevelId);
            _currentLevelId = list[idx + 1];
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
