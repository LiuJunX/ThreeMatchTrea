using Match3.Core;

namespace Match3.Core.Structs;

public readonly struct TileMove
{
    public readonly Position From;
    public readonly Position To;

    public TileMove(Position from, Position to)
    {
        From = from;
        To = to;
    }

    public override string ToString() => $"[{From.X},{From.Y}] -> [{To.X},{To.Y}]";
}
