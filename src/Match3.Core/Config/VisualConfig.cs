using System;
using System.Collections.Generic;

namespace Match3.Core.Config;

/// <summary>
/// Visual configuration for rendering (colors, animation timings).
/// Platform-agnostic - uses hex strings for colors.
/// </summary>
[Serializable]
public class VisualConfig
{
    /// <summary>
    /// TileType name to hex color mapping (e.g., "Red" -> "#E63333").
    /// </summary>
    public Dictionary<string, string> TileColors { get; set; } = new();

    /// <summary>
    /// Bomb ElementType name to hex color with alpha (e.g., "HorizontalRocket" -> "#FFFFFFE6").
    /// </summary>
    public Dictionary<string, string> BombIndicatorColors { get; set; } = new();

    /// <summary>
    /// Camera/scene background color in hex (e.g., "#262633").
    /// </summary>
    public string BackgroundColor { get; set; } = "#262633";
}

/// <summary>
/// Animation timing configuration.
/// </summary>
[Serializable]
public class AnimationConfig
{
    /// <summary>Duration of swap animation in seconds.</summary>
    public float SwapDuration { get; set; } = 0.15f;

    /// <summary>Base duration of fall animation in seconds.</summary>
    public float FallDuration { get; set; } = 0.3f;

    /// <summary>Duration of match highlight flash.</summary>
    public float MatchFlashDuration { get; set; } = 0.1f;

    /// <summary>Duration of tile destruction animation.</summary>
    public float DestroyDuration { get; set; } = 0.2f;

    /// <summary>Delay between spawning new tiles.</summary>
    public float SpawnDelay { get; set; } = 0.05f;

    /// <summary>Delay between cascade matches.</summary>
    public float CascadeDelay { get; set; } = 0.1f;
}

/// <summary>
/// Object pool size configuration.
/// </summary>
[Serializable]
public class PoolSizeConfig
{
    public int Initial { get; set; }
    public int Max { get; set; }
}

/// <summary>
/// JSON DTO for game configuration.
/// Use <see cref="ToMatch3Config"/> to convert to runtime config.
/// </summary>
/// <remarks>
/// Relationship with Match3Config:
/// - GameConfigExtended: JSON deserialization DTO, loaded from config/game/match3.json
/// - Match3Config: Runtime configuration used by game engine
/// </remarks>
[Serializable]
public class GameConfigExtended
{
    public int DefaultWidth { get; set; } = 8;
    public int DefaultHeight { get; set; } = 8;
    public int TileTypesCount { get; set; } = 6;

    public PhysicsConfig Physics { get; set; } = new();
    public Dictionary<string, PoolSizeConfig> PoolSizes { get; set; } = new();

    /// <summary>
    /// Convert to runtime Match3Config.
    /// </summary>
    public Match3Config ToMatch3Config()
    {
        return new Match3Config(DefaultWidth, DefaultHeight, TileTypesCount)
        {
            SwapSpeed = Physics.SwapSpeed,
            InitialFallSpeed = Physics.InitialFallSpeed,
            GravitySpeed = Physics.GravityAcceleration,
            MaxFallSpeed = Physics.MaxFallSpeed
        };
    }
}

/// <summary>
/// Physics/gravity configuration (JSON DTO).
/// </summary>
[Serializable]
public class PhysicsConfig
{
    public float SwapSpeed { get; set; } = 13.0f;
    public float InitialFallSpeed { get; set; } = 6.0f;
    public float GravityAcceleration { get; set; } = 16.0f;
    public float MaxFallSpeed { get; set; } = 13.0f;
}
