using System;

namespace Match3.Core.Primitives
{
    /// <summary>
    /// Represents a 2D integer vector.
    /// Provides basic position functionality for the grid.
    /// </summary>
    public struct Vector2Int : IEquatable<Vector2Int>
    {
        public int X;
        public int Y;

        public Vector2Int(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static Vector2Int One => new Vector2Int(1, 1);
        public static Vector2Int Zero => new Vector2Int(0, 0);

        public override bool Equals(object obj)
        {
            return obj is Vector2Int other && Equals(other);
        }

        public bool Equals(Vector2Int other)
        {
            return X == other.X && Y == other.Y;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        public static bool operator ==(Vector2Int left, Vector2Int right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Vector2Int left, Vector2Int right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"({X}, {Y})";
        }
    }
}
