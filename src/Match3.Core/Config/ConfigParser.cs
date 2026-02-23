using System.Text.Json;
using System.Text.Json.Serialization;

namespace Match3.Core.Config;

/// <summary>
/// JSON configuration parser.
/// Platform-agnostic - accepts JSON strings, does not perform IO.
/// </summary>
public static class ConfigParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        IncludeFields = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Parse visual colors configuration from JSON string.
    /// </summary>
    public static VisualConfig ParseVisualConfig(string json)
    {
        return JsonSerializer.Deserialize<VisualConfig>(json, Options)
               ?? new VisualConfig();
    }

    /// <summary>
    /// Parse animation configuration from JSON string.
    /// </summary>
    public static AnimationConfig ParseAnimationConfig(string json)
    {
        return JsonSerializer.Deserialize<AnimationConfig>(json, Options)
               ?? new AnimationConfig();
    }

    /// <summary>
    /// Parse extended game configuration from JSON string.
    /// </summary>
    public static GameConfigExtended ParseGameConfig(string json)
    {
        return JsonSerializer.Deserialize<GameConfigExtended>(json, Options)
               ?? new GameConfigExtended();
    }

    /// <summary>
    /// Parse level configuration from JSON string.
    /// </summary>
    public static LevelConfig ParseLevelConfig(string json)
    {
        return JsonSerializer.Deserialize<LevelConfig>(json, Options)
               ?? new LevelConfig();
    }

    /// <summary>
    /// Serialize configuration to JSON string.
    /// </summary>
    public static string Serialize<T>(T config)
    {
        return JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            IncludeFields = true,
            Converters = { new JsonStringEnumConverter() }
        });
    }
}
