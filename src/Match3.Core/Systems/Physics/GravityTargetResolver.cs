using System.Collections.Generic;
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
    private readonly HashSet<int> _reservedSlots;

    public GravityTargetResolver(IRandom random)
    {
        _random = random;
        _reservedSlots = new HashSet<int>();
    }

    /// <inheritdoc />
    public void ClearReservations()
    {
        _reservedSlots.Clear();
    }

    /// <inheritdoc />
    public IGravityTargetResolver.TargetInfo DetermineTarget(ref GameState state, int x, int y)
    {
        // Skip past holes below current position
        int checkY = y + 1;
        while (checkY < state.Height && state.IsHole(x, checkY))
            checkY++;

        if (checkY >= state.Height)
            return new IGravityTargetResolver.TargetInfo(x, y);

        // ① Single-cell vertical: target the next cell below
        if (CanMoveTo(ref state, x, checkY))
        {
            ReserveSlot(x, checkY, state.Width);
            return new IGravityTargetResolver.TargetInfo(x, checkY);
        }

        // ② Obstacle — always try diagonal
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
    public NextMoveType PeekNextMove(ref GameState state, int x, int y)
    {
        int checkY = y + 1;
        while (checkY < state.Height && state.IsHole(x, checkY))
            checkY++;

        if (checkY >= state.Height)
            return NextMoveType.Stop;

        // Can move down? (no reservation check — this is a peek)
        if (state.IsValid(x, checkY) &&
            !state.IsHole(x, checkY) &&
            !state.HasObstacle(x, checkY) &&
            state.GetTile(x, checkY).Type == ElementType.None &&
            state.CanReceive(x, checkY))
        {
            return NextMoveType.Vertical;
        }

        // Blocked — would diagonal to dead zone be possible?
        if (state.HasObstacle(x, checkY) ||
            state.GetTile(x, checkY).Type != ElementType.None)
        {
            if (x > 0 && CanPeek(ref state, x - 1, checkY) && IsDeadZone(ref state, x - 1, checkY))
                return NextMoveType.Diagonal;
            if (x < state.Width - 1 && CanPeek(ref state, x + 1, checkY) && IsDeadZone(ref state, x + 1, checkY))
                return NextMoveType.Diagonal;
        }

        return NextMoveType.Stop;
    }

    private IGravityTargetResolver.TargetInfo? FindDiagonalTarget(
        ref GameState state, int x, int checkY, int originalY)
    {
        bool canLeft = x > 0 &&
                       CanMoveTo(ref state, x - 1, checkY) &&
                       !HasCollisionRisk(ref state, x - 1, originalY) &&
                       IsDeadZone(ref state, x - 1, checkY);

        bool canRight = x < state.Width - 1 &&
                        CanMoveTo(ref state, x + 1, checkY) &&
                        !HasCollisionRisk(ref state, x + 1, originalY) &&
                        IsDeadZone(ref state, x + 1, checkY);

        int targetX = -1;

        if (canLeft && canRight)
            targetX = _random.Next(0, 2) == 0 ? x - 1 : x + 1;
        else if (canLeft)
            targetX = x - 1;
        else if (canRight)
            targetX = x + 1;

        if (targetX != -1)
        {
            ReserveSlot(targetX, checkY, state.Width);
            return new IGravityTargetResolver.TargetInfo(targetX, checkY);
        }

        return null;
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
    /// Check if there's a movable tile at (x, y) that could fall into the cell below.
    /// </summary>
    private static bool HasCollisionRisk(ref GameState state, int x, int y)
    {
        var tile = state.GetTile(x, y);
        return tile.Type != ElementType.None;
    }

    private bool CanMoveTo(ref GameState state, int x, int y)
    {
        return state.IsValid(x, y) &&
               !state.IsHole(x, y) &&
               !state.HasObstacle(x, y) &&
               state.GetTile(x, y).Type == ElementType.None &&
               state.CanReceive(x, y) &&
               !IsReserved(x, y, state.Width);
    }

    /// <summary>
    /// CanMoveTo without reservation check (for peek only).
    /// </summary>
    private static bool CanPeek(ref GameState state, int x, int y)
    {
        return state.IsValid(x, y) &&
               !state.IsHole(x, y) &&
               !state.HasObstacle(x, y) &&
               state.GetTile(x, y).Type == ElementType.None &&
               state.CanReceive(x, y);
    }

    private void ReserveSlot(int x, int y, int width)
    {
        _reservedSlots.Add(y * width + x);
    }

    private bool IsReserved(int x, int y, int width)
    {
        return _reservedSlots.Contains(y * width + x);
    }

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
