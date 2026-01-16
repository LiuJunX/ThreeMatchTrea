using Match3.Editor.Interfaces;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Match3.Web.Services.EditorAdapters
{
    public class PhysicalFileSystemService : IFileSystemService
    {
        private readonly string _rootDir;

        public PhysicalFileSystemService(string rootDir)
        {
            _rootDir = rootDir;
        }

        public string GetStorageRoot() => _rootDir;

        public async Task WriteTextAsync(string path, string content)
        {
            var fullPath = ResolvePath(path);
            var dir = Path.GetDirectoryName(fullPath);
            if (dir != null)
            {
                Directory.CreateDirectory(dir);
            }
            await File.WriteAllTextAsync(fullPath, content);
        }

        public async Task<string> ReadTextAsync(string path)
        {
            return await File.ReadAllTextAsync(ResolvePath(path));
        }

        public IEnumerable<string> GetFiles(string dir, string pattern)
        {
            var fullDir = ResolvePath(dir);
            if (!Directory.Exists(fullDir)) return System.Array.Empty<string>();
            return Directory.GetFiles(fullDir, pattern);
        }

        public IEnumerable<string> GetDirectories(string dir)
        {
            var fullDir = ResolvePath(dir);
            if (!Directory.Exists(fullDir)) return System.Array.Empty<string>();
            return Directory.GetDirectories(fullDir);
        }

        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(ResolvePath(path));
        }

        public void DeleteFile(string path)
        {
            var full = ResolvePath(path);
            if (File.Exists(full)) File.Delete(full);
        }

        public void DeleteDirectory(string path)
        {
            var full = ResolvePath(path);
            if (Directory.Exists(full)) Directory.Delete(full, true);
        }

        public bool FileExists(string path) => File.Exists(ResolvePath(path));
        public bool DirectoryExists(string path) => Directory.Exists(ResolvePath(path));

        // Path operations
        public string GetFileName(string path) => Path.GetFileName(path);
        public string GetFileNameWithoutExtension(string path) => Path.GetFileNameWithoutExtension(path);
        public string GetDirectoryName(string path) => Path.GetDirectoryName(path) ?? string.Empty;
        public string CombinePath(string path1, string path2) => Path.Combine(path1, path2);
        public string NormalizePath(string path) => path.Replace('\\', '/');

        private string ResolvePath(string path)
        {
            if (Path.IsPathRooted(path)) return path;
            return Path.Combine(_rootDir, path);
        }
    }
}
