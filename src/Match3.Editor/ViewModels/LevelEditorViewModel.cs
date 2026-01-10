using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Match3.Core;
using Match3.Core.Config;
using Match3.Core.Interfaces;
using Match3.Core.Scenarios;
using Match3.Core.Systems;
using Match3.Editor.Interfaces;
using Match3.Random;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Utility;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Gravity;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;

namespace Match3.Editor.ViewModels
{
    public class LevelEditorViewModel : INotifyPropertyChanged
    {
        private readonly IPlatformService _platform;
        private readonly IFileSystemService _fileSystem;
        private readonly IJsonService _jsonService;
        private readonly IGameLogger _logger;
        private readonly IScenarioService _scenarioService;

        // --- Core State ---
        public enum EditorMode { Level, Scenario }
        private EditorMode _currentMode = EditorMode.Level;
        public EditorMode CurrentMode
        {
            get => _currentMode;
            set { _currentMode = value; OnPropertyChanged(nameof(CurrentMode)); OnPropertyChanged(nameof(ActiveLevelConfig)); }
        }

        // --- Data Models ---
        public LevelConfig CurrentLevel { get; set; } = new LevelConfig();
        
        private ScenarioConfig _currentScenario = new ScenarioConfig();
        public ScenarioConfig CurrentScenario
        {
            get => _currentScenario;
            set
            {
                _currentScenario = value;
                OnPropertyChanged(nameof(CurrentScenario));
                OnPropertyChanged(nameof(ScenarioDescription));
                OnPropertyChanged(nameof(ActiveLevelConfig));
            }
        }

        public ScenarioMetadata CurrentScenarioMetadata { get; set; } = new ScenarioMetadata();

        private string _scenarioName = "New Scenario";
        public string ScenarioName
        {
            get => _scenarioName;
            set
            {
                if (_scenarioName != value)
                {
                    _scenarioName = value;
                    OnPropertyChanged(nameof(ScenarioName));
                    IsDirty = true;
                }
            }
        }

        public void SetScenarioName(string name)
        {
            if (_scenarioName != name)
            {
                _scenarioName = name;
                OnPropertyChanged(nameof(ScenarioName));
            }
        }

        public string ScenarioDescription
        {
            get => CurrentScenario.Description;
            set
            {
                if (CurrentScenario.Description != value)
                {
                    CurrentScenario.Description = value;
                    OnPropertyChanged(nameof(ScenarioDescription));
                    IsDirty = true;
                }
            }
        }
        
        // --- Editor UI State ---
        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            set { _isDirty = value; OnPropertyChanged(nameof(IsDirty)); }
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

        private bool _isRecording;
        public bool IsRecording
        {
            get => _isRecording;
            set { _isRecording = value; OnPropertyChanged(nameof(IsRecording)); }
        }

        private bool _isAssertionMode;
        public bool IsAssertionMode
        {
            get => _isAssertionMode;
            set { _isAssertionMode = value; OnPropertyChanged(nameof(IsAssertionMode)); }
        }

        // --- File Browser State ---
        public ScenarioFolderNode? RootFolderNode { get; private set; }
        public List<ScenarioFileEntry> SearchResults { get; private set; } = new List<ScenarioFileEntry>();
        public string CurrentFilePath { get; set; } = "";
        public HashSet<string> ExpandedPaths { get; } = new HashSet<string>();

        public void SetRootFolder(ScenarioFolderNode root)
        {
            RootFolderNode = root;
            OnPropertyChanged(nameof(RootFolderNode));
        }

        // --- Computed Properties ---
        public LevelConfig ActiveLevelConfig => CurrentMode == EditorMode.Level ? CurrentLevel : CurrentScenario.InitialState;
        
        public BombType[] ActiveBombs => ActiveLevelConfig.Bombs;

        // --- Simulation ---
        public Match3Engine? SimulationController { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? OnRequestRepaint;

        public LevelEditorViewModel(
            IPlatformService platform, 
            IFileSystemService fileSystem, 
            IJsonService jsonService,
            IGameLogger logger,
            IScenarioService scenarioService)
        {
            _platform = platform;
            _fileSystem = fileSystem;
            _jsonService = jsonService;
            _logger = logger;
            _scenarioService = scenarioService;
            
            EnsureDefaultLevel();
        }

        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        protected void RequestRepaint() => OnRequestRepaint?.Invoke();

        // --- Actions ---

        public void SwitchMode(EditorMode mode)
        {
            CurrentMode = mode;
            if (mode == EditorMode.Scenario)
            {
                EditorWidth = CurrentScenario.InitialState.Width;
                EditorHeight = CurrentScenario.InitialState.Height;
                if (CurrentScenario.InitialState.Grid.All(t => t == TileType.None))
                {
                    GenerateRandomLevel();
                }
            }
            else
            {
                EnsureDefaultLevel();
                EditorWidth = CurrentLevel.Width;
                EditorHeight = CurrentLevel.Height;
            }
            IsRecording = false;
            SimulationController = null;
        }

        public void EnsureDefaultLevel()
        {
            if (CurrentLevel.Grid == null || CurrentLevel.Grid.Length == 0)
            {
                CurrentLevel = new LevelConfig(8, 8);
                GenerateRandomLevel();
            }
            if (CurrentLevel.Bombs == null || CurrentLevel.Bombs.Length != CurrentLevel.Grid.Length)
            {
                CurrentLevel.Bombs = new BombType[CurrentLevel.Grid.Length];
            }
        }

        public void GenerateRandomLevel()
        {
            var config = ActiveLevelConfig;
            if (config.Bombs == null || config.Bombs.Length != config.Grid.Length)
            {
                config.Bombs = new BombType[config.Grid.Length];
            }

            var rng = new SeedManager(Environment.TickCount).GetRandom(RandomDomain.Refill);
            var types = new[] { TileType.Red, TileType.Green, TileType.Blue, TileType.Yellow, TileType.Purple, TileType.Orange };

            for (int i = 0; i < config.Grid.Length; i++)
            {
                config.Grid[i] = types[rng.Next(0, types.Length)];
            }
            
            Array.Clear(config.Bombs, 0, config.Bombs.Length);
            
            RequestRepaint();
            IsDirty = true;
        }

        public void ResizeGrid()
        {
            var oldConfig = ActiveLevelConfig;
            var newConfig = new LevelConfig(EditorWidth, EditorHeight);
            
            int w = Math.Min(oldConfig.Width, newConfig.Width);
            int h = Math.Min(oldConfig.Height, newConfig.Height);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int oldIdx = y * oldConfig.Width + x;
                    int newIdx = y * newConfig.Width + x;
                    if (oldIdx < oldConfig.Grid.Length && newIdx < newConfig.Grid.Length)
                    {
                        newConfig.Grid[newIdx] = oldConfig.Grid[oldIdx];
                        if (oldConfig.Bombs != null && oldIdx < oldConfig.Bombs.Length)
                        {
                            newConfig.Bombs[newIdx] = oldConfig.Bombs[oldIdx];
                        }
                    }
                }
            }

            if (CurrentMode == EditorMode.Level)
            {
                newConfig.MoveLimit = CurrentLevel.MoveLimit;
                CurrentLevel = newConfig;
            }
            else
            {
                CurrentScenario.InitialState = newConfig;
                CurrentScenario.Operations.Clear();
                CurrentScenario.ExpectedState = new LevelConfig();
            }
            RequestRepaint();
            IsDirty = true;
        }

        public void ToggleAssertionMode()
        {
            IsAssertionMode = !IsAssertionMode;
            if (IsAssertionMode)
            {
                IsRecording = false;
            }
        }

        public void HandleGridClick(int index)
        {
            if (IsAssertionMode)
            {
                var w = ActiveLevelConfig.Width;
                var x = index % w;
                var y = index / w;
                
                var existing = CurrentScenario.Assertions.FirstOrDefault(a => a.X == x && a.Y == y);
                if (existing != null)
                {
                    CurrentScenario.Assertions.Remove(existing);
                }
                else
                {
                    var type = AssertColor ? SelectedType : (TileType?)null;
                    var bomb = AssertBomb ? SelectedBomb : (BombType?)null;
                    
                    CurrentScenario.Assertions.Add(new ScenarioAssertion
                    {
                        X = x, Y = y,
                        Type = type,
                        Bomb = bomb
                    });
                }
                IsDirty = true;
                RequestRepaint();
                return;
            }

            if (IsRecording && SimulationController != null)
            {
                var w = ActiveLevelConfig.Width;
                SimulationController.OnTap(new Position(index % w, index / w));
            }
            else
            {
                PaintTile(index);
            }
        }

        public void PaintTile(int index)
        {
            if (index < 0 || index >= ActiveLevelConfig.Grid.Length) return;
            
            if (SelectedBomb != BombType.None)
            {
                ActiveLevelConfig.Bombs[index] = SelectedBomb;
                if (SelectedBomb == BombType.Color)
                {
                    ActiveLevelConfig.Grid[index] = TileType.Rainbow;
                }
                else
                {
                    var current = ActiveLevelConfig.Grid[index];
                    if (current == TileType.None || current == TileType.Bomb || current == TileType.Rainbow)
                    {
                        var defaultColor = (SelectedType >= TileType.Red && SelectedType <= TileType.Orange) 
                            ? SelectedType 
                            : TileType.Red;
                        ActiveLevelConfig.Grid[index] = defaultColor;
                    }
                }
            }
            else
            {
                ActiveLevelConfig.Grid[index] = SelectedType;
                if (SelectedType == TileType.Rainbow)
                {
                    ActiveLevelConfig.Bombs[index] = BombType.Color;
                }
                else
                {
                    ActiveLevelConfig.Bombs[index] = BombType.None;
                }
            }

            RequestRepaint();
            IsDirty = true;
        }

        // --- IO & Export ---
        
        public void ExportJson()
        {
            if (CurrentMode == EditorMode.Level)
                JsonOutput = _jsonService.Serialize(CurrentLevel);
            else
                JsonOutput = _jsonService.Serialize(CurrentScenario);
        }

        public void ImportJson(bool keepScenarioMode = false)
        {
            if (string.IsNullOrWhiteSpace(JsonOutput)) return;
            try
            {
                if (JsonOutput.Contains("Operations"))
                {
                    CurrentScenario = _jsonService.Deserialize<ScenarioConfig>(JsonOutput);
                    CurrentMode = EditorMode.Scenario;
                    EditorWidth = CurrentScenario.InitialState.Width;
                    EditorHeight = CurrentScenario.InitialState.Height;
                }
                else
                {
                    var level = _jsonService.Deserialize<LevelConfig>(JsonOutput);

                    if (keepScenarioMode || CurrentMode == EditorMode.Scenario)
                    {
                        CurrentScenario = new ScenarioConfig 
                        { 
                            InitialState = level,
                            Operations = new List<MoveOperation>() 
                        };
                        CurrentMode = EditorMode.Scenario;
                        EditorWidth = level.Width;
                        EditorHeight = level.Height;
                    }
                    else
                    {
                        CurrentLevel = level;
                        CurrentMode = EditorMode.Level;
                        EditorWidth = CurrentLevel.Width;
                        EditorHeight = CurrentLevel.Height;
                    }
                }
                
                EnsureDefaultLevel();
                RequestRepaint();
                IsDirty = false;
            }
            catch (Exception ex)
            {
                _ = _platform.ShowAlertAsync($"Import Error: {ex.Message}");
            }
        }

        // --- Scenario Management (New Logic) ---

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
                        // Cannot auto-save unnamed file, ask to discard?
                        // For now just proceed or abort? Let's abort if they said YES to save but we can't save.
                        // Or we could trigger "Save As" flow, but simplest is just:
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
                SetScenarioName(Path.GetFileNameWithoutExtension(path));
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
                var currentName = Path.GetFileNameWithoutExtension(CurrentFilePath);
                if (!string.Equals(currentName, ScenarioName, StringComparison.Ordinal))
                {
                     // Logic for rename
                     _scenarioService.RenameScenario(CurrentFilePath, ScenarioName);
                     
                     var stem = ScenarioFileName.SanitizeFileStem(ScenarioName);
                     SetScenarioName(stem);

                     var dir = Path.GetDirectoryName(CurrentFilePath);
                     var newPath = string.IsNullOrEmpty(dir) 
                         ? stem + ".json" 
                         : Path.Combine(dir, stem + ".json");
                     CurrentFilePath = newPath.Replace('\\', '/');
                }

                ExportJson();
                _scenarioService.WriteScenarioJson(CurrentFilePath, JsonOutput);
                IsDirty = false;
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
                _scenarioService.DuplicateScenario(path, Path.GetFileNameWithoutExtension(path) + "_Copy");
                RefreshScenarioList();
            }
            catch(Exception ex) 
            { 
                await _platform.ShowAlertAsync("Error", "Failed to duplicate: " + ex.Message);
            }
        }

        public async Task DeleteFileAsync(string path, bool isFolder)
        {
            var confirm = await _platform.ConfirmAsync("Delete", $"Are you sure you want to delete '{Path.GetFileName(path)}'?");
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

        // --- Simulation ---

        public void StartRecording()
        {
            IsRecording = true;
            IsAssertionMode = false;
            CurrentScenario.Operations.Clear();
            
            var seed = CurrentScenario.Seed;
            var seedManager = new SeedManager(seed);
            
            var view = new EditorGameView(this);
            var config = new Match3Config(ActiveLevelConfig.Width, ActiveLevelConfig.Height, 6);
            
            var scoreSystem = new StandardScoreSystem();
            var inputSystem = new StandardInputSystem();
            var tileGen = new StandardTileGenerator(seedManager.GetRandom(RandomDomain.Refill));

            SimulationController = new Match3Engine(
                config, 
                seedManager.GetRandom(RandomDomain.Main),
                view,
                _logger,
                inputSystem,
                new ClassicMatchFinder(),
                new StandardMatchProcessor(scoreSystem),
                new StandardGravitySystem(tileGen),
                new PowerUpHandler(scoreSystem),
                scoreSystem,
                tileGen,
                ActiveLevelConfig
            );
        }

        public void StopRecording()
        {
            IsRecording = false;
        }

        public void UpdateSimulation(float dt)
        {
            if (SimulationController != null)
            {
                SimulationController.Update(dt);
                RequestRepaint();
            }
        }
        
        public void RecordMove(Position a, Position b)
        {
            CurrentScenario.Operations.Add(new MoveOperation(a.X, a.Y, b.X, b.Y));
            IsDirty = true;
        }

        private class EditorGameView : IGameView
        {
            private readonly LevelEditorViewModel _vm;
            public EditorGameView(LevelEditorViewModel vm) => _vm = vm;
            public void RenderBoard(TileType[,] board) { _vm.RequestRepaint(); }
            public void ShowSwap(Position a, Position b, bool success) 
            {
                if(success) _vm.RecordMove(a, b);
                _vm.RequestRepaint();
            }
            public void ShowMatches(IReadOnlyCollection<Position> matched) { }
            public void ShowGravity(IEnumerable<TileMove> moves) { }
            public void ShowRefill(IEnumerable<TileMove> moves) { }
        }
    }
}
