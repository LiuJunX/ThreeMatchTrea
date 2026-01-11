using System.Collections.Generic;
using Match3.Core.Interfaces;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.PowerUps;

public class PowerUpHandler : IPowerUpHandler
{
    private readonly IScoreSystem _scoreSystem;

    public PowerUpHandler(IScoreSystem scoreSystem)
    {
        _scoreSystem = scoreSystem;
    }

    public void ProcessSpecialMove(ref GameState state, Position p1, Position p2, out int points)
    {
        points = 0;
        var t1 = state.GetTile(p1.X, p1.Y);
        var t2 = state.GetTile(p2.X, p2.Y);

        // Calculate score before modifying state (tiles might be cleared)
        points = _scoreSystem.CalculateSpecialMoveScore(t1.Type, t1.Bomb, t2.Type, t2.Bomb);

        if (TryHandleRainbowCombo(ref state, t1, t2, p1, p2, out int rainbowPoints))
        {
            // Use calculated points, ignore legacy out param if needed, or consistency check
            return;
        }

        if (TryHandleBombCombo(ref state, t1, t2, p1, p2, out int bombPoints))
        {
            return;
        }
        
        // If no special move happened, reset points
        points = 0;
    }

    private bool TryHandleRainbowCombo(ref GameState state, Tile t1, Tile t2, Position p1, Position p2, out int points)
    {
        points = 0; // Legacy param, ignored by caller in favor of IScoreSystem result
        bool isT1Rainbow = t1.Type == TileType.Rainbow;
        bool isT2Rainbow = t2.Type == TileType.Rainbow;

        if (!isT1Rainbow && !isT2Rainbow) return false;

        // 1. Rainbow + Rainbow
        if (isT1Rainbow && isT2Rainbow)
        {
            ClearAll(ref state);
            return true;
        }

        // 2. Rainbow + Any
        var colorTile = isT1Rainbow ? t2 : t1;
        
        // If the other tile is not a valid color target (e.g. None), ignore
        if (colorTile.Type == TileType.None || colorTile.Type == TileType.Rainbow) return false;

        if (colorTile.Bomb != BombType.None)
        {
            // Rainbow + Bomb: Transform all of that color to that BombType
            ReplaceColorWithBomb(ref state, colorTile.Type, colorTile.Bomb);
            
            // Then Explode all of them. Pass the bomb type to ensure they explode even if cleared by others.
            ExplodeAllByType(ref state, colorTile.Type, colorTile.Bomb);
        }
        else
        {
            // Rainbow + Normal: Clear all of that color
            ClearColor(ref state, colorTile.Type);
        }
        
        // Clear the Rainbow and the source tile (ensure they are gone)
        state.SetTile(p1.X, p1.Y, new Tile(0, TileType.None, p1.X, p1.Y));
        state.SetTile(p2.X, p2.Y, new Tile(0, TileType.None, p2.X, p2.Y));
        
        return true;
    }

    private bool TryHandleBombCombo(ref GameState state, Tile t1, Tile t2, Position p1, Position p2, out int points)
    {
        points = 0;
        if (t1.Bomb == BombType.None || t2.Bomb == BombType.None) return false;

        // 3. Bomb + Bomb
        if ((t1.Bomb == BombType.Horizontal || t1.Bomb == BombType.Vertical) &&
            (t2.Bomb == BombType.Horizontal || t2.Bomb == BombType.Vertical))
        {
            ExplodeRow(ref state, p2.Y);
            ExplodeCol(ref state, p2.X);
        }
        else
        {
            ExplodeArea(ref state, p2.X, p2.Y, 2);
        }
        
        state.SetTile(p1.X, p1.Y, new Tile(0, TileType.None, p1.X, p1.Y));
        state.SetTile(p2.X, p2.Y, new Tile(0, TileType.None, p2.X, p2.Y));
        
        return true;
    }

    private void ClearAll(ref GameState state)
    {
        for(int i=0; i<state.Grid.Length; i++)
        {
            ref var t = ref state.Grid[i];
            t = new Tile(0, TileType.None, t.Position);
        }
    }
    
    private void ClearColor(ref GameState state, TileType color)
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
    
    private void ReplaceColorWithBomb(ref GameState state, TileType color, BombType bomb)
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
    
    private void ExplodeAllByType(ref GameState state, TileType type, BombType forcedBombType = BombType.None)
    {
        // Collect positions first
        var positions = new List<Position>();
        for(int i=0; i<state.Grid.Length; i++)
        {
            if (state.Grid[i].Type == type)
            {
                int x = i % state.Width;
                int y = i / state.Width;
                positions.Add(new Position(x, y));
            }
        }

        // Explode each
        foreach(var p in positions)
        {
            if (forcedBombType != BombType.None)
            {
                ExplodeBomb(ref state, p.X, p.Y, forcedBombType);
            }
            else
            {
                var t = state.GetTile(p.X, p.Y);
                if (t.Bomb != BombType.None)
                    ExplodeBomb(ref state, p.X, p.Y, t.Bomb);
                else
                    state.SetTile(p.X, p.Y, new Tile(0, TileType.None, p.X, p.Y));
            }
        }
    }

    private void ExplodeBomb(ref GameState state, int cx, int cy, BombType type)
    {
        switch (type)
        {
            case BombType.Horizontal:
                ExplodeRow(ref state, cy);
                break;
            case BombType.Vertical:
                ExplodeCol(ref state, cx);
                break;
            case BombType.Ufo:
                {
                    var positions = new List<Position>();
                    for (int i = 0; i < state.Grid.Length; i++)
                    {
                        int x = i % state.Width;
                        int y = i / state.Width;
                        if (x == cx && y == cy) continue;
                        if (state.Grid[i].Type != TileType.None)
                        {
                            positions.Add(new Position(x, y));
                        }
                    }
                    if (positions.Count > 0)
                    {
                        int idx = state.Random.Next(0, positions.Count);
                        var p = positions[idx];
                        state.SetTile(p.X, p.Y, new Tile(0, TileType.None, p.X, p.Y));
                    }
                }
                break;
            case BombType.Square3x3:
                ExplodeArea(ref state, cx, cy, 1); // 3x3
                break;
            case BombType.Color:
                ClearAll(ref state);
                break;
            default:
                state.SetTile(cx, cy, new Tile(0, TileType.None, cx, cy));
                break;
        }
    }

    private void ExplodeRow(ref GameState state, int y)
    {
        for(int x=0; x<state.Width; x++) 
            state.SetTile(x, y, new Tile(0, TileType.None, x, y));
    }
    
    private void ExplodeCol(ref GameState state, int x)
    {
        for(int y=0; y<state.Height; y++) 
            state.SetTile(x, y, new Tile(0, TileType.None, x, y));
    }
    
    private void ExplodeArea(ref GameState state, int cx, int cy, int radius)
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
}
