using System.Numerics;

namespace Match3.Presentation;

/// <summary>
/// Interface for visual state that animations can modify.
/// </summary>
public interface IVisualState
{
    /// <summary>
    /// Set the visual position of a tile.
    /// </summary>
    void SetTilePosition(int tileId, Vector2 position);

    /// <summary>
    /// Set the visual scale of a tile.
    /// </summary>
    void SetTileScale(int tileId, Vector2 scale);

    /// <summary>
    /// Set the visual alpha (opacity) of a tile.
    /// </summary>
    void SetTileAlpha(int tileId, float alpha);

    /// <summary>
    /// Set whether a tile is visible.
    /// </summary>
    void SetTileVisible(int tileId, bool visible);

    /// <summary>
    /// Set the visual rotation angle of a tile (degrees).
    /// </summary>
    void SetTileRotation(int tileId, float rotation);

    /// <summary>
    /// Set the visual position of a projectile.
    /// </summary>
    void SetProjectilePosition(int projectileId, Vector2 position);

    /// <summary>
    /// Set whether a projectile is visible.
    /// </summary>
    void SetProjectileVisible(int projectileId, bool visible);

    /// <summary>
    /// Add a visual effect at a position.
    /// </summary>
    void AddEffect(string effectType, Vector2 position, float duration);
}
