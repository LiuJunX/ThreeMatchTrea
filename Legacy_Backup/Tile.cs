using System.Numerics;
using Match3.Core.Models.Enums;

namespace Match3.Core.Models.Grid;

public struct Tile
{
    public TileType Type;
    public BombType Bomb;
    public Vector2 Position; // Visual position (World Space)
    public long Id;

    public Tile(long id, TileType type, int x, int y, BombType bomb = BombType.None)
    {
        Id = id;
        Type = type;
        Bomb = bomb;
        Position = new Vector2(x, y);
    }
    
    public Tile(long id, TileType type, Vector2 position, BombType bomb = BombType.None)
    {
        Id = id;
        Type = type;
        Bomb = bomb;
        Position = position;
    }
}
