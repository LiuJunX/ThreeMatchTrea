using System.Runtime.CompilerServices;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Random;

namespace Match3.Core.Systems.Physics;

/// <summary>
/// Single-cell gravity target resolver with dead-zone diagonal slide support.
/// Each call returns at most one cell of movement. The physics system uses
/// <see cref="PeekNextMove"/> for one-frame lookahead snap decisions.
/// </summary>
public sealed class GravityTargetResolver : IGravityTargetResolver
{
    private readonly IRandom _random;
    private bool[] _reserved;
    private int _reservedWidth;

    public GravityTargetResolver(IRandom random)
    {
        _random = random;
        _reserved = System.Array.Empty<bool>();
    }

    /// <inheritdoc />
    public void ClearReservations()
    {
        System.Array.Clear(_reserved, 0, _reserved.Length);
    }

    /// <inheritdoc />
    public IGravityTargetResolver.TargetInfo DetermineTarget(ref GameState state, int x, int y)
    {
        EnsureReservedCapacity(state);

        int checkY = SkipHolesBelow(in state, x, y);
        if (checkY >= state.Height)
            return new IGravityTargetResolver.TargetInfo(x, y);

        // ① Single-cell vertical
        if (IsCellAvailable(ref state, x, checkY) && !IsReserved(x, checkY))
        {
            Reserve(x, checkY);
            return new IGravityTargetResolver.TargetInfo(x, checkY);
        }

        // ② Obstacle — try diagonal to dead zone
        if (state.HasObstacle(x, checkY))
        {
            var diag = FindDiagonalTarget(ref state, x, checkY, y);
            if (diag.HasValue)
                return diag.Value;
            return new IGravityTargetResolver.TargetInfo(x, y);
        }

        // ③ Tile blocking — try diagonal to dead zone
        if (state.GetTile(x, checkY).Type != ElementType.None)
        {
            var diag = FindDiagonalTarget(ref state, x, checkY, y);
            if (diag.HasValue)
                return diag.Value;
        }

        // ④ Temporary CellLock on empty cell — wait
        return new IGravityTargetResolver.TargetInfo(x, y);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Peek is a read-only query for presentation-layer decisions (e.g. whether to
    /// preserve velocity through a cell). Callers must NOT use the result to modify
    /// data that affects simulation logic — Grid positions, CellLocks, or tile lifecycle.
    /// Only Position/Velocity (visual state) should be influenced by peek results.
    /// </remarks>
    public NextMoveType PeekNextMove(ref GameState state, int x, int y)
    {
        EnsureReservedCapacity(state);
        int checkY = SkipHolesBelow(in state, x, y);
        if (checkY >= state.Height)
            return NextMoveType.Stop;

        // Can move down?
        if (IsCellAvailable(ref state, x, checkY) && !IsReserved(x, checkY))
            return NextMoveType.Vertical;

        // Blocked — would diagonal to dead zone be possible?
        if (state.HasObstacle(x, checkY) ||
            state.GetTile(x, checkY).Type != ElementType.None)
        {
            if (x > 0 && IsCellAvailable(ref state, x - 1, checkY)
                      && !IsReserved(x - 1, checkY)
                      && !HasTileAt(ref state, x - 1, y)
                      && IsDeadZone(ref state, x - 1, checkY))
                return NextMoveType.Diagonal;
            if (x < state.Width - 1 && IsCellAvailable(ref state, x + 1, checkY)
                                    && !IsReserved(x + 1, checkY)
                                    && !HasTileAt(ref state, x + 1, y)
                                    && IsDeadZone(ref state, x + 1, checkY))
                return NextMoveType.Diagonal;
        }

        return NextMoveType.Stop;
    }

    private IGravityTargetResolver.TargetInfo? FindDiagonalTarget(
        ref GameState state, int x, int checkY, int originalY)
    {
        bool canLeft = x > 0 &&
                       IsCellAvailable(ref state, x - 1, checkY) &&
                       !IsReserved(x - 1, checkY) &&
                       !HasTileAt(ref state, x - 1, originalY) &&
                       IsDeadZone(ref state, x - 1, checkY);

        bool canRight = x < state.Width - 1 &&
                        IsCellAvailable(ref state, x + 1, checkY) &&
                        !IsReserved(x + 1, checkY) &&
                        !HasTileAt(ref state, x + 1, originalY) &&
                        IsDeadZone(ref state, x + 1, checkY);

        int targetX = -1;

        if (canLeft && canRight)
        {
            // Random 50/50. Safe because IsSliding prevents re-evaluation mid-slide.
            targetX = _random.Next(0, 2) == 0 ? x - 1 : x + 1;
        }
        else if (canLeft)
            targetX = x - 1;
        else if (canRight)
            targetX = x + 1;

        if (targetX != -1)
        {
            Reserve(targetX, checkY);
            return new IGravityTargetResolver.TargetInfo(targetX, checkY);
        }

        return null;
    }

    #region Cell Queries

    /// <summary>
    /// Whether the cell is structurally available (empty, no obstacle, can receive).
    /// Does NOT check reservations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCellAvailable(ref GameState state, int x, int y)
    {
        return state.IsValid(x, y) &&
               !state.IsHole(x, y) &&
               !state.HasObstacle(x, y) &&
               state.GetTile(x, y).Type == ElementType.None &&
               state.CanReceive(x, y);
    }

    /// <summary>
    /// Whether there is any tile at (x, y) that could compete for a diagonal slot.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasTileAt(ref GameState state, int x, int y)
    {
        return state.GetTile(x, y).Type != ElementType.None;
    }

    /// <summary>
    /// Returns true if the cell at (x, y) cannot be reached by refill from above.
    /// </summary>
    private static bool IsDeadZone(ref GameState state, int x, int y)
    {
        for (int row = y - 1; row >= 0; row--)
        {
            if (state.IsHole(x, row))
                continue;

            if (state.HasObstacle(x, row))
                return true;

            var tile = state.GetTile(x, row);
            if (tile.Type != ElementType.None)
                return !state.CanMoveIgnoringLocks(x, row);
        }

        return false;
    }

    /// <summary>
    /// Skip past holes below (x, y), returning the first non-hole row.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SkipHolesBelow(in GameState state, int x, int y)
    {
        int checkY = y + 1;
        while (checkY < state.Height && state.IsHole(x, checkY))
            checkY++;
        return checkY;
    }

    #endregion

    #region Reservation (bool[] for hot-path performance)

    private void EnsureReservedCapacity(GameState state)
    {
        int size = state.Width * state.Height;
        if (_reserved.Length < size)
        {
            _reserved = new bool[size];
            _reservedWidth = state.Width;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Reserve(int x, int y)
    {
        _reserved[y * _reservedWidth + x] = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsReserved(int x, int y)
    {
        return _reserved[y * _reservedWidth + x];
    }

    #endregion

    /// <summary>
    /// Find the exit Y coordinate of a hole zone starting at or below entryY.
    /// Returns -1 if no hole zone exists at entryY.
    /// </summary>
    public static int FindHoleZoneExit(in GameState state, int x, int entryY)
    {
        if (!state.IsHole(x, entryY)) return -1;

        int exitY = entryY;
        for (int y = entryY + 1; y < state.Height; y++)
        {
            if (state.IsHole(x, y))
                exitY = y;
            else
                break;
        }
        return exitY;
    }
}
