using System;
using System.Collections.Generic;
using Match3.Core.Structs;

namespace Match3.Core.Logic;

/// <summary>
/// Stateless logic functions.
/// This is the "System" in our ECS-like architecture.
/// </summary>
public static class GameRules
{
    public static void Initialize(ref GameState state)
    {
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                state.Set(x, y, GenerateNonMatchingTile(ref state, x, y));
            }
        }
    }

    public static TileType GenerateNonMatchingTile(ref GameState state, int x, int y)
    {
        // Try up to 10 times
        for (int i = 0; i < 10; i++)
        {
            var t = (TileType)state.Random.Next(1, state.TileTypesCount + 1);
            if (!CreatesImmediateRun(ref state, x, y, t)) return t;
        }
        return (TileType)state.Random.Next(1, state.TileTypesCount + 1);
    }

    private static bool CreatesImmediateRun(ref GameState state, int x, int y, TileType t)
    {
        if (x >= 2)
        {
            if (state.Get(x - 1, y) == t && state.Get(x - 2, y) == t) return true;
        }
        if (y >= 2)
        {
            if (state.Get(x, y - 1) == t && state.Get(x, y - 2) == t) return true;
        }
        return false;
    }

    public static bool IsValidMove(in GameState state, Position from, Position to)
    {
        // 1. Check bounds
        if (from.X < 0 || from.X >= state.Width || from.Y < 0 || from.Y >= state.Height) return false;
        if (to.X < 0 || to.X >= state.Width || to.Y < 0 || to.Y >= state.Height) return false;

        // 2. Check adjacency
        if (Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y) != 1) return false;

        return true;
    }

    public static bool ApplyMove(ref GameState state, Position from, Position to, out int cascades, out int totalPoints)
    {
        cascades = 0;
        totalPoints = 0;

        // 1. Swap
        Swap(ref state, from, to);

        // 2. Check match
        if (!HasMatches(in state))
        {
            // Revert
            Swap(ref state, from, to);
            return false;
        }

        // 3. Process Cascades
        state.MoveCount++;
        
        while (true)
        {
            var matches = FindMatches(in state);
            if (matches.Count == 0) break;

            cascades++;
            
            // Score
            int points = matches.Count * 10;
            if (matches.Count > 3) points += (matches.Count - 3) * 20;
            if (cascades > 1) points *= cascades;
            totalPoints += points;

            // Clear
            foreach (var p in matches)
            {
                state.Set(p.X, p.Y, TileType.None);
            }

            // Gravity & Refill
            ApplyGravity(ref state);
            Refill(ref state);
        }

        state.Score += totalPoints;
        return true;
    }

    public static void Swap(ref GameState state, Position a, Position b)
    {
        var idxA = state.Index(a.X, a.Y);
        var idxB = state.Index(b.X, b.Y);
        var temp = state.Grid[idxA];
        state.Grid[idxA] = state.Grid[idxB];
        state.Grid[idxB] = temp;
    }

    public static HashSet<Position> FindMatches(in GameState state)
    {
        var result = new HashSet<Position>();
        int w = state.Width;
        int h = state.Height;

        // Horizontal
        for (int y = 0; y < h; y++)
        {
            int run = 1;
            for (int x = 1; x < w; x++)
            {
                var curr = state.Get(x, y);
                var prev = state.Get(x - 1, y);
                if (curr != TileType.None && curr == prev)
                {
                    run++;
                }
                else
                {
                    if (run >= 3)
                    {
                        for (int k = x - run; k < x; k++) result.Add(new Position(k, y));
                    }
                    run = 1;
                }
            }
            if (run >= 3)
            {
                for (int k = w - run; k < w; k++) result.Add(new Position(k, y));
            }
        }

        // Vertical
        for (int x = 0; x < w; x++)
        {
            int run = 1;
            for (int y = 1; y < h; y++)
            {
                var curr = state.Get(x, y);
                var prev = state.Get(x, y - 1);
                if (curr != TileType.None && curr == prev)
                {
                    run++;
                }
                else
                {
                    if (run >= 3)
                    {
                        for (int k = y - run; k < y; k++) result.Add(new Position(x, k));
                    }
                    run = 1;
                }
            }
            if (run >= 3)
            {
                for (int k = h - run; k < h; k++) result.Add(new Position(x, k));
            }
        }
        return result;
    }

    public static bool HasMatches(in GameState state)
    {
        // Optimized check: return true as soon as one match is found
        // For now, we reuse FindMatches for correctness, but this can be optimized later.
        return FindMatches(in state).Count > 0;
    }

    public static List<TileMove> ApplyGravity(ref GameState state)
    {
        var moves = new List<TileMove>();
        for (int x = 0; x < state.Width; x++)
        {
            int writeY = state.Height - 1;
            for (int y = state.Height - 1; y >= 0; y--)
            {
                var t = state.Get(x, y);
                if (t != TileType.None)
                {
                    if (writeY != y)
                    {
                        state.Set(x, writeY, t);
                        state.Set(x, y, TileType.None);
                        moves.Add(new TileMove(new Position(x, y), new Position(x, writeY)));
                    }
                    writeY--;
                }
            }
        }
        return moves;
    }

    public static List<TileMove> Refill(ref GameState state)
    {
        var newTiles = new List<TileMove>();
        for (int x = 0; x < state.Width; x++)
        {
            int nextSpawnY = -1;
            // Iterate from bottom up to assign "closest" spawn tile to deepest empty slot
            for (int y = state.Height - 1; y >= 0; y--)
            {
                if (state.Get(x, y) == TileType.None)
                {
                    var t = GenerateNonMatchingTile(ref state, x, y);
                    state.Set(x, y, t);
                    newTiles.Add(new TileMove(new Position(x, nextSpawnY), new Position(x, y)));
                    nextSpawnY--;
                }
            }
        }
        return newTiles;
    }
}
