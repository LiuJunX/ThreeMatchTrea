using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Match3.Core.Scenarios;
using Match3.Editor.Helpers;
using Match3.Editor.Interfaces;
using Match3.Editor.ViewModels;

namespace Match3.Editor.Logic
{
    /// <summary>
    /// Manages Scenario file operations (load, save, create, delete).
    /// Extracted from LevelEditorViewModel to reduce class size.
    /// </summary>
    public class ScenarioFileManager : INotifyPropertyChanged
    {
        private readonly IPlatformService _platform;
        private readonly IScenarioService _scenarioService;
        private readonly IFileSystemService _fileSystem;
        private readonly Func<bool> _getIsDirty;
        private readonly Action<bool> _setIsDirty;
        private readonly Func<string> _getScenarioName;
        private readonly Action<string> _setScenarioName;
        private readonly Func<string> _exportJson;
        private readonly Action<string, bool> _importJson;

        public ScenarioFolderNode? RootFolderNode { get; private set; }
        public List<ScenarioFileEntry> SearchResults { get; private set; } = new List<ScenarioFileEntry>();
        public string CurrentFilePath { get; set; } = "";
        public HashSet<string> ExpandedPaths { get; } = new HashSet<string>();

        public event PropertyChangedEventHandler? PropertyChanged;

        public ScenarioFileManager(
            IPlatformService platform,
            IScenarioService scenarioService,
            IFileSystemService fileSystem,
            Func<bool> getIsDirty,
            Action<bool> setIsDirty,
            Func<string> getScenarioName,
            Action<string> setScenarioName,
            Func<string> exportJson,
            Action<string, bool> importJson)
        {
            _platform = platform;
            _scenarioService = scenarioService;
            _fileSystem = fileSystem;
            _getIsDirty = getIsDirty;
            _setIsDirty = setIsDirty;
            _getScenarioName = getScenarioName;
            _setScenarioName = setScenarioName;
            _exportJson = exportJson;
            _importJson = importJson;
        }

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void SetRootFolder(ScenarioFolderNode root)
        {
            RootFolderNode = root;
            OnPropertyChanged(nameof(RootFolderNode));
        }

        public void RefreshScenarioList()
        {
            RootFolderNode = _scenarioService.BuildTree();
            OnPropertyChanged(nameof(RootFolderNode));
        }

        public async Task LoadScenarioAsync(string path)
        {
            if (_getIsDirty())
            {
                var confirm = await _platform.ConfirmAsync(
                    "Unsaved Changes",
                    "You have unsaved changes. Do you want to save them before switching?");
                if (confirm)
                {
                    if (!string.IsNullOrEmpty(CurrentFilePath))
                    {
                        await SaveScenarioAsync();
                    }
                    else
                    {
                        var discard = await _platform.ConfirmAsync(
                            "Cannot Save",
                            "File has no path. Discard changes?");
                        if (!discard) return;
                    }
                }
                else
                {
                    var discard = await _platform.ConfirmAsync(
                        "Discard Changes?",
                        "Are you sure you want to discard unsaved changes?");
                    if (!discard) return;
                }
            }

            try
            {
                var json = _scenarioService.ReadScenarioJson(path);
                _importJson(json, true);
                CurrentFilePath = path;
                _setScenarioName(_fileSystem.GetFileNameWithoutExtension(path));
            }
            catch (Exception ex)
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
                var scenarioName = _getScenarioName();
                if (!string.Equals(currentName, scenarioName, StringComparison.Ordinal))
                {
                    _scenarioService.RenameScenario(CurrentFilePath, scenarioName);

                    var stem = ScenarioFileName.SanitizeFileStem(scenarioName);
                    _setScenarioName(stem);

                    var dir = _fileSystem.GetDirectoryName(CurrentFilePath);
                    var newPath = string.IsNullOrEmpty(dir)
                        ? stem + ".json"
                        : _fileSystem.CombinePath(dir, stem + ".json");
                    CurrentFilePath = _fileSystem.NormalizePath(newPath);
                }

                var json = _exportJson();
                _scenarioService.WriteScenarioJson(CurrentFilePath, json);
                _setIsDirty(false);
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
            catch (Exception ex)
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
            catch (Exception ex)
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
            catch (Exception ex)
            {
                await _platform.ShowAlertAsync("Error", "Failed to duplicate: " + ex.Message);
            }
        }

        public async Task DeleteFileAsync(string path, bool isFolder)
        {
            var confirm = await _platform.ConfirmAsync(
                "Delete",
                "Are you sure you want to delete '" + _fileSystem.GetFileName(path) + "'?");
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
            catch (Exception ex)
            {
                await _platform.ShowAlertAsync("Error", "Failed to delete: " + ex.Message);
            }
        }
    }
}
