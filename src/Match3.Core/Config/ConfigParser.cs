using System;
using System.Collections.Generic;
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
        Converters = { new JsonStringEnumConverter(), new ByteArrayFlexConverter() }
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
            Converters = { new JsonStringEnumConverter(), new ByteArrayFlexConverter() }
        });
    }
}

/// <summary>
/// Reads byte[] from either a JSON int array [0,1,2,...] or a base64 string.
/// Writes as int array for human readability.
/// </summary>
internal sealed class ByteArrayFlexConverter : JsonConverter<byte[]>
{
    public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
            return reader.GetBytesFromBase64();

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<byte>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    return list.ToArray();
                if (reader.TokenType == JsonTokenType.Number)
                    list.Add((byte)reader.GetInt32());
            }
        }

        throw new JsonException($"Cannot convert token {reader.TokenType} to byte[]");
    }

    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var b in value)
            writer.WriteNumberValue(b);
        writer.WriteEndArray();
    }
}
