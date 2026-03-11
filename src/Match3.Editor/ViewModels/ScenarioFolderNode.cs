using System.Collections.Generic;
using Match3.Core.Scenarios;

namespace Match3.Editor.ViewModels;

public class ScenarioFileEntry
{
    public string RelativePath { get; }
    public string Name { get; }
    public ScenarioMetadata Metadata { get; }
    public long SizeBytes { get; }

    public ScenarioFileEntry(string relativePath, string name, ScenarioMetadata metadata, long sizeBytes)
    {
        RelativePath = relativePath;
        Name = name;
        Metadata = metadata;
        SizeBytes = sizeBytes;
    }
}

public class ScenarioFolderNode
{
    public string Name { get; }
    public string RelativePath { get; }
    public IReadOnlyList<ScenarioFolderNode> Folders { get; }
    public IReadOnlyList<ScenarioFileEntry> Files { get; }

    public ScenarioFolderNode(string name, string relativePath, IReadOnlyList<ScenarioFolderNode> folders, IReadOnlyList<ScenarioFileEntry> files)
    {
        Name = name;
        RelativePath = relativePath;
        Folders = folders;
        Files = files;
    }
}
