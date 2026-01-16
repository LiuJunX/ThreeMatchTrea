using System;
using Match3.Core.Models.Enums;

namespace Match3.Core.Config;

[Serializable]
public class LevelConfig
{
    public int Width { get; set; } = 8;
    public int Height { get; set; } = 8;
    public TileType[] Grid { get; set; }
    public BombType[] Bombs { get; set; }

    /// <summary>
    /// Ground layer configuration.
    /// </summary>
    public GroundType[] Grounds { get; set; }

    /// <summary>
    /// Ground health values (optional, defaults to type's default health).
    /// </summary>
    public byte[] GroundHealths { get; set; }

    /// <summary>
    /// Cover layer configuration.
    /// </summary>
    public CoverType[] Covers { get; set; }

    /// <summary>
    /// Cover health values (optional, defaults to type's default health).
    /// </summary>
    public byte[] CoverHealths { get; set; }

    public int MoveLimit { get; set; } = 20;

    /// <summary>
    /// Target difficulty for spawn model (0.0 = easy, 1.0 = hard).
    /// Default 0.5 for medium difficulty.
    /// </summary>
    public float TargetDifficulty { get; set; } = 0.5f;

    public LevelConfig()
    {
        var size = Width * Height;
        Grid = new TileType[size];
        Bombs = new BombType[size];
        Grounds = new GroundType[size];
        GroundHealths = new byte[size];
        Covers = new CoverType[size];
        CoverHealths = new byte[size];
    }

    public LevelConfig(int width, int height)
    {
        Width = width;
        Height = height;
        var size = width * height;
        Grid = new TileType[size];
        Bombs = new BombType[size];
        Grounds = new GroundType[size];
        GroundHealths = new byte[size];
        Covers = new CoverType[size];
        CoverHealths = new byte[size];
    }
}
