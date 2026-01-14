using System;

namespace Match3.Core.Models.Grid;

public struct Position : IEquatable<Position>
{
    public int X;
    public int Y;

    public Position(int x, int y)
    {
        X = x;
        Y = y;
    }

    public bool Equals(Position other)
    {
        return X == other.X && Y == other.Y;
    }

    public override bool Equals(object? obj)
    {
        return obj is Position other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    public static bool operator ==(Position left, Position right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Position left, Position right)
    {
        return !left.Equals(right);
    }

    public static Position Invalid => new Position(-1, -1);
    public bool IsValid => X >= 0 && Y >= 0;

    public Position GetNeighbor(Match3.Core.Models.Enums.Direction direction)
    {
        return direction switch
        {
            Match3.Core.Models.Enums.Direction.Up => new Position(X, Y - 1),
            Match3.Core.Models.Enums.Direction.Down => new Position(X, Y + 1),
            Match3.Core.Models.Enums.Direction.Left => new Position(X - 1, Y),
            Match3.Core.Models.Enums.Direction.Right => new Position(X + 1, Y),
            _ => this
        };
    }

    public override string ToString() => $"({X}, {Y})";
}
