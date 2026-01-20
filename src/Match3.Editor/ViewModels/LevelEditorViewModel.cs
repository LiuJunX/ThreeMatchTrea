using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Match3.Core.Analysis;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Scenarios;
using Match3.Editor.Interfaces;
using Match3.Editor.Logic;
using Match3.Editor.Helpers;

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
        private readonly IScenarioService _scenarioService;
        private readonly ILevelService _levelService;
        private readonly IFileSystemService _fileSystem;

        private readonly EditorSession _session;
        private readonly GridManipulator _gridManipulator;

        // --- Level Analysis ---
        private readonly ILevelAnalysisService _analysisService;
        private CancellationTokenSource? _analysisCts;

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

        // --- Level Analysis State ---
        private bool _isAnalyzing;
        public bool IsAnalyzing
        {
            get => _isAnalyzing;
            private set { _isAnalyzing = value; OnPropertyChanged(nameof(IsAnalyzing)); }
        }

        private float _analysisProgress;
        public float AnalysisProgress
        {
            get => _analysisProgress;
            private set { _analysisProgress = value; OnPropertyChanged(nameof(AnalysisProgress)); }
        }

        private string _analysisProgressText = "";
        public string AnalysisProgressText
        {
            get => _analysisProgressText;
            private set { _analysisProgressText = value; OnPropertyChanged(nameof(AnalysisProgressText)); }
        }

        private float _winRate;
        public float WinRate
        {
            get => _winRate;
            private set { _winRate = value; OnPropertyChanged(nameof(WinRate)); }
        }

        private float _deadlockRate;
        public float DeadlockRate
        {
            get => _deadlockRate;
            private set { _deadlockRate = value; OnPropertyChanged(nameof(DeadlockRate)); }
        }

        private string _difficultyText = "";
        public string DifficultyText
        {
            get => _difficultyText;
            private set { _difficultyText = value; OnPropertyChanged(nameof(DifficultyText)); }
        }

        // --- File Browser State (Scenario) ---
        public ScenarioFolderNode? RootFolderNode { get; private set; }
        public List<ScenarioFileEntry> SearchResults { get; private set; } = new List<ScenarioFileEntry>();
        public string CurrentFilePath { get; set; } = "";
        public HashSet<string> ExpandedPaths { get; } = new HashSet<string>();

        public void SetRootFolder(ScenarioFolderNode root)
        {
            RootFolderNode = root;
            OnPropertyChanged(nameof(RootFolderNode));
        }

        // --- File Browser State (Level) ---
        public ScenarioFolderNode? RootLevelFolderNode { get; private set; }
        public string CurrentLevelFilePath { get; set; } = "";
        public HashSet<string> LevelExpandedPaths { get; } = new HashSet<string>();

        public void SetRootLevelFolder(ScenarioFolderNode root)
        {
            RootLevelFolderNode = root;
            OnPropertyChanged(nameof(RootLevelFolderNode));
        }

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

        // --- Objective Editing ---
        public const int MaxObjectives = 4;

        public static ObjectiveTargetLayer[] ObjectiveTargetLayers { get; } =
            (ObjectiveTargetLayer[])Enum.GetValues(typeof(ObjectiveTargetLayer));

        /// <summary>
        /// Gets the objectives array from the active level config.
        /// </summary>
        public LevelObjective[] Objectives => ActiveLevelConfig.Objectives;

        /// <summary>
        /// Gets the count of active objectives (non-None layer).
        /// </summary>
        public int ActiveObjectiveCount
        {
            get
            {
                int count = 0;
                foreach (var obj in Objectives)
                {
                    if (obj.TargetLayer != ObjectiveTargetLayer.None) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// Adds a new objective if under the maximum limit.
        /// </summary>
        public void AddObjective()
        {
            var objectives = ActiveLevelConfig.Objectives;

            // Find first empty slot
            for (int i = 0; i < objectives.Length; i++)
            {
                if (objectives[i].TargetLayer == ObjectiveTargetLayer.None)
                {
                    objectives[i] = new LevelObjective
                    {
                        TargetLayer = ObjectiveTargetLayer.Tile,
                        ElementType = (int)TileType.Red,
                        TargetCount = 10
                    };
                    _session.IsDirty = true;
                    OnPropertyChanged(nameof(Objectives));
                    OnPropertyChanged(nameof(ActiveObjectiveCount));
                    return;
                }
            }
        }

        /// <summary>
        /// Removes an objective at the specified index.
        /// </summary>
        public void RemoveObjective(int index)
        {
            var objectives = ActiveLevelConfig.Objectives;
            if (index < 0 || index >= objectives.Length) return;

            objectives[index] = new LevelObjective { TargetLayer = ObjectiveTargetLayer.None };
            _session.IsDirty = true;
            OnPropertyChanged(nameof(Objectives));
            OnPropertyChanged(nameof(ActiveObjectiveCount));
        }

        /// <summary>
        /// Updates an objective's target layer.
        /// </summary>
        public void SetObjectiveLayer(int index, ObjectiveTargetLayer layer)
        {
            var objectives = ActiveLevelConfig.Objectives;
            if (index < 0 || index >= objectives.Length) return;

            var obj = objectives[index];
            obj.TargetLayer = layer;

            // Reset element type to first valid value for the new layer
            obj.ElementType = layer switch
            {
                ObjectiveTargetLayer.Tile => (int)TileType.Red,
                ObjectiveTargetLayer.Cover => (int)CoverType.Cage,
                ObjectiveTargetLayer.Ground => (int)GroundType.Ice,
                _ => 0
            };

            objectives[index] = obj;
            _session.IsDirty = true;
            OnPropertyChanged(nameof(Objectives));
        }

        /// <summary>
        /// Updates an objective's element type.
        /// </summary>
        public void SetObjectiveElementType(int index, int elementType)
        {
            var objectives = ActiveLevelConfig.Objectives;
            if (index < 0 || index >= objectives.Length) return;

            var obj = objectives[index];
            obj.ElementType = elementType;
            objectives[index] = obj;
            _session.IsDirty = true;
            OnPropertyChanged(nameof(Objectives));
        }

        /// <summary>
        /// Updates an objective's target count.
        /// </summary>
        public void SetObjectiveTargetCount(int index, int count)
        {
            var objectives = ActiveLevelConfig.Objectives;
            if (index < 0 || index >= objectives.Length) return;

            var obj = objectives[index];
            obj.TargetCount = Math.Max(1, count);
            objectives[index] = obj;
            _session.IsDirty = true;
            OnPropertyChanged(nameof(Objectives));
        }

        /// <summary>
        /// Gets available element types for a given layer.
        /// </summary>
        public static IReadOnlyList<(int Value, string Name)> GetElementTypesForLayer(ObjectiveTargetLayer layer)
        {
            return layer switch
            {
                ObjectiveTargetLayer.Tile => new[]
                {
                    ((int)TileType.Red, "Red"),
                    ((int)TileType.Green, "Green"),
                    ((int)TileType.Blue, "Blue"),
                    ((int)TileType.Yellow, "Yellow"),
                    ((int)TileType.Purple, "Purple"),
                    ((int)TileType.Orange, "Orange"),
                },
                ObjectiveTargetLayer.Cover => new[]
                {
                    ((int)CoverType.Cage, "Cage"),
                    ((int)CoverType.Chain, "Chain"),
                    ((int)CoverType.Bubble, "Bubble"),
                },
                ObjectiveTargetLayer.Ground => new[]
                {
                    ((int)GroundType.Ice, "Ice"),
                },
                _ => Array.Empty<(int, string)>()
            };
        }

        /// <summary>
        /// Gets the display name for an element type within a layer.
        /// </summary>
        public static string GetElementTypeName(ObjectiveTargetLayer layer, int elementType)
        {
            return layer switch
            {
                ObjectiveTargetLayer.Tile => ((TileType)elementType).ToString(),
                ObjectiveTargetLayer.Cover => ((CoverType)elementType).ToString(),
                ObjectiveTargetLayer.Ground => ((GroundType)elementType).ToString(),
                _ => "Unknown"
            };
        }

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
            _scenarioService = scenarioService;
            _levelService = levelService;
            _fileSystem = fileSystem;

            _session = new EditorSession();
            _gridManipulator = new GridManipulator();
            _analysisService = new LevelAnalysisService();

            _session.PropertyChanged += OnSessionPropertyChanged;

            // Initialize default state
            _session.EnsureDefaultLevel();
            GenerateRandomLevel();
        }

        public void Dispose()
        {
            _analysisCts?.Cancel();
            _analysisCts?.Dispose();
            _session.PropertyChanged -= OnSessionPropertyChanged;
        }

        private void OnSessionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
        }

        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        protected void RequestRepaint() => OnRequestRepaint?.Invoke();

        // --- Level Analysis ---

        /// <summary>
        /// 从缓存加载分析数据（如果有）
        /// </summary>
        public void LoadCachedAnalysis()
        {
            var cache = ActiveLevelConfig.AnalysisCache;
            if (cache != null)
            {
                WinRate = cache.WinRate;
                DeadlockRate = cache.DeadlockRate;
                DifficultyText = $"{cache.Difficulty} ({cache.WinRate:P0})";
            }
            else
            {
                WinRate = 0;
                DeadlockRate = 0;
                DifficultyText = "未分析";
            }
        }

        /// <summary>
        /// 重新开始关卡分析（取消之前的分析）
        /// </summary>
        public void RestartAnalysis()
        {
            // 取消之前的分析
            _analysisCts?.Cancel();
            _analysisCts?.Dispose();
            _analysisCts = new CancellationTokenSource();

            // 先显示缓存数据（如果有）
            var cache = ActiveLevelConfig.AnalysisCache;
            if (cache != null)
            {
                WinRate = cache.WinRate;
                DeadlockRate = cache.DeadlockRate;
                DifficultyText = $"{cache.Difficulty} (重新分析中...)";
            }
            else
            {
                WinRate = 0;
                DeadlockRate = 0;
                DifficultyText = "分析中...";
            }

            IsAnalyzing = true;
            AnalysisProgress = 0;

            _ = RunAnalysisAsync(_analysisCts.Token);
        }

        private async Task RunAnalysisAsync(CancellationToken token)
        {
            var progress = new Progress<SimulationProgress>(p =>
            {
                AnalysisProgress = p.Progress;
                AnalysisProgressText = $"{p.CompletedCount} / {p.TotalCount}";
                WinRate = p.WinRate;
                DeadlockRate = p.DeadlockRate;
                DifficultyText = $"通过率: {p.WinRate:P0}";
            });

            try
            {
                var result = await _analysisService.AnalyzeAsync(
                    ActiveLevelConfig,
                    new AnalysisConfig { SimulationCount = 500, ProgressReportInterval = 10 },
                    progress,
                    token);

                if (!result.WasCancelled)
                {
                    WinRate = result.WinRate;
                    DeadlockRate = result.DeadlockRate;
                    DifficultyText = $"{GetDifficultyName(result.DifficultyRating)} ({result.WinRate:P0})";

                    // 保存分析结果到缓存
                    ActiveLevelConfig.AnalysisCache = new LevelAnalysisCacheData
                    {
                        WinRate = result.WinRate,
                        DeadlockRate = result.DeadlockRate,
                        AverageMovesUsed = result.AverageMovesUsed,
                        Difficulty = result.DifficultyRating.ToString(),
                        SimulationCount = result.TotalSimulations,
                        AnalyzedAt = DateTime.Now
                    };
                }
            }
            catch (OperationCanceledException)
            {
                // 被取消，忽略
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        private static string GetDifficultyName(DifficultyRating rating) => rating switch
        {
            DifficultyRating.VeryEasy => "非常简单",
            DifficultyRating.Easy => "简单",
            DifficultyRating.Medium => "中等",
            DifficultyRating.Hard => "困难",
            DifficultyRating.VeryHard => "非常困难",
            _ => "未知"
        };

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
                        X = x, Y = y,
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
                LoadCachedAnalysis();  // 加载缓存，不重新分析
            }
            catch (Exception ex)
            {
                _ = _platform.ShowAlertAsync($"Import Error: {ex.Message}");
            }
        }

        // --- Scenario Management ---

        public void RefreshScenarioList()
        {
            RootFolderNode = _scenarioService.BuildTree();
        }

        public async Task LoadScenarioAsync(string path)
        {
            if (IsDirty)
            {
                var confirm = await _platform.ConfirmAsync("Unsaved Changes", "You have unsaved changes. Do you want to save them before switching?");
                if (confirm)
                {
                    if (!string.IsNullOrEmpty(CurrentFilePath))
                    {
                        await SaveScenarioAsync();
                    }
                    else
                    {
                        var discard = await _platform.ConfirmAsync("Cannot Save", "File has no path. Discard changes?");
                        if (!discard) return;
                    }
                }
                else
                {
                    var discard = await _platform.ConfirmAsync("Discard Changes?", "Are you sure you want to discard unsaved changes?");
                    if (!discard) return;
                }
            }

            try
            {
                var json = _scenarioService.ReadScenarioJson(path);
                JsonOutput = json;
                ImportJson(keepScenarioMode: true);
                CurrentFilePath = path;
                SetScenarioName(_fileSystem.GetFileNameWithoutExtension(path));
            }
            catch(Exception ex)
            {
                await _platform.ShowAlertAsync("Error", "Failed to load file: " + ex.Message);
            }
        }

        public async Task SaveScenarioAsync()
        {
            if (string.IsNullOrEmpty(CurrentFilePath)) return;

            try
            {
                var currentName = _fileSystem.GetFileNameWithoutExtension(CurrentFilePath);
                if (!string.Equals(currentName, ScenarioName, StringComparison.Ordinal))
                {
                     _scenarioService.RenameScenario(CurrentFilePath, ScenarioName);

                     var stem = ScenarioFileName.SanitizeFileStem(ScenarioName);
                     SetScenarioName(stem);

                     var dir = _fileSystem.GetDirectoryName(CurrentFilePath);
                     var newPath = string.IsNullOrEmpty(dir)
                         ? stem + ".json"
                         : _fileSystem.CombinePath(dir, stem + ".json");
                     CurrentFilePath = _fileSystem.NormalizePath(newPath);
                }

                ExportJson();
                _scenarioService.WriteScenarioJson(CurrentFilePath, JsonOutput);
                _session.IsDirty = false;
                RefreshScenarioList();
            }
            catch (Exception ex)
            {
                await _platform.ShowAlertAsync("Error", "Failed to save: " + ex.Message);
            }
        }

        public async Task CreateNewScenarioAsync(string folderPath)
        {
            try 
            {
                var newPath = _scenarioService.CreateNewScenario(folderPath, "New Scenario", "{}");
                RefreshScenarioList();
            }
            catch(Exception ex) 
            { 
                await _platform.ShowAlertAsync("Error", "Failed to create scenario: " + ex.Message);
            }
        }

        public async Task CreateNewFolderAsync(string parentPath)
        {
            try 
            {
                _scenarioService.CreateFolder(parentPath, "New Folder");
                RefreshScenarioList();
            }
            catch(Exception ex) 
            { 
                await _platform.ShowAlertAsync("Error", "Failed to create folder: " + ex.Message);
            }
        }

        public async Task DuplicateScenarioAsync(string path)
        {
            try
            {
                _scenarioService.DuplicateScenario(path, _fileSystem.GetFileNameWithoutExtension(path) + "_Copy");
                RefreshScenarioList();
            }
            catch(Exception ex) 
            { 
                await _platform.ShowAlertAsync("Error", "Failed to duplicate: " + ex.Message);
            }
        }

        public async Task DeleteFileAsync(string path, bool isFolder)
        {
            var confirm = await _platform.ConfirmAsync("Delete", "Are you sure you want to delete '" + _fileSystem.GetFileName(path) + "'?");
            if (!confirm) return;

            try
            {
                if (isFolder)
                {
                    _scenarioService.DeleteFolder(path);
                }
                else
                {
                    _scenarioService.DeleteScenario(path);
                }
                RefreshScenarioList();
            }
            catch(Exception ex)
            {
                await _platform.ShowAlertAsync("Error", "Failed to delete: " + ex.Message);
            }
        }

        // --- Level File Management ---

        public void RefreshLevelList()
        {
            RootLevelFolderNode = _levelService.BuildTree();
            OnPropertyChanged(nameof(RootLevelFolderNode));
        }

        public async Task LoadLevelAsync(string path)
        {
            if (IsDirty)
            {
                var confirm = await _platform.ConfirmAsync("Unsaved Changes", "You have unsaved changes. Do you want to save them before switching?");
                if (confirm)
                {
                    if (!string.IsNullOrEmpty(CurrentLevelFilePath))
                    {
                        await SaveLevelAsync();
                    }
                    else
                    {
                        var discard = await _platform.ConfirmAsync("Cannot Save", "File has no path. Discard changes?");
                        if (!discard) return;
                    }
                }
                else
                {
                    var discard = await _platform.ConfirmAsync("Discard Changes?", "Are you sure you want to discard unsaved changes?");
                    if (!discard) return;
                }
            }

            try
            {
                var json = _levelService.ReadLevelJson(path);
                JsonOutput = json;
                ImportJson(keepScenarioMode: false);
                CurrentLevelFilePath = path;
                _session.IsDirty = false;
            }
            catch (Exception ex)
            {
                await _platform.ShowAlertAsync("Error", "Failed to load level: " + ex.Message);
            }
        }

        public async Task SaveLevelAsync()
        {
            if (string.IsNullOrEmpty(CurrentLevelFilePath)) return;

            try
            {
                ExportJson();
                _levelService.WriteLevelJson(CurrentLevelFilePath, JsonOutput);
                _session.IsDirty = false;
                RefreshLevelList();
            }
            catch (Exception ex)
            {
                await _platform.ShowAlertAsync("Error", "Failed to save level: " + ex.Message);
            }
        }

        public async Task CreateNewLevelAsync(string folderPath)
        {
            try
            {
                ExportJson();
                var newPath = _levelService.CreateNewLevel(folderPath, "New Level", JsonOutput);
                RefreshLevelList();
                CurrentLevelFilePath = newPath;
            }
            catch (Exception ex)
            {
                await _platform.ShowAlertAsync("Error", "Failed to create level: " + ex.Message);
            }
        }

        public async Task CreateNewLevelFolderAsync(string parentPath)
        {
            try
            {
                _levelService.CreateFolder(parentPath, "New Folder");
                RefreshLevelList();
            }
            catch (Exception ex)
            {
                await _platform.ShowAlertAsync("Error", "Failed to create folder: " + ex.Message);
            }
        }

        public async Task DuplicateLevelAsync(string path)
        {
            try
            {
                _levelService.DuplicateLevel(path, _fileSystem.GetFileNameWithoutExtension(path) + "_Copy");
                RefreshLevelList();
            }
            catch (Exception ex)
            {
                await _platform.ShowAlertAsync("Error", "Failed to duplicate level: " + ex.Message);
            }
        }

        public async Task DeleteLevelFileAsync(string path, bool isFolder)
        {
            var confirm = await _platform.ConfirmAsync("Delete", "Are you sure you want to delete '" + _fileSystem.GetFileName(path) + "'?");
            if (!confirm) return;

            try
            {
                if (isFolder)
                {
                    _levelService.DeleteFolder(path);
                }
                else
                {
                    _levelService.DeleteLevel(path);
                }
                RefreshLevelList();
            }
            catch (Exception ex)
            {
                await _platform.ShowAlertAsync("Error", "Failed to delete: " + ex.Message);
            }
        }
    }
}
