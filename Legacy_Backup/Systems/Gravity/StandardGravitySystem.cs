using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Interfaces;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.Gravity;

public class StandardGravitySystem : IGravitySystem
{
    private readonly ITileGenerator _tileGenerator;

    public StandardGravitySystem(ITileGenerator tileGenerator)
    {
        _tileGenerator = tileGenerator;
    }

    public List<TileMove> ApplyGravity(ref GameState state)
    {
        var moves = Pools.ObtainList<TileMove>();
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

    public List<TileMove> Refill(ref GameState state)
    {
        var newTiles = Pools.ObtainList<TileMove>();
        for (int x = 0; x < state.Width; x++)
        {
            int nextSpawnY = -1;
            for (int y = state.Height - 1; y >= 0; y--)
            {
                if (state.GetType(x, y) == TileType.None)
                {
                    var t = _tileGenerator.GenerateNonMatchingTile(ref state, x, y);
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
