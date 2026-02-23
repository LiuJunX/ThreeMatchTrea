using System;
using System.Linq;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Editor.Logic;
using Match3.Editor.Validation;
using Match3.Random;

namespace Match3.Editor
{
    /// <summary>
    /// The single entry point for all level editing operations.
    /// Pure C#, no UI framework dependency.
    /// View reads state properties and subscribes to StateChanged.
    /// View calls methods to perform edits.
    /// Editor never initiates dialogs — returns results for the View to handle.
    /// </summary>
    public class LevelEditor
    {
        private readonly IEditorFileSystem _fs;
        private readonly UndoHistory _undo = new UndoHistory();
        private readonly LevelValidator _validator = new LevelValidator();
        private readonly GridManipulator _grid = new GridManipulator();

        private bool _strokeActive;
        private bool _strokeDirty;

        // ═══════════════════════════════════════════
        //  State — View reads these to render
        // ═══════════════════════════════════════════

        public LevelConfig Level { get; private set; } = null!;

        /// <summary>0 = Tiles, 1 = Covers, 2 = Grounds</summary>
        public int ActiveLayer { get; private set; }

        public TileType SelectedTileType { get; private set; } = TileType.Red;
        public BombType SelectedBombType { get; private set; } = BombType.None;
        public CoverType SelectedCoverType { get; private set; } = CoverType.None;
        public GroundType SelectedGroundType { get; private set; } = GroundType.None;

        public bool CanUndo => _undo.CanUndo;
        public bool CanRedo => _undo.CanRedo;
        public bool IsDirty { get; private set; }
        public string CurrentFilePath { get; private set; } = "";

        // ═══════════════════════════════════════════
        //  Events
        // ═══════════════════════════════════════════

        /// <summary>
        /// Fired after any state mutation. View should re-read state and repaint.
        /// </summary>
        public event Action? StateChanged;

        // ═══════════════════════════════════════════
        //  Constructor
        // ═══════════════════════════════════════════

        public LevelEditor(IEditorFileSystem fileSystem)
        {
            _fs = fileSystem;
            NewLevel(8, 8);
        }

        // ═══════════════════════════════════════════
        //  Tool Selection
        // ═══════════════════════════════════════════

        public void SetActiveLayer(int layer)
        {
            ActiveLayer = Math.Max(0, Math.Min(2, layer));
            Notify();
        }

        public void SetSelectedTileType(TileType type)
        {
            SelectedTileType = type;
            Notify();
        }

        public void SetSelectedBombType(BombType type)
        {
            SelectedBombType = type;
            Notify();
        }

        public void SetSelectedCoverType(CoverType type)
        {
            SelectedCoverType = type;
            Notify();
        }

        public void SetSelectedGroundType(GroundType type)
        {
            SelectedGroundType = type;
            Notify();
        }

        // ═══════════════════════════════════════════
        //  Stroke-based Painting
        // ═══════════════════════════════════════════

        /// <summary>
        /// Call at the start of a drag/click. Groups subsequent PaintCell
        /// calls into a single undo entry.
        /// If a previous stroke was not ended, it is ended first.
        /// </summary>
        public void BeginStroke()
        {
            if (_strokeActive)
                EndStroke();
            _strokeActive = true;
            _strokeDirty = false;
        }

        /// <summary>
        /// Paint a single cell using the currently selected tool.
        /// If no stroke is active, this is treated as a single-cell stroke.
        /// </summary>
        public void PaintCell(int x, int y)
        {
            bool autoStroke = !_strokeActive;
            if (autoStroke)
                BeginStroke();

            int index = y * Level.Width + x;
            if (index < 0 || index >= Level.Grid.Length)
            {
                if (autoStroke) EndStroke();
                return;
            }

            switch (ActiveLayer)
            {
                case 0: // Tiles
                    _grid.PaintTile(Level, index, SelectedTileType, SelectedBombType);
                    _strokeDirty = true;
                    break;
                case 1: // Covers
                    if (SelectedCoverType == CoverType.None)
                        _grid.ClearCover(Level, index);
                    else
                        _grid.PaintCover(Level, index, SelectedCoverType);
                    _strokeDirty = true;
                    break;
                case 2: // Grounds
                    if (SelectedGroundType == GroundType.None)
                        _grid.ClearGround(Level, index);
                    else
                        _grid.PaintGround(Level, index, SelectedGroundType);
                    _strokeDirty = true;
                    break;
            }

            if (autoStroke)
                EndStroke();
            else
                Notify();
        }

        /// <summary>
        /// End the current stroke. Records an undo snapshot if any cell changed.
        /// </summary>
        public void EndStroke()
        {
            if (_strokeDirty)
            {
                _undo.Record(Level);
                IsDirty = true;
            }
            _strokeActive = false;
            _strokeDirty = false;
            Notify();
        }

        // ═══════════════════════════════════════════
        //  Batch Operations (auto-record undo)
        // ═══════════════════════════════════════════

        public void Resize(int width, int height)
        {
            width = Math.Max(3, Math.Min(20, width));
            height = Math.Max(3, Math.Min(20, height));
            if (width == Level.Width && height == Level.Height) return;

            Level = _grid.ResizeGrid(Level, width, height);
            RecordAfterChange();
        }

        public void GenerateRandom(int seed)
        {
            _grid.GenerateRandomLevel(Level, seed);
            RecordAfterChange();
        }

        public void SetMoveLimit(int limit)
        {
            limit = Math.Max(1, limit);
            if (Level.MoveLimit == limit) return;
            Level.MoveLimit = limit;
            RecordAfterChange();
        }

        public void SetTargetDifficulty(float difficulty)
        {
            difficulty = Math.Max(0f, Math.Min(1f, difficulty));
            if (Math.Abs(Level.TargetDifficulty - difficulty) < 0.001f) return;
            Level.TargetDifficulty = difficulty;
            RecordAfterChange();
        }

        // ═══════════════════════════════════════════
        //  Objective Editing
        // ═══════════════════════════════════════════

        public void AddObjective()
        {
            if (ObjectiveEditorHelper.TryAddObjective(Level.Objectives))
                RecordAfterChange();
        }

        public void RemoveObjective(int index)
        {
            if (ObjectiveEditorHelper.TryRemoveObjective(Level.Objectives, index))
                RecordAfterChange();
        }

        public void SetObjective(int index, ObjectiveTargetLayer layer, int elementType, int targetCount)
        {
            if (index < 0 || index >= Level.Objectives.Length) return;
            var obj = Level.Objectives[index];
            obj.TargetLayer = layer;
            obj.ElementType = elementType;
            obj.TargetCount = Math.Max(1, targetCount);
            Level.Objectives[index] = obj;
            RecordAfterChange();
        }

        // ═══════════════════════════════════════════
        //  Undo / Redo
        // ═══════════════════════════════════════════

        public void Undo()
        {
            var snapshot = _undo.Undo();
            if (snapshot == null) return;
            Level = snapshot;
            IsDirty = true;
            Notify();
        }

        public void Redo()
        {
            var snapshot = _undo.Redo();
            if (snapshot == null) return;
            Level = snapshot;
            IsDirty = true;
            Notify();
        }

        // ═══════════════════════════════════════════
        //  File IO — no dialogs, returns results
        // ═══════════════════════════════════════════

        /// <summary>
        /// Save to current file path. Validates first.
        /// Returns validation result — caller decides whether to show errors.
        /// </summary>
        public ValidationResult Save()
        {
            if (string.IsNullOrEmpty(CurrentFilePath))
                return new ValidationResult(new System.Collections.Generic.List<ValidationMessage>
                {
                    new ValidationMessage(Severity.Error, "No file path set. Use SaveAs().")
                });

            return SaveAs(CurrentFilePath);
        }

        /// <summary>
        /// Save to a specific path. Validates first.
        /// </summary>
        public ValidationResult SaveAs(string path)
        {
            var result = Validate();
            if (result.HasErrors)
                return result;

            var json = ConfigParser.Serialize(Level);
            _fs.WriteText(path, json);
            CurrentFilePath = path;
            IsDirty = false;
            Notify();
            return result;
        }

        /// <summary>
        /// Load a level from file. Replaces current state and resets undo history.
        /// </summary>
        public void Load(string path)
        {
            var json = _fs.ReadText(path);
            Level = ConfigParser.ParseLevelConfig(json);
            CurrentFilePath = path;
            IsDirty = false;
            _undo.Clear();
            _undo.Record(Level);
            Notify();
        }

        /// <summary>
        /// Create a new blank level with default objectives.
        /// </summary>
        public void NewLevel(int width, int height)
        {
            width = Math.Max(3, Math.Min(20, width));
            height = Math.Max(3, Math.Min(20, height));

            Level = new LevelConfig(width, height);
            Level.Objectives[0] = new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)TileType.Red,
                TargetCount = 20
            };

            _grid.GenerateRandomLevel(Level, Environment.TickCount);

            CurrentFilePath = "";
            IsDirty = false;
            _undo.Clear();
            _undo.Record(Level);
            Notify();
        }

        /// <summary>
        /// List all .json level files in the levels directory.
        /// </summary>
        public string[] GetLevelFiles()
        {
            var dir = _fs.GetLevelsDirectory();
            return _fs.ListFiles(dir, "*.json");
        }

        /// <summary>
        /// Returns the levels directory path. View uses this to construct save paths.
        /// </summary>
        public string GetLevelsDirectory()
        {
            return _fs.GetLevelsDirectory();
        }

        /// <summary>
        /// Returns just the file name from CurrentFilePath (no directory).
        /// Returns empty string if no file is set.
        /// </summary>
        public string GetCurrentFileName()
        {
            if (string.IsNullOrEmpty(CurrentFilePath)) return "";
            int sep = CurrentFilePath.LastIndexOfAny(new[] { '/', '\\' });
            return sep >= 0 ? CurrentFilePath.Substring(sep + 1) : CurrentFilePath;
        }

        // ═══════════════════════════════════════════
        //  Validation
        // ═══════════════════════════════════════════

        public ValidationResult Validate()
        {
            return _validator.Validate(Level);
        }

        // ═══════════════════════════════════════════
        //  Internal helpers
        // ═══════════════════════════════════════════

        private void RecordAfterChange()
        {
            _undo.Record(Level);
            IsDirty = true;
            Notify();
        }

        private void Notify()
        {
            StateChanged?.Invoke();
        }
    }
}
