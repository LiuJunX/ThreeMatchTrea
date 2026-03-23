using Match3.Core.Models.Enums;

namespace Match3.Core.Models.Grid;

/// <summary>
/// Represents a static obstacle element occupying a cell.
/// Obstacles are the cell's primary content — they replace tiles
/// and have complex multi-stage HP, conditional destruction, and death effects.
/// <para>Size: 3 bytes. Blittable. Clone via Array.Copy.</para>
/// </summary>
public struct Obstacle
{
    /// <summary>
    /// The type of obstacle. None means no obstacle at this position.
    /// </summary>
    public ObstacleType Type;

    /// <summary>
    /// Current stage / hit-points (e.g., Box 4→3→2→1→0).
    /// 0 = destroyed or no obstacle. Max 255.
    /// </summary>
    public byte Stage;

    /// <summary>
    /// Internal state counter. Usage depends on obstacle type:
    /// <list type="bullet">
    ///   <item>ColorBox: color variant (cast of ElementType, e.g., 1=Red)</item>
    ///   <item>MagicHat: accumulated adjacent hit count</item>
    ///   <item>Curtain: color variant (cast of ElementType)</item>
    ///   <item>Mailbox: unused (0) — permanent generator, never decremented</item>
    ///   <item>PotionBottle: bitmask of remaining sub-bottles (bit N = Item(N+1) present)</item>
    ///   <item>Others: unused (0)</item>
    /// </list>
    /// </summary>
    public byte State;

    /// <summary>Returns true if this position has an obstacle.</summary>
    public readonly bool HasObstacle => Type != ObstacleType.None;

    /// <summary>An empty obstacle (no obstacle element).</summary>
    public static Obstacle Empty => default;

    public Obstacle(ObstacleType type, byte stage, byte state = 0)
    {
        Type = type;
        Stage = stage;
        State = state;
    }
}
