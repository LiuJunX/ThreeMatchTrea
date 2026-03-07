using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Random;

namespace Match3.Core.Systems.Physics;

/// <summary>
/// Default implementation of IGravityTargetResolver.
/// Resolves gravity targets for falling tiles with diagonal slide support.
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
        var currentTile = state.GetTile(x, y);

        // Skip past holes below current position
        int checkY = y + 1;
        while (checkY < state.Height && state.IsHole(x, checkY))
            checkY++;

        // 1. Try Vertical Move
        if (checkY < state.Height)
        {
            if (CanMoveTo(ref state, x, checkY))
            {
                return GuardHoleTransit(
                    FindLowestVerticalTarget(ref state, x, checkY),
                    currentTile, y);
            }

            // 2. Check if blocked by a falling tile (follow it)
            var tileBelow = state.GetTile(x, checkY);
            if (tileBelow.Type != ElementType.None && tileBelow.IsFalling)
            {
                // If current tile is already falling (chasing from above), follow immediately
                // Otherwise, wait until the tile below has cleared the midpoint
                bool shouldFollow = currentTile.IsFalling ||
                                    tileBelow.Position.Y >= checkY + 0.5f;

                if (shouldFollow)
                {
                    // Follow the falling tile below
                    float targetY = tileBelow.Position.Y - 1.0f;

                    // Clamp follow target above any hole zone in this column.
                    // Prevents follower from entering the hole zone where it could
                    // get stuck if the leader lands and blocks the exit.
                    targetY = ClampAboveHoleZone(ref state, x, y, targetY);

                    // If clamped target is at or above current row, treat as blocked
                    if (targetY <= y)
                    {
                        return GuardHoleTransit(
                            new IGravityTargetResolver.TargetInfo(new Vector2(x, y), 0f, false),
                            currentTile, y);
                    }

                    return GuardHoleTransit(
                        new IGravityTargetResolver.TargetInfo(
                            new Vector2(x, targetY),
                            tileBelow.Velocity.Y,
                            foundDynamicTarget: true),
                        currentTile, y);
                }
                // Tile below is falling but hasn't cleared the cell yet - stay put
                return GuardHoleTransit(
                    new IGravityTargetResolver.TargetInfo(new Vector2(x, y), 0f, false),
                    currentTile, y);
            }

            // 3. Try Diagonal Slide
            if (tileBelow.IsSuspended)
            {
                return GuardHoleTransit(
                    FindDiagonalTarget(ref state, x, checkY, y),
                    currentTile, y);
            }

            // If blocked by a normal tile, stay put.
            return GuardHoleTransit(
                new IGravityTargetResolver.TargetInfo(new Vector2(x, y), 0f, false),
                currentTile, y);
        }

        // Bottom of grid (or only holes below)
        return GuardHoleTransit(
            new IGravityTargetResolver.TargetInfo(new Vector2(x, y), 0f, false),
            currentTile, y);
    }

    /// <summary>
    /// Prevent hole-transit tiles from being pulled backward (upward).
    /// During hole transit, a tile's physical Position.Y is far past its grid row
    /// because UpdateGridPosition freezes the grid slot while traversing holes.
    /// Without this guard, targets based on the grid row would snap the tile backward.
    /// </summary>
    private static IGravityTargetResolver.TargetInfo GuardHoleTransit(
        IGravityTargetResolver.TargetInfo target, Tile tile, int gridY)
    {
        // Only activate when tile has physically moved well past its grid row (hole transit)
        if (tile.Position.Y <= gridY + 1.0f) return target;

        // Target is at or below current position — forward motion, no issue
        if (target.Position.Y >= tile.Position.Y) return target;

        // Tile is stuck in hole zone (zero velocity, not falling):
        // allow backward movement so it can exit the hole zone.
        // This handles the case where the exit became blocked after the tile entered.
        if (!tile.IsFalling && System.Math.Abs(tile.Velocity.Y) < 0.1f)
            return target;

        // Target would pull the tile backward — hold at current physical position.
        // The tile decelerates and waits until a forward target becomes available
        // (e.g., the blocking tile below clears).
        return new IGravityTargetResolver.TargetInfo(
            new Vector2(target.Position.X, tile.Position.Y), 0f, false);
    }

    /// <summary>
    /// Clamp a follow target Y so it stays above any hole zone in the column.
    /// Returns the clamped targetY.
    /// </summary>
    private static float ClampAboveHoleZone(ref GameState state, int x, int currentY, float targetY)
    {
        for (int h = currentY + 1; h < state.Height; h++)
        {
            if (state.IsHole(x, h))
            {
                // Clamp to the cell just before the hole entry
                return System.Math.Min(targetY, h - 1.0f);
            }
        }
        return targetY;
    }

    private IGravityTargetResolver.TargetInfo FindLowestVerticalTarget(ref GameState state, int x, int startY)
    {
        int floorY = startY;

        for (int k = startY + 1; k < state.Height; k++)
        {
            // Skip holes — tiles pass through them
            if (state.IsHole(x, k)) continue;

            if (CanMoveTo(ref state, x, k))
            {
                floorY = k;
            }
            else
            {
                break;
            }
        }

        ReserveSlot(x, floorY, state.Width);
        return new IGravityTargetResolver.TargetInfo(new Vector2(x, floorY), 0f, false);
    }

    private IGravityTargetResolver.TargetInfo FindDiagonalTarget(ref GameState state, int x, int checkY, int originalY)
    {
        bool canLeft = x > 0 && CanMoveTo(ref state, x - 1, checkY) && IsOverheadClear(ref state, x - 1, originalY);
        bool canRight = x < state.Width - 1 && CanMoveTo(ref state, x + 1, checkY) && IsOverheadClear(ref state, x + 1, originalY);

        int targetX = -1;

        if (canLeft && canRight)
        {
            targetX = _random.Next(0, 2) == 0 ? x - 1 : x + 1;
        }
        else if (canLeft)
        {
            targetX = x - 1;
        }
        else if (canRight)
        {
            targetX = x + 1;
        }

        if (targetX != -1)
        {
            ReserveSlot(targetX, checkY, state.Width);
            return new IGravityTargetResolver.TargetInfo(new Vector2(targetX, checkY), 0f, false);
        }

        return new IGravityTargetResolver.TargetInfo(new Vector2(x, originalY), 0f, false);
    }

    private bool CanMoveTo(ref GameState state, int x, int y)
    {
        return IsInsideGrid(state, x, y) &&
               !state.IsHole(x, y) &&
               state.GetTile(x, y).Type == ElementType.None &&
               state.CanReceive(x, y) &&
               !IsReserved(x, y, state.Width);
    }

    private bool IsOverheadClear(ref GameState state, int targetX, int targetY)
    {
        return state.GetTile(targetX, targetY).Type == ElementType.None;
    }

    private static bool IsInsideGrid(GameState state, int x, int y)
    {
        return x >= 0 && x < state.Width && y >= 0 && y < state.Height;
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
    /// Find the exit Y coordinate of a hole zone starting at or below entryY in the given column.
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
