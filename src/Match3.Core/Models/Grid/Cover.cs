using Match3.Core.Models.Enums;

namespace Match3.Core.Models.Grid;

/// <summary>
/// Represents a cover element above a tile.
/// Cover elements protect the tile from being destroyed until the cover is removed.
/// </summary>
public struct Cover
{
    /// <summary>
    /// The type of cover element. None means no cover at this position.
    /// </summary>
    public CoverType Type;

    /// <summary>
    /// Health points. When reduced to 0, the cover is destroyed.
    /// Default is 1 for most cover types.
    /// </summary>
    public byte Health;

    /// <summary>
    /// If true, the cover moves with the tile (Dynamic).
    /// If false, the cover stays fixed at the grid position (Static).
    /// </summary>
    public bool IsDynamic;

    /// <summary>
    /// Creates a new cover element with the specified type, health, and behavior.
    /// </summary>
    public Cover(CoverType type, byte health = 1, bool isDynamic = false)
    {
        Type = type;
        Health = health;
        IsDynamic = isDynamic;
    }

    /// <summary>
    /// Returns true if this position has a cover element.
    /// </summary>
    public readonly bool HasCover => Type != CoverType.None;

    /// <summary>
    /// An empty cover (no cover element).
    /// </summary>
    public static Cover Empty => new(CoverType.None, 0, false);
}
