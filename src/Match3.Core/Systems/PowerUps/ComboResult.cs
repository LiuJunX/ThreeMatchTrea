using Match3.Core.Models.Grid;
using Match3.Core.Systems.Projectiles;

namespace Match3.Core.Systems.PowerUps;

/// <summary>
/// Describes the full effect of a bomb combo: area to explode + UFO projectiles to launch.
/// Returned by <see cref="BombComboHandler.TryApplyCombo(ref GameState, Position, Position, System.Collections.Generic.HashSet{Position}, out ComboResult)"/>.
/// </summary>
public readonly struct ComboResult
{
    /// <summary>True if the combo involves two ColorBombs (full screen clear, slow wave).</summary>
    public bool IsDoubleColorBomb { get; init; }

    /// <summary>True if at least one tile is a ColorBomb (affects explosion timing).</summary>
    public bool HasColorBomb { get; init; }

    /// <summary>UFO projectile launch info, if the combo involves UFO. Null otherwise.</summary>
    public UfoLaunchInfo? UfoLaunch { get; init; }
}

/// <summary>
/// Describes what UFO projectiles to launch as part of a combo.
/// </summary>
public readonly struct UfoLaunchInfo
{
    /// <summary>True = UFO+UFO (3 projectiles), False = UFO+other (1 payload projectile).</summary>
    public bool IsUfoUfoCombo { get; init; }

    /// <summary>Tile ID of the first UFO (or the only UFO for payload combos).</summary>
    public int UfoTileId { get; init; }

    /// <summary>Position of the first UFO.</summary>
    public Position UfoPosition { get; init; }

    /// <summary>For UFO+UFO: second UFO tile ID. For payload: passenger tile ID.</summary>
    public int OtherTileId { get; init; }

    /// <summary>For UFO+UFO: second UFO position. For payload: passenger position.</summary>
    public Position OtherPosition { get; init; }

    /// <summary>Payload type for UFO+Rocket/Square combos.</summary>
    public UfoPayload Payload { get; init; }
}
