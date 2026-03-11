using Match3.Core.Analysis;
using Match3.Editor.ViewModels;

namespace Match3.Editor.Interfaces;

/// <summary>
/// Service for managing level files in the file system.
/// </summary>
public interface ILevelService
{
    /// <summary>
    /// Builds a tree structure of the level folder.
    /// </summary>
    ScenarioFolderNode BuildTree();

    /// <summary>
    /// Reads a level JSON file.
    /// </summary>
    string ReadLevelJson(string relativePath);

    /// <summary>
    /// Writes a level JSON file.
    /// </summary>
    void WriteLevelJson(string relativePath, string json);

    /// <summary>
    /// Creates a new level file.
    /// </summary>
    string CreateNewLevel(string folderRelativePath, string levelName, string json);

    /// <summary>
    /// Creates a new folder.
    /// </summary>
    string CreateFolder(string parentFolderRelativePath, string folderName);

    /// <summary>
    /// Duplicates a level file.
    /// </summary>
    string DuplicateLevel(string sourceRelativePath, string newLevelName);

    /// <summary>
    /// Deletes a level file.
    /// </summary>
    void DeleteLevel(string relativePath);

    /// <summary>
    /// Deletes a folder.
    /// </summary>
    void DeleteFolder(string relativePath);

    /// <summary>
    /// Renames a level file.
    /// </summary>
    void RenameLevel(string relativePath, string newName);

    /// <summary>
    /// Renames a folder.
    /// </summary>
    void RenameFolder(string relativePath, string newName);

    /// <summary>
    /// Reads analysis snapshot for a level.
    /// Returns null if no analysis file exists.
    /// </summary>
    /// <param name="levelRelativePath">Level file path (e.g., "levels/Level1.json")</param>
    LevelAnalysisSnapshot? ReadAnalysisSnapshot(string levelRelativePath);

    /// <summary>
    /// Writes analysis snapshot for a level.
    /// Creates {levelName}.analysis.json alongside the level file.
    /// </summary>
    /// <param name="levelRelativePath">Level file path</param>
    /// <param name="snapshot">Analysis snapshot to save</param>
    void WriteAnalysisSnapshot(string levelRelativePath, LevelAnalysisSnapshot snapshot);

    /// <summary>
    /// Gets the analysis file path for a level.
    /// </summary>
    string GetAnalysisFilePath(string levelRelativePath);
}
