namespace Match3.Editor;

/// <summary>
/// Minimal platform abstraction for file IO.
/// The only interface the editor core needs from the host platform.
/// Unity, Web, CLI each provide their own implementation.
/// </summary>
public interface IEditorFileSystem
{
    string ReadText(string path);
    void WriteText(string path, string content);
    bool FileExists(string path);
    string[] ListFiles(string directory, string pattern);
    void CreateDirectory(string path);
    void DeleteFile(string path);

    /// <summary>
    /// Returns the absolute path to the levels directory (e.g. config/levels/).
    /// </summary>
    string GetLevelsDirectory();
}
