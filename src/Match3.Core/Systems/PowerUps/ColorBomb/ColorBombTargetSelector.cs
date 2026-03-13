using System;
using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.PowerUps.ColorBomb;

/// <summary>
/// Pure functions for ColorBomb target selection: picking the best target color,
/// collecting matching tiles on the board, and shuffling the target list.
/// </summary>
internal static class ColorBombTargetSelector
{
    /// <summary>
    /// Picks the most frequent color on the board, excluding reserved colors,
    /// special tile types, and tiles already locked with <see cref="CellLockType.Targeting"/>.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="reservedColors">Colors already claimed by active sessions.</param>
    /// <returns>The most frequent eligible color, or <see cref="ElementType.None"/> if none found.</returns>
    internal static ElementType PickTargetColor(in GameState state, HashSet<ElementType> reservedColors)
    {
        // Count tiles per color, excluding reserved colors and special types
        Span<int> counts = stackalloc int[7]; // None(0) + Item1-Item6(1-6)

        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var tile = state.GetTile(x, y);
                if (!tile.Type.IsColor()) continue;
                if (reservedColors.Contains(tile.Type)) continue;
                // Skip tiles already locked by another session (Targeting)
                if (state.IsLocked(x, y, CellLockType.Targeting)) continue;

                int idx = (int)tile.Type;
                if (idx >= 1 && idx <= 6)
                    counts[idx]++;
            }
        }

        // Find most frequent
        ElementType best = ElementType.None;
        int bestCount = 0;
        for (int i = 1; i <= 6; i++)
        {
            if (counts[i] > bestCount)
            {
                bestCount = counts[i];
                best = (ElementType)i;
            }
        }

        return best;
    }

    /// <summary>
    /// Scans the board for tiles matching the session's target color and adds
    /// them to <see cref="ColorBombSession.PendingTargets"/>. Skips positions
    /// already targeted, falling tiles, and tiles locked by other sessions.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="session">The active ColorBomb session to collect targets for.</param>
    internal static void CollectTargets(in GameState state, ColorBombSession session)
    {
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var pos = new Position(x, y);
                if (session.TargetedPositions.Contains(pos)) continue;

                var tile = state.GetTile(x, y);
                if (tile.Type != session.TargetColor) continue;
                // Skip tiles still in motion (only target settled tiles)
                if (tile.IsFalling) continue;
                // Skip tiles already locked by another session
                if (state.IsLocked(x, y, CellLockType.Targeting)) continue;

                session.PendingTargets.Add(new BeamTarget
                {
                    Position = pos,
                    TileId = tile.Id
                });
                session.TargetedPositions.Add(pos);
            }
        }
    }

    /// <summary>
    /// Performs a Fisher-Yates shuffle on a subrange of the target list,
    /// from <paramref name="startIndex"/> to the end.
    /// </summary>
    /// <param name="targets">The list of beam targets to shuffle.</param>
    /// <param name="startIndex">Index to start shuffling from (inclusive).</param>
    /// <param name="random">Random number generator.</param>
    internal static void ShuffleTargets(List<BeamTarget> targets, int startIndex, Match3.Random.IRandom random)
    {
        // Fisher-Yates shuffle on [startIndex, Count)
        for (int i = targets.Count - 1; i > startIndex; i--)
        {
            int j = random.Next(startIndex, i + 1);
            (targets[i], targets[j]) = (targets[j], targets[i]);
        }
    }
}
