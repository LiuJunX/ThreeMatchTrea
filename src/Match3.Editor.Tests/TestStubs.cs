using System.Collections.Generic;
using System.Threading.Tasks;
using Match3.Core.Analysis;
using Match3.Editor.Interfaces;
using Match3.Editor.ViewModels;

namespace Match3.Editor.Tests
{
    /// <summary>
    /// Stub implementations for unit testing.
    /// </summary>
    public static class TestStubs
    {
        public class StubPlatformService : IPlatformService
        {
            public bool ConfirmResult { get; set; } = true;
            public List<string> AlertMessages { get; } = new List<string>();
            public int ConfirmCallCount { get; private set; }

            public Task ShowAlertAsync(string message)
            {
                AlertMessages.Add(message);
                return Task.CompletedTask;
            }

            public Task ShowAlertAsync(string title, string message)
            {
                AlertMessages.Add($"{title}: {message}");
                return Task.CompletedTask;
            }

            public Task<bool> ConfirmAsync(string title, string message)
            {
                ConfirmCallCount++;
                return Task.FromResult(ConfirmResult);
            }

            public Task<string> PromptAsync(string title, string defaultValue = "")
                => Task.FromResult(defaultValue);

            public Task CopyToClipboardAsync(string text) => Task.CompletedTask;
        }

        public class StubLevelService : ILevelService
        {
            public string? LastCreatedPath { get; private set; }
            public string? LastWrittenPath { get; private set; }
            public string? LastWrittenJson { get; private set; }
            public string? LastDeletedPath { get; private set; }
            public string? LastDuplicatedPath { get; private set; }
            public LevelAnalysisSnapshot? SnapshotToReturn { get; set; }
            public LevelAnalysisSnapshot? LastWrittenSnapshot { get; private set; }
            public string JsonToReturn { get; set; } = "{}";
            public bool ThrowOnRead { get; set; }

            public ScenarioFolderNode BuildTree() => new ScenarioFolderNode(
                "levels", "data/levels",
                System.Array.Empty<ScenarioFolderNode>(),
                System.Array.Empty<ScenarioFileEntry>());

            public string CreateNewLevel(string folderPath, string name, string json)
            {
                LastCreatedPath = $"{folderPath}/{name}.json";
                return LastCreatedPath;
            }

            public string CreateFolder(string parentPath, string name) => $"{parentPath}/{name}";

            public string DuplicateLevel(string path, string newName)
            {
                LastDuplicatedPath = path;
                return $"{System.IO.Path.GetDirectoryName(path)}/{newName}.json";
            }

            public void DeleteLevel(string path) => LastDeletedPath = path;
            public void DeleteFolder(string path) => LastDeletedPath = path;
            public void RenameLevel(string path, string newName) { }
            public void RenameFolder(string path, string newName) { }

            public string ReadLevelJson(string path)
            {
                if (ThrowOnRead)
                    throw new System.IO.FileNotFoundException("File not found");
                return JsonToReturn;
            }

            public void WriteLevelJson(string path, string json)
            {
                LastWrittenPath = path;
                LastWrittenJson = json;
            }

            public LevelAnalysisSnapshot? ReadAnalysisSnapshot(string levelPath) => SnapshotToReturn;

            public void WriteAnalysisSnapshot(string levelPath, LevelAnalysisSnapshot snapshot)
            {
                LastWrittenSnapshot = snapshot;
            }

            public string GetAnalysisFilePath(string levelPath) =>
                levelPath.Replace(".json", ".analysis.json");
        }

        public class StubScenarioService : IScenarioService
        {
            public string? LastCreatedPath { get; private set; }
            public string? LastWrittenPath { get; private set; }
            public string? LastWrittenJson { get; private set; }
            public string? LastDeletedPath { get; private set; }
            public string? LastDuplicatedPath { get; private set; }
            public string? LastRenamedPath { get; private set; }
            public string? LastRenamedNewName { get; private set; }
            public string JsonToReturn { get; set; } = "{}";
            public bool ThrowOnRead { get; set; }

            public ScenarioFolderNode BuildTree() => new ScenarioFolderNode(
                "scenarios", "data/scenarios",
                System.Array.Empty<ScenarioFolderNode>(),
                System.Array.Empty<ScenarioFileEntry>());

            public string CreateNewScenario(string folderPath, string name, string json)
            {
                LastCreatedPath = $"{folderPath}/{name}.json";
                return LastCreatedPath;
            }

            public string CreateFolder(string parentPath, string name) => $"{parentPath}/{name}";

            public string DuplicateScenario(string path, string newName)
            {
                LastDuplicatedPath = path;
                return $"{System.IO.Path.GetDirectoryName(path)}/{newName}.json";
            }

            public void DeleteScenario(string path) => LastDeletedPath = path;
            public void DeleteFolder(string path) => LastDeletedPath = path;

            public void RenameScenario(string path, string newName)
            {
                LastRenamedPath = path;
                LastRenamedNewName = newName;
            }

            public void RenameFolder(string path, string newName) { }

            public string ReadScenarioJson(string path)
            {
                if (ThrowOnRead)
                    throw new System.IO.FileNotFoundException("File not found");
                return JsonToReturn;
            }

            public void WriteScenarioJson(string path, string json)
            {
                LastWrittenPath = path;
                LastWrittenJson = json;
            }
        }

        public class StubFileSystemService : IFileSystemService
        {
            public Task WriteTextAsync(string path, string content) => Task.CompletedTask;
            public Task<string> ReadTextAsync(string path) => Task.FromResult("{}");
            public IEnumerable<string> GetFiles(string dir, string pattern) => System.Array.Empty<string>();
            public IEnumerable<string> GetDirectories(string dir) => System.Array.Empty<string>();
            public void CreateDirectory(string path) { }
            public void DeleteFile(string path) { }
            public void DeleteDirectory(string path) { }
            public bool FileExists(string path) => true;
            public bool DirectoryExists(string path) => true;
            public string GetStorageRoot() => "data";
            public string GetFileName(string path) => System.IO.Path.GetFileName(path);
            public string GetFileNameWithoutExtension(string path) => System.IO.Path.GetFileNameWithoutExtension(path);
            public string GetDirectoryName(string path) => System.IO.Path.GetDirectoryName(path) ?? "";
            public string CombinePath(string path1, string path2) => System.IO.Path.Combine(path1, path2);
            public string NormalizePath(string path) => path.Replace('\\', '/');
        }
    }
}
