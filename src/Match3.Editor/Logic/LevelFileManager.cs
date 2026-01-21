using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Match3.Editor.Interfaces;
using Match3.Editor.ViewModels;

namespace Match3.Editor.Logic
{
    /// <summary>
    /// Manages Level file operations (load, save, create, delete).
    /// Extracted from LevelEditorViewModel to reduce class size.
    /// </summary>
    public class LevelFileManager : INotifyPropertyChanged
    {
        private readonly IPlatformService _platform;
        private readonly ILevelService _levelService;
        private readonly IFileSystemService _fileSystem;
        private readonly Func<bool> _getIsDirty;
        private readonly Action<bool> _setIsDirty;
        private readonly Func<string> _exportJson;
        private readonly Action<string, bool> _importJson;

        public ScenarioFolderNode? RootLevelFolderNode { get; private set; }
        public string CurrentLevelFilePath { get; set; } = "";
        public HashSet<string> LevelExpandedPaths { get; } = new HashSet<string>();

        public event PropertyChangedEventHandler? PropertyChanged;

        public LevelFileManager(
            IPlatformService platform,
            ILevelService levelService,
            IFileSystemService fileSystem,
            Func<bool> getIsDirty,
            Action<bool> setIsDirty,
            Func<string> exportJson,
            Action<string, bool> importJson)
        {
            _platform = platform;
            _levelService = levelService;
            _fileSystem = fileSystem;
            _getIsDirty = getIsDirty;
            _setIsDirty = setIsDirty;
            _exportJson = exportJson;
            _importJson = importJson;
        }

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void SetRootLevelFolder(ScenarioFolderNode root)
        {
            RootLevelFolderNode = root;
            OnPropertyChanged(nameof(RootLevelFolderNode));
        }

        public void RefreshLevelList()
        {
            RootLevelFolderNode = _levelService.BuildTree();
            OnPropertyChanged(nameof(RootLevelFolderNode));
        }

        public async Task LoadLevelAsync(string path, Action? onLoadSuccess = null)
        {
            if (_getIsDirty())
            {
                var confirm = await _platform.ConfirmAsync(
                    "Unsaved Changes",
                    "You have unsaved changes. Do you want to save them before switching?");
                if (confirm)
                {
                    if (!string.IsNullOrEmpty(CurrentLevelFilePath))
                    {
                        await SaveLevelAsync();
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
                var json = _levelService.ReadLevelJson(path);
                _importJson(json, false);
                CurrentLevelFilePath = path;
                _setIsDirty(false);
                onLoadSuccess?.Invoke();
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
                var json = _exportJson();
                _levelService.WriteLevelJson(CurrentLevelFilePath, json);
                _setIsDirty(false);
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
                var json = _exportJson();
                var newPath = _levelService.CreateNewLevel(folderPath, "New Level", json);
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
            var confirm = await _platform.ConfirmAsync(
                "Delete",
                "Are you sure you want to delete '" + _fileSystem.GetFileName(path) + "'?");
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
