using Match3.Core.Models.Enums;

namespace Match3.Core.Models.Grid;

/// <summary>
/// Represents a ground element beneath a tile.
/// Ground elements are damaged when the tile above is destroyed.
/// </summary>
public struct Ground
{
    /// <summary>
    /// The type of ground element. None means no ground at this position.
    /// </summary>
    public GroundType Type;

    /// <summary>
    /// Health points. When reduced to 0, the ground is destroyed.
    /// Default is 1 for most ground types.
    /// </summary>
    public byte Health;

    /// <summary>
    /// Creates a new ground element with the specified type and health.
    /// </summary>
    public Ground(GroundType type, byte health = 1)
    {
        Type = type;
        Health = health;
    }

    /// <summary>
    /// Returns true if this position has a ground element.
    /// </summary>
    public readonly bool HasGround => Type != GroundType.None;

    /// <summary>
    /// An empty ground (no ground element).
    /// </summary>
    public static Ground Empty => new(GroundType.None, 0);
}
