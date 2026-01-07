using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Match3.Core.Structs;

namespace Match3.Core.Logic;

public static class GameRules
{
    public static void Initialize(ref GameState state)
    {
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                state.SetTile(x, y, new Tile(state.NextTileId++, GenerateNonMatchingTile(ref state, x, y), x, y));
            }
        }
    }

    public static TileType GenerateNonMatchingTile(ref GameState state, int x, int y)
    {
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
            if (state.GetType(x - 1, y) == t && state.GetType(x - 2, y) == t) return true;
        }
        if (y >= 2)
        {
            if (state.GetType(x, y - 1) == t && state.GetType(x, y - 2) == t) return true;
        }
        return false;
    }

    public static bool IsValidMove(in GameState state, Position from, Position to)
    {
        if (from.X < 0 || from.X >= state.Width || from.Y < 0 || from.Y >= state.Height) return false;
        if (to.X < 0 || to.X >= state.Width || to.Y < 0 || to.Y >= state.Height) return false;
        if (Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y) != 1) return false;
        return true;
    }

    public static bool ApplyMove(ref GameState state, Position from, Position to, out int cascades, out int totalPoints)
    {
        cascades = 0;
        totalPoints = 0;

        // 1. Swap
        Swap(ref state, from, to);

        // 2. Check for Special Bomb Combos (Bomb + Bomb)
        var tileFrom = state.GetTile(from.X, from.Y);
        var tileTo = state.GetTile(to.X, to.Y);
        
        bool isBombCombo = (tileFrom.Bomb != BombType.None || tileFrom.Type == TileType.Rainbow) && 
                           (tileTo.Bomb != BombType.None || tileTo.Type == TileType.Rainbow);
        
        // Check for Color Bomb + Normal (Special Activation)
        bool isColorMix = !isBombCombo && (tileFrom.Type == TileType.Rainbow || tileTo.Type == TileType.Rainbow);

        if (isBombCombo || isColorMix)
        {
             // Process Special Combo
             ProcessSpecialMove(ref state, from, to, out int comboPoints);
             totalPoints += comboPoints;
             
             // After special move, we always cascade
             // Re-check matches after the explosion to continue the chain
        }
        else
        {
            // Normal Move
            if (!HasMatches(in state))
            {
                Swap(ref state, from, to); // Revert
                return false;
            }
        }

        // 3. Process Cascades
        state.MoveCount++;
        
        bool firstIteration = true;
        while (true)
        {
            // Find matches and generate new bombs if applicable
            // Use 'to' as focus only for the first iteration (user move)
            var groups = FindMatchGroups(in state, firstIteration ? to : null);
            
            if (groups.Count == 0 && !HasPendingExplosions(in state)) break;

            cascades++;
            firstIteration = false;
            
            int points = ProcessMatches(ref state, groups);
            
            if (cascades > 1) points *= cascades;
            totalPoints += points;

            ApplyGravity(ref state);
            Refill(ref state);
        }

        state.Score += totalPoints;
        return true;
    }

    private static bool HasPendingExplosions(in GameState state)
    {
        // For now, we handle explosions immediately in ProcessMatches, so this is just for future use or checks
        // Ideally we might want a queue of explosions, but let's keep it synchronous for now.
        return false;
    }

    private static void ProcessSpecialMove(ref GameState state, Position p1, Position p2, out int points)
    {
        points = 0;
        var t1 = state.GetTile(p1.X, p1.Y);
        var t2 = state.GetTile(p2.X, p2.Y);

        // Logic for Combos
        // 1. Color + Color -> Clear Board
        if (t1.Type == TileType.Rainbow && t2.Type == TileType.Rainbow)
        {
            ClearAll(ref state);
            points += 5000;
            return;
        }

        // 2. Color + Bomb -> Turn all of that color into that bomb type
        if (t1.Type == TileType.Rainbow || t2.Type == TileType.Rainbow)
        {
            var colorTile = t1.Type == TileType.Rainbow ? t2 : t1;
            var rainbowTile = t1.Type == TileType.Rainbow ? t1 : t2;
            
            if (colorTile.Bomb != BombType.None)
            {
                // Turn all 'colorTile.Type' into 'colorTile.Bomb'
                ReplaceColorWithBomb(ref state, colorTile.Type, colorTile.Bomb);
                // Then explode all of them? Or let next loop handle it?
                // Usually we trigger them. Let's trigger them.
                ExplodeAllByType(ref state, colorTile.Type);
                
                // Clear the rainbow and the original bomb
                state.SetTile(p1.X, p1.Y, new Tile(0, TileType.None, p1.X, p1.Y));
                state.SetTile(p2.X, p2.Y, new Tile(0, TileType.None, p2.X, p2.Y));
            }
            else
            {
                // Color + Normal -> Clear all of that color
                ClearColor(ref state, colorTile.Type);
                state.SetTile(p1.X, p1.Y, new Tile(0, TileType.None, p1.X, p1.Y));
                state.SetTile(p2.X, p2.Y, new Tile(0, TileType.None, p2.X, p2.Y));
            }
            points += 2000;
            return;
        }

        // 3. Bomb + Bomb
        if (t1.Bomb != BombType.None && t2.Bomb != BombType.None)
        {
             // Combine effects
             // e.g. Line + Line -> Cross
             // Square + Square -> Big Boom
             // Line + Square -> Big Cross (3 rows x 3 cols)
             
             // For simplicity, trigger a "Mega Explosion" based on types
             // Or just trigger both?
             // Requirement: "Arbitrary adjacent bomb swap elimination"
             
             ExplodeBomb(ref state, p1.X, p1.Y, t1.Bomb, 2); // 2x Radius or special?
             ExplodeBomb(ref state, p2.X, p2.Y, t2.Bomb, 2);
             
             // Actually, usually they merge into a specific effect centered at p2 (target).
             // Let's implement specific combos:
             
             if ((t1.Bomb == BombType.Horizontal || t1.Bomb == BombType.Vertical) &&
                 (t2.Bomb == BombType.Horizontal || t2.Bomb == BombType.Vertical))
             {
                 // Cross Blast
                 ExplodeRow(ref state, p2.Y);
                 ExplodeCol(ref state, p2.X);
             }
             else
             {
                 // Generic big boom for now
                 ExplodeArea(ref state, p2.X, p2.Y, 2); // 5x5
             }
             
             state.SetTile(p1.X, p1.Y, new Tile(0, TileType.None, p1.X, p1.Y));
             state.SetTile(p2.X, p2.Y, new Tile(0, TileType.None, p2.X, p2.Y));
             
             points += 1000;
             return;
        }
    }

    public static int ProcessMatches(ref GameState state, List<MatchGroup> groups)
    {
        int points = 0;
        var tilesToClear = new HashSet<Position>();
        var protectedTiles = new HashSet<Position>();

        foreach (var g in groups)
        {
            points += g.Positions.Count * 10;
            
            // Add matched tiles to clear list
            foreach (var p in g.Positions)
            {
                tilesToClear.Add(p);
            }

            // Create Bomb if needed
            if (g.SpawnBombType != BombType.None && g.BombOrigin.HasValue)
            {
                var p = g.BombOrigin.Value;
                // We will set this tile to the bomb type AFTER clearing
                // To avoid clearing the newly created bomb, we remove it from tilesToClear
                // BUT we need to ensure the tile at 'p' is actually part of the match (it is).
                tilesToClear.Remove(p);
                protectedTiles.Add(p);
                
                // Set the bomb
                // If it's a Color Bomb, the tile type must be Rainbow
                var newType = g.SpawnBombType == BombType.Color ? TileType.Rainbow : g.Type;
                state.SetTile(p.X, p.Y, new Tile(state.NextTileId++, newType, p.X, p.Y, g.SpawnBombType));
            }
        }

        // Process Clearing (Explosions)
        // We use a queue to handle cascading explosions
        var queue = new Queue<Position>(tilesToClear);
        var cleared = new HashSet<Position>(); // Track what's already cleared to avoid cycles

        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            if (protectedTiles.Contains(p)) continue; // Do not clear newly created bombs in this step
            if (cleared.Contains(p)) continue;

            var t = state.GetTile(p.X, p.Y);
            if (t.Type == TileType.None) continue; // Already cleared

            cleared.Add(p);

            // If it's a bomb, add its range to queue
            if (t.Bomb != BombType.None)
            {
                var explosionRange = GetExplosionRange(in state, p.X, p.Y, t.Bomb);
                foreach (var exP in explosionRange)
                {
                    if (!cleared.Contains(exP))
                        queue.Enqueue(exP);
                }
            }
            
            // Actually clear the tile (unless it was the one we just turned into a bomb - handled above)
            // Wait, we removed the new bomb from 'tilesToClear' so it's not in the initial queue.
            // But if an explosion hits it, it might be added to queue.
            // We should protect the JUST CREATED bomb? 
            // In standard Match-3, the new bomb is safe from the match that created it.
            // But if another match explodes it in the same frame?
            // For now, assume it's safe because we only set it after this loop?
            // No, we set it inside the loop above.
            // So we need to check if 'p' is a newly created bomb.
            // Simpler: Just clear it.
            
            state.SetTile(p.X, p.Y, new Tile(0, TileType.None, p.X, p.Y));
        }
        
        return points;
    }
    
    private static List<Position> GetExplosionRange(in GameState state, int cx, int cy, BombType type)
    {
        var list = new List<Position>();
        int w = state.Width;
        int h = state.Height;

        switch (type)
        {
            case BombType.Horizontal:
                for (int x = 0; x < w; x++) list.Add(new Position(x, cy));
                break;
            case BombType.Vertical:
                for (int y = 0; y < h; y++) list.Add(new Position(cx, y));
                break;
            case BombType.SmallCross:
                list.Add(new Position(cx, cy));
                if (cx > 0) list.Add(new Position(cx - 1, cy));
                if (cx < w - 1) list.Add(new Position(cx + 1, cy));
                if (cy > 0) list.Add(new Position(cx, cy - 1));
                if (cy < h - 1) list.Add(new Position(cx, cy + 1));
                break;
            case BombType.Square9x9:
                // 3x3 Area
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = cx + dx;
                        int ny = cy + dy;
                        if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                            list.Add(new Position(nx, ny));
                    }
                }
                break;
            case BombType.Color:
                // Usually handled separately, but if it explodes, maybe random or all?
                // Let's say if a color bomb is destroyed by an explosion, it triggers random lightning?
                // For now, just itself.
                list.Add(new Position(cx, cy));
                break;
        }
        return list;
    }
    
    // Helpers for Bomb Effects
    private static void ClearAll(ref GameState state)
    {
        for(int i=0; i<state.Grid.Length; i++)
        {
            ref var t = ref state.Grid[i];
            t = new Tile(0, TileType.None, t.Position);
        }
    }
    
    private static void ClearColor(ref GameState state, TileType color)
    {
        for(int i=0; i<state.Grid.Length; i++)
        {
            if (state.Grid[i].Type == color)
            {
                ref var t = ref state.Grid[i];
                t = new Tile(0, TileType.None, t.Position);
            }
        }
    }
    
    private static void ReplaceColorWithBomb(ref GameState state, TileType color, BombType bomb)
    {
        for(int i=0; i<state.Grid.Length; i++)
        {
            if (state.Grid[i].Type == color)
            {
                ref var t = ref state.Grid[i];
                t.Bomb = bomb;
            }
        }
    }
    
    private static void ExplodeAllByType(ref GameState state, TileType type)
    {
        // This is complex because of recursion.
        // For simplicity, just clear them all for now.
        ClearColor(ref state, type);
    }

    private static void ExplodeBomb(ref GameState state, int x, int y, BombType type, int radiusMultiplier = 1)
    {
        // Simple instant clear for combos
        var range = GetExplosionRange(in state, x, y, type); // Need to adjust for radiusMultiplier if needed
        foreach(var p in range)
        {
             state.SetTile(p.X, p.Y, new Tile(0, TileType.None, p.X, p.Y));
        }
    }

    private static void ExplodeRow(ref GameState state, int y)
    {
        for(int x=0; x<state.Width; x++) 
            state.SetTile(x, y, new Tile(0, TileType.None, x, y));
    }
    
    private static void ExplodeCol(ref GameState state, int x)
    {
        for(int y=0; y<state.Height; y++) 
            state.SetTile(x, y, new Tile(0, TileType.None, x, y));
    }
    
    private static void ExplodeArea(ref GameState state, int cx, int cy, int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int nx = cx + dx;
                int ny = cy + dy;
                if (nx >= 0 && nx < state.Width && ny >= 0 && ny < state.Height)
                     state.SetTile(nx, ny, new Tile(0, TileType.None, nx, ny));
            }
        }
    }

    public static void Swap(ref GameState state, Position a, Position b)
    {
        var idxA = state.Index(a.X, a.Y);
        var idxB = state.Index(b.X, b.Y);
        var temp = state.Grid[idxA];
        state.Grid[idxA] = state.Grid[idxB];
        state.Grid[idxB] = temp;
        
        // Update positions in struct? 
        // No, the Tile struct has a Position field which is for VISUALS.
        // The Grid index determines logic position.
        // However, if we want animations to work, we might need to swap the visual positions too?
        // Match3Controller handles visual position interpolation. 
        // Here we just swap the data.
    }

    public static HashSet<Position> FindMatches(in GameState state)
    {
        // Compatibility wrapper
        var groups = FindMatchGroups(in state);
        var result = new HashSet<Position>();
        foreach(var g in groups)
        {
            foreach(var p in g.Positions) result.Add(p);
        }
        return result;
    }

    public static bool HasMatches(in GameState state)
    {
        return FindMatches(in state).Count > 0;
    }

    public static List<MatchGroup> FindMatchGroups(in GameState state, Position? focus = null)
    {
        var groups = new List<MatchGroup>();
        var visited = new HashSet<Position>();
        int w = state.Width;
        int h = state.Height;

        // 1. Find all horizontal and vertical runs
        // We use a temporary structure to map each tile to its runs
        // Or simpler: Iterate board, if tile not visited, Flood Fill matching colors.
        // Then check if the component forms a valid match (>=3 in row or col).
        
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var p = new Position(x, y);
                if (visited.Contains(p)) continue;
                
                var type = state.GetType(x, y);
                if (type == TileType.None || type == TileType.Rainbow) continue; // Rainbow doesn't match itself normally

                // Flood fill to find connected components of same color
                var component = GetConnectedComponent(in state, p, type);
                
                // Analyze component to see if it contains any valid match (Run >= 3)
                var validMatch = AnalyzeMatch(component, focus);
                
                if (validMatch != null)
                {
                    validMatch.Type = type; // Set type explicitly
                    groups.Add(validMatch);
                    foreach(var mp in validMatch.Positions) visited.Add(mp);
                }
            }
        }
        return groups;
    }

    private static HashSet<Position> GetConnectedComponent(in GameState state, Position start, TileType type)
    {
        var component = new HashSet<Position>();
        var queue = new Queue<Position>();
        queue.Enqueue(start);
        component.Add(start);
        
        while(queue.Count > 0)
        {
            var curr = queue.Dequeue();
            // Check 4 neighbors
            CheckNeighbor(state, curr.X + 1, curr.Y, type, component, queue);
            CheckNeighbor(state, curr.X - 1, curr.Y, type, component, queue);
            CheckNeighbor(state, curr.X, curr.Y + 1, type, component, queue);
            CheckNeighbor(state, curr.X, curr.Y - 1, type, component, queue);
        }
        return component;
    }
    
    private static void CheckNeighbor(in GameState state, int x, int y, TileType type, HashSet<Position> component, Queue<Position> queue)
    {
        if (x < 0 || x >= state.Width || y < 0 || y >= state.Height) return;
        if (state.GetType(x, y) == type)
        {
            var p = new Position(x, y);
            if (!component.Contains(p))
            {
                component.Add(p);
                queue.Enqueue(p);
            }
        }
    }

    private static MatchGroup AnalyzeMatch(HashSet<Position> component, Position? focus)
    {
        // Check for at least one run of 3
        bool hasRun = false;
        var positions = new List<Position>(component);
        positions.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));

        // Check Horizontal Runs
        // Group by Y
        var byY = positions.GroupBy(p => p.Y);
        foreach(var grp in byY)
        {
            var row = grp.OrderBy(p => p.X).ToList();
            int run = 1;
            for(int i=1; i<row.Count; i++)
            {
                if (row[i].X == row[i-1].X + 1) run++;
                else run = 1;
                if (run >= 3) hasRun = true;
            }
        }
        
        // Check Vertical Runs
        var byX = positions.GroupBy(p => p.X);
        foreach(var grp in byX)
        {
            var col = grp.OrderBy(p => p.Y).ToList();
            int run = 1;
            for(int i=1; i<col.Count; i++)
            {
                if (col[i].Y == col[i-1].Y + 1) run++;
                else run = 1;
                if (run >= 3) hasRun = true;
            }
        }

        if (!hasRun) return null;

        // Valid Match. Determine Shape & Bomb.
        var group = new MatchGroup();
        group.Positions = component;
        // Assume type is same for all
        // group.Type = ...; // Caller knows type
        
        int count = component.Count;
        int minX = positions.Min(p => p.X);
        int maxX = positions.Max(p => p.X);
        int minY = positions.Min(p => p.Y);
        int maxY = positions.Max(p => p.Y);
        int width = maxX - minX + 1;
        int height = maxY - minY + 1;

        // Determine Type from first element
        // Not passed in, but needed. We can get it from state if we had it, but component doesn't have state.
        // We'll set it in caller or leave it. The logic below doesn't strictly need it for BombType.
        
        // Bomb Logic
        if (count >= 5)
        {
            if (width == count || height == count)
            {
                group.SpawnBombType = BombType.Color; // 5 in a line
            }
            else
            {
                // T or L shape
                // Use SmallCross for count=5, Square9x9 for count > 5 (or user preference)
                if (count == 5)
                    group.SpawnBombType = BombType.SmallCross;
                else
                    group.SpawnBombType = BombType.Square9x9; 
            }
        }
        else if (count == 4)
        {
            // 4 in a line (or 2x2?)
            // If 2x2 (Width=2, Height=2), it's a square match.
            if (width == 2 && height == 2)
            {
                // 2x2 Square (Bird?)
                // group.SpawnBombType = BombType.Bird; 
            }
            else
            {
                // Line 4
                // Determine direction: if Width > Height -> Horizontal Match -> Vertical Bomb (usually)
                // Or Horizontal Match -> Horizontal Bomb? 
                // Candy Crush: 4 Horiz -> Striped (Vertical Stripe? No, Striped that clears Horizontal?)
                // Actually: 4 Horiz -> Striped with Horizontal or Vertical?
                // Usually it depends on the swipe direction. Since we don't have swipe, random.
                group.SpawnBombType = (width > height) ? BombType.Vertical : BombType.Horizontal;
            }
        }
        
        // Set Origin
        if (focus.HasValue && component.Contains(focus.Value))
        {
            group.BombOrigin = focus.Value;
        }
        else
        {
            group.BombOrigin = positions[count / 2];
        }

        return group;
    }

    public static List<TileMove> ApplyGravity(ref GameState state)
    {
        var moves = new List<TileMove>();
        for (int x = 0; x < state.Width; x++)
        {
            int writeY = state.Height - 1;
            for (int y = state.Height - 1; y >= 0; y--)
            {
                var t = state.GetTile(x, y);
                if (t.Type != TileType.None)
                {
                    if (writeY != y)
                    {
                        state.SetTile(x, writeY, t);
                        state.SetTile(x, y, new Tile(0, TileType.None, x, y));
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
            for (int y = state.Height - 1; y >= 0; y--)
            {
                if (state.GetType(x, y) == TileType.None)
                {
                    var t = GenerateNonMatchingTile(ref state, x, y);
                    var tile = new Tile(state.NextTileId++, t, new Vector2(x, nextSpawnY));
                    state.SetTile(x, y, tile);
                    newTiles.Add(new TileMove(new Position(x, nextSpawnY), new Position(x, y)));
                    nextSpawnY--;
                }
            }
        }
        return newTiles;
    }
}
