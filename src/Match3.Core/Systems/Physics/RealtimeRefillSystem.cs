using System.Numerics;
using Match3.Core.Interfaces;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Physics;

public class RealtimeRefillSystem
{
    private readonly ITileGenerator _tileGenerator;

    public RealtimeRefillSystem(ITileGenerator tileGenerator)
    {
        _tileGenerator = tileGenerator;
    }

    public void Update(ref GameState state)
    {
        for (int x = 0; x < state.Width; x++)
        {
            // Find continuous empty slots starting from top
            int deepestEmptyY = -1;
            for (int y = 0; y < state.Height; y++)
            {
                if (state.GetTile(x, y).Type != TileType.None)
                {
                    break;
                }
                deepestEmptyY = y;
            }

            if (deepestEmptyY >= 0)
            {
                // Fill from bottom up (deepest first) to ensure correct stacking positions
                // deepestEmptyY corresponds to the first tile to enter (lowest position: -1)
                // 0 corresponds to the last tile (highest position: -(deepest + 1))
                
                for (int y = deepestEmptyY; y >= 0; y--)
                {
                    var type = _tileGenerator.GenerateNonMatchingTile(ref state, x, y);
                    var tile = new Tile(state.NextTileId++, type, x, y);
                    
                    // Calculate start position
                    // The tile at 'deepestEmptyY' starts at -1.0f
                    // The tile above it starts at -2.0f, etc.
                    // offset = (deepestEmptyY - y) + 1
                    float startY = -1.0f - (deepestEmptyY - y);
                    
                    tile.Position = new Vector2(x, startY);
                    tile.Velocity = new Vector2(0, 2.0f); 
                    tile.IsFalling = true;

                    state.SetTile(x, y, tile);
                }
            }
        }
    }
}
