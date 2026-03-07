using System.Numerics;
using Match3.Core.Models.Enums;

namespace Match3.Core.Models.Grid;

public struct Tile
{
    public ElementType Type; // Was TileType
    public BombType Bomb;
    public Vector2 Position; // Logic position (World Space), e.g. (3, 4.5)
    public Vector2 Velocity; // Physics velocity
    public TileState State;  // Physics/lifecycle state flags
    public int Id;

    /// <summary>
    /// Whether tile is suspended (gravity ignored). Backward compatible property.
    /// </summary>
    public bool IsSuspended
    {
        get => (State & TileState.Suspended) != 0;
        set => State = value ? State | TileState.Suspended : State & ~TileState.Suspended;
    }

    /// <summary>
    /// Whether tile is currently falling. Backward compatible property.
    /// </summary>
    public bool IsFalling
    {
        get => (State & TileState.Falling) != 0;
        set => State = value ? State | TileState.Falling : State & ~TileState.Falling;
    }

    public Tile(int id, ElementType type, int x, int y, BombType bomb = BombType.None)
    {
        Id = id;
        Type = type;
        Bomb = bomb;
        Position = new Vector2(x, y);
        Velocity = Vector2.Zero;
        State = TileState.None;
    }

    public Tile(int id, ElementType type, Vector2 position, BombType bomb = BombType.None)
    {
        Id = id;
        Type = type;
        Bomb = bomb;
        Position = position;
        Velocity = Vector2.Zero;
        State = TileState.None;
    }
}
