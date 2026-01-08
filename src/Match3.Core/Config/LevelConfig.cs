using System;
using Match3.Core;

namespace Match3.Core.Config;

[Serializable]
public class LevelConfig
{
    public int Width { get; set; } = 8;
    public int Height { get; set; } = 8;
    public TileType[] Grid { get; set; }
    public int MoveLimit { get; set; } = 20;

    public LevelConfig()
    {
        Grid = new TileType[Width * Height];
    }

    public LevelConfig(int width, int height)
    {
        Width = width;
        Height = height;
        Grid = new TileType[width * height];
    }
}
