using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Match3.Core.Scenarios;
using Match3.Editor.Interfaces;
using Match3.Editor.ViewModels;

namespace Match3.Web.Services;

public sealed class ScenarioLibraryService : IScenarioService
{
    private readonly string _rootDir;
    private readonly Dictionary<string, (DateTime LastWrite, ScenarioFileEntry Entry)> _entryCache = new();

    public ScenarioLibraryService(string rootDir)
    {
        _rootDir = rootDir;
    }

    public string RootDir => _rootDir;

    public ScenarioFolderNode BuildTree()
    {
        Directory.CreateDirectory(_rootDir);
        return BuildFolderNode(_rootDir, "");
    }

    public bool IsPathContained(string parentPath, string childPath)
    {
        var parentFull = ToFullPath(parentPath, isFile: false).TrimEnd(Path.DirectorySeparatorChar);
        var childFull = ToFullPath(childPath, isFile: true); // Assuming child is file or folder, but treating as path
        
        // If checking folder containment, ensure both are treated consistently
        if (!Path.HasExtension(childPath))
        {
             childFull = ToFullPath(childPath, isFile: false).TrimEnd(Path.DirectorySeparatorChar);
        }

        return childFull.StartsWith(parentFull, StringComparison.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ScenarioFileEntry> SearchFiles(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText)) return Array.Empty<ScenarioFileEntry>();

        var allFiles = Directory.EnumerateFiles(_rootDir, "*.json", SearchOption.AllDirectories);
        var regex = new Regex(searchText, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        return allFiles
            .Select(CreateFileEntryFromDisk)
            .Where(e => Matches(e, searchText, regex))
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
    public ScenarioFileEntry GetFileEntry(string fileRelativePath)
    {
        var full = ToFullPath(fileRelativePath, isFile: true);
        return CreateFileEntryFromDisk(full);
    }

    public string ReadScenarioJson(string fileRelativePath)
    {
        var full = ToFullPath(fileRelativePath, isFile: true);
        return File.ReadAllText(full);
    }

    public void WriteScenarioJson(string fileRelativePath, string json)
    {
        var full = ToFullPath(fileRelativePath, isFile: true);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(full, json);
    }

    public string CreateNewScenario(string folderRelativePath, string scenarioName, string json)
    {
        var folderFull = ToFullPath(folderRelativePath, isFile: false);
        Directory.CreateDirectory(folderFull);

        var stem = ScenarioFileName.SanitizeFileStem(scenarioName);
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

    public string DuplicateScenario(string sourceRelativePath, string newScenarioName)
    {
        var sourceFull = ToFullPath(sourceRelativePath, isFile: true);
        var folderFull = Path.GetDirectoryName(sourceFull) ?? _rootDir;
        var json = File.ReadAllText(sourceFull);
        return CreateNewScenario(ToRelativePath(folderFull), newScenarioName, json);
    }


    public void DeleteScenario(string fileRelativePath)
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

    public void CopyFolder(string sourceRelativePath, string destParentRelativePath)
    {
        var sourceFull = ToFullPath(sourceRelativePath, isFile: false);
        var sourceDirName = Path.GetFileName(sourceFull);
        var destParentFull = ToFullPath(destParentRelativePath, isFile: false);
        
        // Create new folder name like "Folder_Copy"
        var newFolderName = $"{sourceDirName}_Copy";
        var destFull = Path.Combine(destParentFull, newFolderName);
        
        // Ensure unique name
        int i = 1;
        while (Directory.Exists(destFull))
        {
            newFolderName = $"{sourceDirName}_Copy{i++}";
            destFull = Path.Combine(destParentFull, newFolderName);
        }
        
        Directory.CreateDirectory(destFull);
        
        // Recursive copy
        CopyDirectory(sourceFull, destFull);
    }

    private void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSubDir);
        }
    }

    public void RenameScenario(string relativePath, string newName)
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
                throw new IOException($"A scenario with the name '{stem}' already exists.");
            }
            File.Move(fullPath, newFullPath);
        }
    }

    public void RenameFolder(string relativePath, string newName)
    {
        var fullPath = ToFullPath(relativePath, isFile: false);
        if (!Directory.Exists(fullPath)) return;

        var parent = Path.GetDirectoryName(fullPath);
        // Sanitize folder name?
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
        ScenarioMetadata? metadata = null;
        string? name = null;

        try
        {
            using var stream = File.OpenRead(fullPath);
            using var doc = JsonDocument.Parse(stream);
            
            // Case-insensitive property search
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name.Equals("Metadata", StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.Object)
                {
                    metadata = JsonSerializer.Deserialize<ScenarioMetadata>(prop.Value.GetRawText(), new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
            }
        }
        catch (Exception)
        {
            // If parsing fails, we'll fall back to default metadata.
            // Consider logging this error if IGameLogger is available in future.
        }

        if (metadata == null)
        {
            metadata = new ScenarioMetadata
            {
                CreatedUtc = fileInfo.CreationTimeUtc,
                UpdatedUtc = fileInfo.LastWriteTimeUtc
            };
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = Path.GetFileNameWithoutExtension(fullPath);
        }

        var entry = new ScenarioFileEntry(rel, name, metadata, fileInfo.Length);
        
        // Update cache
        _entryCache[fullPath] = (lastWrite, entry);

        return entry;
    }

    private bool Matches(ScenarioFileEntry entry, string? searchText, Regex? regex)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        if (regex != null)
        {
            return regex.IsMatch(entry.RelativePath) ||
                   regex.IsMatch(entry.Name) ||
                   entry.Metadata.Tags.Any(t => regex.IsMatch(t));
        }

        var q = searchText.Trim();
        return entry.RelativePath.Contains(q, StringComparison.OrdinalIgnoreCase) ||
               entry.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
               entry.Metadata.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase));
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

        if (!isFile && !combined.EndsWith(Path.DirectorySeparatorChar.ToString()) && Directory.Exists(combined) == false)
        {
            return combined;
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

    // Moved to Match3.Editor.ViewModels
    // public sealed record ScenarioFileEntry(string RelativePath, string Name, ScenarioMetadata Metadata, long SizeBytes);
    // public sealed record ScenarioFolderNode(string Name, string RelativePath, IReadOnlyList<ScenarioFolderNode> Folders, IReadOnlyList<ScenarioFileEntry> Files);

