using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Physics;

/// <summary>
/// What a tile would do from a given position.
/// Used by physics snap logic to decide whether to preserve velocity/overshoot.
/// </summary>
public enum NextMoveType
{
    /// <summary>No further movement — tile stops here.</summary>
    Stop,
    /// <summary>Tile can fall vertically to the next cell.</summary>
    Vertical,
    /// <summary>Tile can slide diagonally to an adjacent dead-zone cell.</summary>
    Diagonal
}

/// <summary>
/// Interface for resolving gravity targets for falling tiles.
/// Targets are always single-cell (one cell at a time).
/// </summary>
public interface IGravityTargetResolver
{
    /// <summary>
    /// Target information for a tile's single-cell movement.
    /// </summary>
    public readonly struct TargetInfo
    {
        /// <summary>Target column (always one cell away from current grid position).</summary>
        public readonly int X;

        /// <summary>Target row.</summary>
        public readonly int Y;

        public TargetInfo(int x, int y) { X = x; Y = y; }
    }

    /// <summary>
    /// Determine the single-cell target for a tile at the given grid position.
    /// Returns the tile's current position if it cannot move.
    /// </summary>
    TargetInfo DetermineTarget(ref GameState state, int x, int y);

    /// <summary>
    /// Lightweight lookahead: what would a tile at (x, y) do next?
    /// Does not require a tile to exist at the position.
    /// Does not modify reservations or any state.
    /// </summary>
    NextMoveType PeekNextMove(ref GameState state, int x, int y);

    /// <summary>
    /// Clear any reserved slots at the start of a new frame.
    /// </summary>
    void ClearReservations();
}
