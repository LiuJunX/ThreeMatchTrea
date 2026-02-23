using System.IO;
using System.Linq;
using UnityEngine;
using IEditorFileSystem = global::Match3.Editor.IEditorFileSystem;

namespace Match3.Unity.LevelEditor.Services
{
    /// <summary>
    /// IEditorFileSystem implementation for Unity.
    /// Uses config/levels/ directory relative to project root.
    /// </summary>
    public class UnityEditorFileSystem : IEditorFileSystem
    {
        private readonly string _levelsDir;

        public UnityEditorFileSystem()
        {
            // In Editor: Application.dataPath = .../unity/Assets
            // Project root is two levels up: .../
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            _levelsDir = Path.Combine(projectRoot, "config", "levels");
            if (!Directory.Exists(_levelsDir))
                Directory.CreateDirectory(_levelsDir);
        }

        public string ReadText(string path) => File.ReadAllText(path);
        public void WriteText(string path, string content) => File.WriteAllText(path, content);
        public bool FileExists(string path) => File.Exists(path);

        public string[] ListFiles(string directory, string pattern)
        {
            if (!Directory.Exists(directory)) return new string[0];
            return Directory.GetFiles(directory, pattern)
                .OrderBy(f => f)
                .ToArray();
        }

        public void CreateDirectory(string path) => Directory.CreateDirectory(path);
        public void DeleteFile(string path) => File.Delete(path);
        public string GetLevelsDirectory() => _levelsDir;
    }
}
