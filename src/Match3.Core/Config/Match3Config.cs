namespace Match3.Core.Config;

/// <summary>
/// Configuration settings for the Match3 game logic.
/// </summary>
public class Match3Config
{
    #region Default Values

    public const int DefaultWidth = 8;
    public const int DefaultHeight = 8;
    public const int DefaultTileTypesCount = 6;
    public const float DefaultSwapSpeed = 15.0f;
    public const float DefaultInitialFallSpeed = 8.0f;
    public const float DefaultGravitySpeed = 20.0f;
    public const float DefaultMaxFallSpeed = 25.0f;

    #endregion

    #region Grid Settings

    public int Width { get; set; } = DefaultWidth;
    public int Height { get; set; } = DefaultHeight;
    public int TileTypesCount { get; set; } = DefaultTileTypesCount;

    #endregion

    #region Animation Settings

    /// <summary>
    /// Speed of tile swap animation (units per second).
    /// </summary>
    public float SwapSpeed { get; set; } = DefaultSwapSpeed;

    #endregion

    #region Gravity Settings

    /// <summary>
    /// Initial fall speed when a tile starts falling (units per second).
    /// </summary>
    public float InitialFallSpeed { get; set; } = DefaultInitialFallSpeed;

    /// <summary>
    /// Gravity acceleration (units per second squared).
    /// </summary>
    public float GravitySpeed { get; set; } = DefaultGravitySpeed;

    /// <summary>
    /// Maximum fall speed cap (units per second).
    /// </summary>
    public float MaxFallSpeed { get; set; } = DefaultMaxFallSpeed;

    #endregion

    #region Logic Flags

    public bool IsGravityEnabled { get; set; } = true;

    #endregion

    #region Constructors

    public Match3Config(int width, int height, int tileTypesCount)
    {
        Width = width;
        Height = height;
        TileTypesCount = tileTypesCount;
    }

    public Match3Config() { }

    #endregion
}
