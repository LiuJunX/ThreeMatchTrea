using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Match3.Core.Analysis;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Scenarios;
using Match3.Editor.Interfaces;
using Match3.Editor.Logic;

namespace Match3.Editor.ViewModels
{
    public enum EditorLayer
    {
        Tiles,
        Covers,
        Grounds
    }

    public class LevelEditorViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IPlatformService _platform;
        private readonly IJsonService _jsonService;

        private readonly EditorSession _session;
        private readonly GridManipulator _gridManipulator;

        // --- Extracted Managers ---
        private readonly LevelAnalysisManager _analysisManager;
        private readonly LevelFileManager _levelFileManager;
        private readonly ScenarioFileManager _scenarioFileManager;

        // --- Core State (Delegated to Session) ---
        public EditorMode CurrentMode
        {
            get => _session.CurrentMode;
            set => _session.CurrentMode = value;
        }

        public LevelConfig CurrentLevel
        {
            get => _session.CurrentLevel;
            set => _session.CurrentLevel = value;
        }

        public ScenarioConfig CurrentScenario
        {
            get => _session.CurrentScenario;
            set => _session.CurrentScenario = value;
        }

        public ScenarioMetadata CurrentScenarioMetadata { get; set; } = new ScenarioMetadata();

        public string ScenarioName
        {
            get => _session.ScenarioName;
            set => _session.ScenarioName = value;
        }

        public string ScenarioDescription
        {
            get => _session.ScenarioDescription;
            set => _session.ScenarioDescription = value;
        }

        public bool IsDirty
        {
            get => _session.IsDirty;
            set => _session.IsDirty = value;
        }

        // --- Editor UI State ---
        private EditorLayer _activeLayer = EditorLayer.Tiles;
        public EditorLayer ActiveLayer
        {
            get => _activeLayer;
            set { _activeLayer = value; OnPropertyChanged(nameof(ActiveLayer)); }
        }

        private int _editorWidth = 8;
        public int EditorWidth
        {
            get => _editorWidth;
            set { _editorWidth = value; OnPropertyChanged(nameof(EditorWidth)); }
        }

        private int _editorHeight = 8;
        public int EditorHeight
        {
            get => _editorHeight;
            set { _editorHeight = value; OnPropertyChanged(nameof(EditorHeight)); }
        }

        private TileType _selectedType = TileType.Red;
        public TileType SelectedType
        {
            get => _selectedType;
            set
            {
                if (_selectedType != value)
                {
                    _selectedType = value;
                    OnPropertyChanged(nameof(SelectedType));
                }
            }
        }

        private BombType _selectedBomb = BombType.None;
        public BombType SelectedBomb
        {
            get => _selectedBomb;
            set
            {
                if (_selectedBomb != value)
                {
                    _selectedBomb = value;
                    OnPropertyChanged(nameof(SelectedBomb));
                }
            }
        }

        private GroundType _selectedGround = GroundType.None;
        public GroundType SelectedGround
        {
            get => _selectedGround;
            set { _selectedGround = value; OnPropertyChanged(nameof(SelectedGround)); }
        }

        private CoverType _selectedCover = CoverType.None;
        public CoverType SelectedCover
        {
            get => _selectedCover;
            set { _selectedCover = value; OnPropertyChanged(nameof(SelectedCover)); }
        }

        private bool _assertColor = true;
        public bool AssertColor
        {
            get => _assertColor;
            set { _assertColor = value; OnPropertyChanged(nameof(AssertColor)); }
        }

        private bool _assertBomb = true;
        public bool AssertBomb
        {
            get => _assertBomb;
            set { _assertBomb = value; OnPropertyChanged(nameof(AssertBomb)); }
        }

        private string _jsonOutput = "";
        public string JsonOutput
        {
            get => _jsonOutput;
            set { _jsonOutput = value; OnPropertyChanged(nameof(JsonOutput)); }
        }

        private bool _isAssertionMode;
        public bool IsAssertionMode
        {
            get => _isAssertionMode;
            set { _isAssertionMode = value; OnPropertyChanged(nameof(IsAssertionMode)); }
        }

        // --- Tab State ---
        private int _activeTabIndex = 0;
        public int ActiveTabIndex
        {
            get => _activeTabIndex;
            set
            {
                if (_activeTabIndex != value)
                {
                    _activeTabIndex = value;
                    OnPropertyChanged(nameof(ActiveTabIndex));
                }
            }
        }

        // --- Level Analysis State (Delegated to LevelAnalysisManager) ---
        public bool IsAnalyzing => _analysisManager.IsAnalyzing;
        public float AnalysisProgress => _analysisManager.AnalysisProgress;
        public string AnalysisProgressText => _analysisManager.AnalysisProgressText;
        public float WinRate => _analysisManager.WinRate;
        public float DeadlockRate => _analysisManager.DeadlockRate;
        public string DifficultyText => _analysisManager.DifficultyText;

        public bool IsDeepAnalysis
        {
            get => _analysisManager.IsDeepAnalysis;
            set => _analysisManager.IsDeepAnalysis = value;
        }

        public DeepAnalysisResult? DeepResult => _analysisManager.DeepResult;
        public LevelAnalysisSnapshot? CurrentAnalysisSnapshot => _analysisManager.CurrentAnalysisSnapshot;

        // --- File Browser State (Scenario) - Delegated to ScenarioFileManager ---
        public ScenarioFolderNode? RootFolderNode => _scenarioFileManager.RootFolderNode;
        public List<ScenarioFileEntry> SearchResults => _scenarioFileManager.SearchResults;
        public string CurrentFilePath
        {
            get => _scenarioFileManager.CurrentFilePath;
            set => _scenarioFileManager.CurrentFilePath = value;
        }
        public HashSet<string> ExpandedPaths => _scenarioFileManager.ExpandedPaths;

        public void SetRootFolder(ScenarioFolderNode root) => _scenarioFileManager.SetRootFolder(root);

        // --- File Browser State (Level) - Delegated to LevelFileManager ---
        public ScenarioFolderNode? RootLevelFolderNode => _levelFileManager.RootLevelFolderNode;
        public string CurrentLevelFilePath
        {
            get => _levelFileManager.CurrentLevelFilePath;
            set => _levelFileManager.CurrentLevelFilePath = value;
        }
        public HashSet<string> LevelExpandedPaths => _levelFileManager.LevelExpandedPaths;

        public void SetRootLevelFolder(ScenarioFolderNode root) => _levelFileManager.SetRootLevelFolder(root);

        // --- Computed Properties ---
        public LevelConfig ActiveLevelConfig => _session.ActiveLevelConfig;
        public BombType[] ActiveBombs => ActiveLevelConfig.Bombs;

        private static readonly TileType[] _tilePaletteTypes =
        {
            TileType.Red,
            TileType.Green,
            TileType.Blue,
            TileType.Yellow,
            TileType.Purple,
            TileType.Orange,
            TileType.Rainbow,
            TileType.None
        };

        private static readonly GroundType[] _groundPaletteTypes = (GroundType[])Enum.GetValues(typeof(GroundType));
        private static readonly CoverType[] _coverPaletteTypes = (CoverType[])Enum.GetValues(typeof(CoverType));
        private static readonly BombType[] _bombPaletteTypes = (BombType[])Enum.GetValues(typeof(BombType));

        public static IReadOnlyList<TileType> TilePaletteTypes => _tilePaletteTypes;
        public static IReadOnlyList<GroundType> GroundPaletteTypes => _groundPaletteTypes;
        public static IReadOnlyList<CoverType> CoverPaletteTypes => _coverPaletteTypes;
        public static IReadOnlyList<BombType> BombPaletteTypes => _bombPaletteTypes;

        public static string GetGroundName(GroundType g) => g.ToString();
        public static string GetCoverName(CoverType c) => c.ToString();

        // --- Objective Editing (Delegated to ObjectiveEditorHelper) ---
        public const int MaxObjectives = ObjectiveEditorHelper.MaxObjectives;

        public static ObjectiveTargetLayer[] ObjectiveTargetLayers { get; } =
            (ObjectiveTargetLayer[])Enum.GetValues(typeof(ObjectiveTargetLayer));

        public LevelObjective[] Objectives => ActiveLevelConfig.Objectives;

        public int ActiveObjectiveCount => ObjectiveEditorHelper.GetActiveObjectiveCount(Objectives);

        public void AddObjective()
        {
            if (ObjectiveEditorHelper.TryAddObjective(ActiveLevelConfig.Objectives))
            {
                _session.IsDirty = true;
                OnPropertyChanged(nameof(Objectives));
                OnPropertyChanged(nameof(ActiveObjectiveCount));
            }
        }

        public void RemoveObjective(int index)
        {
            if (ObjectiveEditorHelper.TryRemoveObjective(ActiveLevelConfig.Objectives, index))
            {
                _session.IsDirty = true;
                OnPropertyChanged(nameof(Objectives));
                OnPropertyChanged(nameof(ActiveObjectiveCount));
            }
        }

        public void SetObjectiveLayer(int index, ObjectiveTargetLayer layer)
        {
            if (ObjectiveEditorHelper.TrySetObjectiveLayer(ActiveLevelConfig.Objectives, index, layer))
            {
                _session.IsDirty = true;
                OnPropertyChanged(nameof(Objectives));
            }
        }

        public void SetObjectiveElementType(int index, int elementType)
        {
            if (ObjectiveEditorHelper.TrySetObjectiveElementType(ActiveLevelConfig.Objectives, index, elementType))
            {
                _session.IsDirty = true;
                OnPropertyChanged(nameof(Objectives));
            }
        }

        public void SetObjectiveTargetCount(int index, int count)
        {
            if (ObjectiveEditorHelper.TrySetObjectiveTargetCount(ActiveLevelConfig.Objectives, index, count))
            {
                _session.IsDirty = true;
                OnPropertyChanged(nameof(Objectives));
            }
        }

        public static IReadOnlyList<(int Value, string Name)> GetElementTypesForLayer(ObjectiveTargetLayer layer) =>
            ObjectiveEditorHelper.GetElementTypesForLayer(layer);

        public static string GetElementTypeName(ObjectiveTargetLayer layer, int elementType) =>
            ObjectiveEditorHelper.GetElementTypeName(layer, elementType);

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? OnRequestRepaint;

        public LevelEditorViewModel(
            IPlatformService platform,
            IJsonService jsonService,
            IScenarioService scenarioService,
            ILevelService levelService,
            IFileSystemService fileSystem)
        {
            _platform = platform;
            _jsonService = jsonService;

            _session = new EditorSession();
            _gridManipulator = new GridManipulator();

            _session.PropertyChanged += OnSessionPropertyChanged;

            // Initialize Level File Manager (first, as Analysis Manager depends on it)
            _levelFileManager = new LevelFileManager(
                platform,
                levelService,
                fileSystem,
                () => _session.IsDirty,
                dirty => _session.IsDirty = dirty,
                () => { ExportJson(); return JsonOutput; },
                (json, keepScenarioMode) => { JsonOutput = json; ImportJson(keepScenarioMode); });
            _levelFileManager.PropertyChanged += OnManagerPropertyChanged;

            // Initialize Analysis Manager
            _analysisManager = new LevelAnalysisManager(
                levelService,
                fileSystem,
                () => ActiveLevelConfig,
                () => _levelFileManager.CurrentLevelFilePath);
            _analysisManager.PropertyChanged += OnManagerPropertyChanged;

            // Initialize Scenario File Manager
            _scenarioFileManager = new ScenarioFileManager(
                platform,
                scenarioService,
                fileSystem,
                () => _session.IsDirty,
                dirty => _session.IsDirty = dirty,
                () => _session.ScenarioName,
                name => _session.ScenarioName = name,
                () => { ExportJson(); return JsonOutput; },
                (json, keepScenarioMode) => { JsonOutput = json; ImportJson(keepScenarioMode); });
            _scenarioFileManager.PropertyChanged += OnManagerPropertyChanged;

            // Initialize default state
            _session.EnsureDefaultLevel();
            GenerateRandomLevel();
        }

        public void Dispose()
        {
            _analysisManager.Dispose();
            _session.PropertyChanged -= OnSessionPropertyChanged;
            _analysisManager.PropertyChanged -= OnManagerPropertyChanged;
            _levelFileManager.PropertyChanged -= OnManagerPropertyChanged;
            _scenarioFileManager.PropertyChanged -= OnManagerPropertyChanged;
        }

        private void OnSessionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
        }

        private void OnManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
        }

        protected void OnPropertyChanged(string? name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected void RequestRepaint() => OnRequestRepaint?.Invoke();

        public void NotifyGridChanged()
        {
            RequestRepaint();
            RestartAnalysis();
        }

        // --- Level Analysis (Delegated to LevelAnalysisManager) ---
        public void LoadCachedAnalysis() => _analysisManager.LoadCachedAnalysis();
        public void RestartAnalysis() => _analysisManager.RestartAnalysis();

        // --- Actions ---

        public void SetScenarioName(string name)
        {
            _session.ScenarioName = name;
        }

        public void SwitchMode(EditorMode mode)
        {
            _session.CurrentMode = mode;
            if (mode == EditorMode.Scenario)
            {
                EditorWidth = _session.CurrentScenario.InitialState.Width;
                EditorHeight = _session.CurrentScenario.InitialState.Height;
                if (_session.CurrentScenario.InitialState.Grid.All(t => t == TileType.None))
                {
                    GenerateRandomLevel();
                }
            }
            else
            {
                _session.EnsureDefaultLevel();
                EditorWidth = _session.CurrentLevel.Width;
                EditorHeight = _session.CurrentLevel.Height;
            }
            IsAssertionMode = false;
        }

        public void GenerateRandomLevel()
        {
            _gridManipulator.GenerateRandomLevel(_session.ActiveLevelConfig, Environment.TickCount);
            RequestRepaint();
            _session.IsDirty = true;
            RestartAnalysis();
        }

        public void ResizeGrid()
        {
            var newConfig = _gridManipulator.ResizeGrid(_session.ActiveLevelConfig, EditorWidth, EditorHeight);

            if (CurrentMode == EditorMode.Level)
            {
                _session.CurrentLevel = newConfig;
            }
            else
            {
                _session.CurrentScenario.InitialState = newConfig;
                _session.CurrentScenario.Operations.Clear();
                _session.CurrentScenario.ExpectedState = new LevelConfig();
            }
            RequestRepaint();
            _session.IsDirty = true;
            RestartAnalysis();
        }

        public void ToggleAssertionMode()
        {
            IsAssertionMode = !IsAssertionMode;
        }

        public void HandleGridClick(int index)
        {
            if (IsAssertionMode)
            {
                var w = ActiveLevelConfig.Width;
                var x = index % w;
                var y = index / w;

                var existing = _session.CurrentScenario.Assertions.FirstOrDefault(a => a.X == x && a.Y == y);
                if (existing != null)
                {
                    _session.CurrentScenario.Assertions.Remove(existing);
                }
                else
                {
                    var type = AssertColor ? SelectedType : (TileType?)null;
                    var bomb = AssertBomb ? SelectedBomb : (BombType?)null;

                    _session.CurrentScenario.Assertions.Add(new ScenarioAssertion
                    {
                        X = x,
                        Y = y,
                        Type = type,
                        Bomb = bomb
                    });
                }
                _session.IsDirty = true;
                RequestRepaint();
                return;
            }

            PaintAt(index);
        }

        public void PaintAt(int index)
        {
            switch (ActiveLayer)
            {
                case EditorLayer.Tiles:
                    _gridManipulator.PaintTile(_session.ActiveLevelConfig, index, SelectedType, SelectedBomb);
                    break;
                case EditorLayer.Covers:
                    if (SelectedCover == CoverType.None)
                        _gridManipulator.ClearCover(_session.ActiveLevelConfig, index);
                    else
                        _gridManipulator.PaintCover(_session.ActiveLevelConfig, index, SelectedCover);
                    break;
                case EditorLayer.Grounds:
                    if (SelectedGround == GroundType.None)
                        _gridManipulator.ClearGround(_session.ActiveLevelConfig, index);
                    else
                        _gridManipulator.PaintGround(_session.ActiveLevelConfig, index, SelectedGround);
                    break;
            }
            RequestRepaint();
            _session.IsDirty = true;
            RestartAnalysis();
        }

        public void PaintTile(int index) => PaintAt(index);

        // --- IO & Export ---

        public void ExportJson()
        {
            if (CurrentMode == EditorMode.Level)
                JsonOutput = _jsonService.Serialize(_session.CurrentLevel);
            else
                JsonOutput = _jsonService.Serialize(_session.CurrentScenario);
        }

        public void ImportJson(bool keepScenarioMode = false)
        {
            if (string.IsNullOrWhiteSpace(JsonOutput)) return;
            try
            {
                if (JsonOutput.Contains("Operations"))
                {
                    _session.CurrentScenario = _jsonService.Deserialize<ScenarioConfig>(JsonOutput);
                    _session.CurrentMode = EditorMode.Scenario;
                    EditorWidth = _session.CurrentScenario.InitialState.Width;
                    EditorHeight = _session.CurrentScenario.InitialState.Height;
                }
                else
                {
                    var level = _jsonService.Deserialize<LevelConfig>(JsonOutput);

                    if (keepScenarioMode || CurrentMode == EditorMode.Scenario)
                    {
                        _session.CurrentScenario = new ScenarioConfig
                        {
                            InitialState = level,
                            Operations = new List<MoveOperation>()
                        };
                        _session.CurrentMode = EditorMode.Scenario;
                        EditorWidth = level.Width;
                        EditorHeight = level.Height;
                    }
                    else
                    {
                        _session.CurrentLevel = level;
                        _session.CurrentMode = EditorMode.Level;
                        EditorWidth = _session.CurrentLevel.Width;
                        EditorHeight = _session.CurrentLevel.Height;
                    }
                }

                _session.EnsureDefaultLevel();
                RequestRepaint();
                _session.IsDirty = false;
                LoadCachedAnalysis();
            }
            catch (Exception ex)
            {
                _ = _platform.ShowAlertAsync($"Import Error: {ex.Message}");
            }
        }

        // --- Scenario Management (Delegated to ScenarioFileManager) ---
        public void RefreshScenarioList() => _scenarioFileManager.RefreshScenarioList();
        public Task LoadScenarioAsync(string path) => _scenarioFileManager.LoadScenarioAsync(path);
        public Task SaveScenarioAsync() => _scenarioFileManager.SaveScenarioAsync();
        public Task CreateNewScenarioAsync(string folderPath) => _scenarioFileManager.CreateNewScenarioAsync(folderPath);
        public Task CreateNewFolderAsync(string parentPath) => _scenarioFileManager.CreateNewFolderAsync(parentPath);
        public Task DuplicateScenarioAsync(string path) => _scenarioFileManager.DuplicateScenarioAsync(path);
        public Task DeleteFileAsync(string path, bool isFolder) => _scenarioFileManager.DeleteFileAsync(path, isFolder);

        // --- Level File Management (Delegated to LevelFileManager) ---
        public void RefreshLevelList() => _levelFileManager.RefreshLevelList();
        public Task LoadLevelAsync(string path) => _levelFileManager.LoadLevelAsync(path, LoadCachedAnalysis);
        public Task SaveLevelAsync() => _levelFileManager.SaveLevelAsync();
        public Task CreateNewLevelAsync(string folderPath) => _levelFileManager.CreateNewLevelAsync(folderPath);
        public Task CreateNewLevelFolderAsync(string parentPath) => _levelFileManager.CreateNewLevelFolderAsync(parentPath);
        public Task DuplicateLevelAsync(string path) => _levelFileManager.DuplicateLevelAsync(path);
        public Task DeleteLevelFileAsync(string path, bool isFolder) => _levelFileManager.DeleteLevelFileAsync(path, isFolder);
    }
}
