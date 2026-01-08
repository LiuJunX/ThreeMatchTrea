using Match3.Core.Interfaces;
using Match3.Core.Structs;
using Match3.Random;

namespace Match3.Core.Logic;

public class StandardTileGenerator : ITileGenerator
{
    private readonly IRandom? _rng;
    

    public StandardTileGenerator()
    {
    }

    public StandardTileGenerator(IRandom rng)
    {
        _rng = rng;
    }

    public TileType GenerateNonMatchingTile(ref GameState state, int x, int y)
    {
        for (int i = 0; i < 10; i++)
        {
            var t = (TileType)Next(state, 1, state.TileTypesCount + 1);
            if (!CreatesImmediateRun(ref state, x, y, t)) return t;
        }
        return (TileType)Next(state, 1, state.TileTypesCount + 1);
    }

    private bool CreatesImmediateRun(ref GameState state, int x, int y, TileType t)
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

    private int Next(GameState state, int minIncl, int maxExcl)
    {
        var rng = _rng ?? state.Random;
        return rng.Next(minIncl, maxExcl);
    }
}
