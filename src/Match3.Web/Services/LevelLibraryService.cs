using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Match3.Core.Scenarios;
using Match3.Editor.Interfaces;
using Match3.Editor.ViewModels;

namespace Match3.Web.Services;

public sealed class LevelLibraryService : ILevelService
{
    private readonly string _rootDir;
    private readonly Dictionary<string, (DateTime LastWrite, ScenarioFileEntry Entry)> _entryCache = new();

    public LevelLibraryService(string rootDir)
    {
        _rootDir = rootDir;
    }

    public string RootDir => _rootDir;

    public ScenarioFolderNode BuildTree()
    {
        Directory.CreateDirectory(_rootDir);
        return BuildFolderNode(_rootDir, "");
    }

    public string ReadLevelJson(string fileRelativePath)
    {
        var full = ToFullPath(fileRelativePath, isFile: true);
        return File.ReadAllText(full);
    }

    public void WriteLevelJson(string fileRelativePath, string json)
    {
        var full = ToFullPath(fileRelativePath, isFile: true);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(full, json);
    }

    public string CreateNewLevel(string folderRelativePath, string levelName, string json)
    {
        var folderFull = ToFullPath(folderRelativePath, isFile: false);
        Directory.CreateDirectory(folderFull);

        var stem = ScenarioFileName.SanitizeFileStem(levelName);
        var fileName = $"{stem}.json";
        var full = Path.Combine(folderFull, fileName);
        for (int i = 2; File.Exists(full); i++)
        {
            fileName = $"{stem}_{i}.json";
            full = Path.Combine(folderFull, fileName);
        }

        File.WriteAllText(full, json);
        return ToRelativePath(full);
    }

    public string DuplicateLevel(string sourceRelativePath, string newLevelName)
    {
        var sourceFull = ToFullPath(sourceRelativePath, isFile: true);
        var folderFull = Path.GetDirectoryName(sourceFull) ?? _rootDir;
        var json = File.ReadAllText(sourceFull);
        return CreateNewLevel(ToRelativePath(folderFull), newLevelName, json);
    }

    public void DeleteLevel(string fileRelativePath)
    {
        var full = ToFullPath(fileRelativePath, isFile: true);
        if (File.Exists(full))
        {
            File.Delete(full);
        }
    }

    public string CreateFolder(string parentFolderRelativePath, string folderName)
    {
        var parentFull = ToFullPath(parentFolderRelativePath, isFile: false);
        Directory.CreateDirectory(parentFull);

        var stem = ScenarioFileName.SanitizeFileStem(folderName);
        var full = Path.Combine(parentFull, stem);
        Directory.CreateDirectory(full);
        return ToRelativePath(full);
    }

    public void DeleteFolder(string folderRelativePath)
    {
        var fullPath = ToFullPath(folderRelativePath, isFile: false);
        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, true);
        }
    }

    public void RenameLevel(string relativePath, string newName)
    {
        var fullPath = ToFullPath(relativePath, isFile: true);
        if (!File.Exists(fullPath)) return;

        var directory = Path.GetDirectoryName(fullPath);
        var stem = ScenarioFileName.SanitizeFileStem(newName);
        var newFileName = $"{stem}.json";
        var newFullPath = Path.Combine(directory!, newFileName);

        if (fullPath != newFullPath)
        {
            if (File.Exists(newFullPath))
            {
                throw new IOException($"A level with the name '{stem}' already exists.");
            }
            File.Move(fullPath, newFullPath);
        }
    }

    public void RenameFolder(string relativePath, string newName)
    {
        var fullPath = ToFullPath(relativePath, isFile: false);
        if (!Directory.Exists(fullPath)) return;

        var parent = Path.GetDirectoryName(fullPath);
        var safeName = ScenarioFileName.SanitizeFileStem(newName);
        var newFullPath = Path.Combine(parent!, safeName);

        if (fullPath != newFullPath && !Directory.Exists(newFullPath))
        {
            Directory.Move(fullPath, newFullPath);
        }
    }

    private ScenarioFolderNode BuildFolderNode(string fullPath, string relativePath)
    {
        var name = string.IsNullOrWhiteSpace(relativePath) ? "/" : Path.GetFileName(fullPath);

        var folders = Directory.EnumerateDirectories(fullPath, "*", SearchOption.TopDirectoryOnly)
            .Select(dir =>
            {
                var childRel = CombineRelative(relativePath, Path.GetFileName(dir));
                return BuildFolderNode(dir, childRel);
            })
            .OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var files = Directory.EnumerateFiles(fullPath, "*.json", SearchOption.TopDirectoryOnly)
            .Select(CreateFileEntryFromDisk)
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ScenarioFolderNode(name, relativePath, folders, files);
    }

    private ScenarioFileEntry CreateFileEntryFromDisk(string fullPath)
    {
        var fileInfo = new FileInfo(fullPath);
        var lastWrite = fileInfo.LastWriteTimeUtc;

        // Check cache
        if (_entryCache.TryGetValue(fullPath, out var cached))
        {
            if (cached.LastWrite == lastWrite)
            {
                return cached.Entry;
            }
        }

        var rel = ToRelativePath(fullPath);
        var name = Path.GetFileNameWithoutExtension(fullPath);

        // For levels, we use a simpler metadata structure
        var metadata = new ScenarioMetadata
        {
            CreatedUtc = fileInfo.CreationTimeUtc,
            UpdatedUtc = fileInfo.LastWriteTimeUtc
        };

        var entry = new ScenarioFileEntry(rel, name, metadata, fileInfo.Length);

        // Update cache
        _entryCache[fullPath] = (lastWrite, entry);

        return entry;
    }

    private string ToFullPath(string relativePath, bool isFile)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return _rootDir;
        }

        var sanitized = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        sanitized = sanitized.TrimStart(Path.DirectorySeparatorChar);

        var combined = Path.GetFullPath(Path.Combine(_rootDir, sanitized));
        var rootFull = Path.GetFullPath(_rootDir);
        if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid path.");
        }

        return combined;
    }

    private string ToRelativePath(string fullPath)
    {
        var rootFull = Path.GetFullPath(_rootDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(fullPath);
        if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        var rel = full.Substring(rootFull.Length);
        return rel.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string CombineRelative(string parent, string child)
    {
        if (string.IsNullOrWhiteSpace(parent))
        {
            return child.Replace('\\', '/');
        }
        return (parent.TrimEnd('/') + "/" + child).Replace('\\', '/');
    }
}
