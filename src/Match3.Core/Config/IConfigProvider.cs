namespace Match3.Core.Config;

/// <summary>
/// Provides access to game configuration.
/// Implementations handle platform-specific loading (file system, resources, etc.).
/// </summary>
public interface IConfigProvider
{
    /// <summary>
    /// Get visual configuration (colors, etc.).
    /// </summary>
    VisualConfig GetVisualConfig();

    /// <summary>
    /// Get animation timing configuration.
    /// </summary>
    AnimationConfig GetAnimationConfig();

    /// <summary>
    /// Get extended game configuration (JSON DTO).
    /// For runtime config, use <see cref="ConfigProviderExtensions.GetMatch3Config"/>.
    /// </summary>
    GameConfigExtended GetGameConfig();

    /// <summary>
    /// Get level configuration by ID.
    /// </summary>
    LevelConfig GetLevelConfig(string levelId);

    /// <summary>
    /// List all available level IDs.
    /// </summary>
    string[] GetLevelIds();

    /// <summary>
    /// Get the level progression blueprint.
    /// </summary>
    ProgressionBlueprint GetProgressionBlueprint();
}

/// <summary>
/// Extension methods for IConfigProvider.
/// </summary>
public static class ConfigProviderExtensions
{
    /// <summary>
    /// Get runtime Match3Config from configuration.
    /// </summary>
    public static Match3Config GetMatch3Config(this IConfigProvider provider)
    {
        return provider.GetGameConfig().ToMatch3Config();
    }
}
