namespace Match3.Core.Config;

/// <summary>
/// Configuration settings for the Match3 game logic.
/// </summary>
public class Match3Config
{
    public int Width { get; set; } = 8;
    public int Height { get; set; } = 8;
    public int TileTypesCount { get; set; } = 6;
    
    // Animation speeds (visual/logical update speeds)
    public float SwapSpeed { get; set; } = 15.0f;
    public float GravitySpeed { get; set; } = 165.0f;
    public float MaxFallSpeed { get; set; } = 40.0f;

    // Logic Flags
    public bool IsGravityEnabled { get; set; } = true;

    public Match3Config(int width, int height, int tileTypesCount)
    {
        Width = width;
        Height = height;
        TileTypesCount = tileTypesCount;
    }

    public Match3Config() { }
}
