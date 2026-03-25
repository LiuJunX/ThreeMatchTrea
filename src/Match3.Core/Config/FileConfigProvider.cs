using System;
using System.Collections.Generic;
using System.Linq;

namespace Match3.Core.Config;

/// <summary>
/// File-based configuration provider.
/// Uses a delegate for file reading to support different platforms.
/// </summary>
public class FileConfigProvider : IConfigProvider
{
    private readonly Func<string, string> _readFile;
    private readonly Func<string, string[]> _listFiles;
    private readonly string _configRoot;

    // Cached configs
    private VisualConfig? _visualConfig;
    private AnimationConfig? _animationConfig;
    private GameConfigExtended? _gameConfig;
    private ProgressionBlueprint? _progressionBlueprint;
    private readonly Dictionary<string, LevelConfig> _levelConfigs = new();

    /// <summary>
    /// Create a file-based config provider.
    /// </summary>
    /// <param name="configRoot">Root path of config directory.</param>
    /// <param name="readFile">Function to read file content (path -> content).</param>
    /// <param name="listFiles">Function to list files in directory (path -> file names).</param>
    public FileConfigProvider(
        string configRoot,
        Func<string, string> readFile,
        Func<string, string[]> listFiles)
    {
        _configRoot = configRoot;
        _readFile = readFile;
        _listFiles = listFiles;
    }

    public VisualConfig GetVisualConfig()
    {
        if (_visualConfig == null)
        {
            var path = CombinePath(_configRoot, "visual", "colors.json");
            var json = _readFile(path);
            _visualConfig = ConfigParser.ParseVisualConfig(json);
        }
        return _visualConfig;
    }

    public AnimationConfig GetAnimationConfig()
    {
        if (_animationConfig == null)
        {
            var path = CombinePath(_configRoot, "visual", "animation.json");
            var json = _readFile(path);
            _animationConfig = ConfigParser.ParseAnimationConfig(json);
        }
        return _animationConfig;
    }

    public GameConfigExtended GetGameConfig()
    {
        if (_gameConfig == null)
        {
            var path = CombinePath(_configRoot, "game", "match3.json");
            var json = _readFile(path);
            _gameConfig = ConfigParser.ParseGameConfig(json);
        }
        return _gameConfig;
    }

    public LevelConfig GetLevelConfig(string levelId)
    {
        if (!_levelConfigs.TryGetValue(levelId, out var config))
        {
            var path = CombinePath(_configRoot, "levels", $"{levelId}.json");
            var json = _readFile(path);
            config = ConfigParser.ParseLevelConfig(json);
            _levelConfigs[levelId] = config;
        }
        return config;
    }

    public ProgressionBlueprint GetProgressionBlueprint()
    {
        if (_progressionBlueprint == null)
        {
            var path = CombinePath(_configRoot, "progression_blueprint.json");
            var json = _readFile(path);
            _progressionBlueprint = ConfigParser.ParseProgressionBlueprint(json);
        }
        return _progressionBlueprint;
    }

    public string[] GetLevelIds()
    {
        var levelsPath = CombinePath(_configRoot, "levels");
        var files = _listFiles(levelsPath);
        return files
            .Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Select(f => f.Substring(0, f.Length - 5)) // Remove .json
            .ToArray();
    }

    /// <summary>
    /// Clear cached configurations.
    /// Call this to reload configs from disk.
    /// </summary>
    public void ClearCache()
    {
        _visualConfig = null;
        _animationConfig = null;
        _gameConfig = null;
        _progressionBlueprint = null;
        _levelConfigs.Clear();
    }

    private static string CombinePath(params string[] parts)
    {
        return string.Join("/", parts);
    }
}
