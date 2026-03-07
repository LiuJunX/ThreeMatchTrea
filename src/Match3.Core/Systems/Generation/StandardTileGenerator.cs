using System;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Random;

namespace Match3.Core.Systems.Generation;

/// <summary>
/// Basic generator: avoids immediate 3-matches.
/// </summary>
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

    private static readonly ElementType[] _colors = new[]
    {
        ElementType.Item1,
        ElementType.Item2,
        ElementType.Item3,
        ElementType.Item4,
        ElementType.Item5,
        ElementType.Item6
    };

    public ElementType GenerateNonMatchingTile(ref GameState state, int x, int y)
    {
        int count = Math.Min(state.TileTypesCount, _colors.Length);
        if (count <= 0) return ElementType.None;

        for (int i = 0; i < 10; i++)
        {
            // Use 0-based index for array lookup
            int idx = Next(state, 0, count);
            var t = _colors[idx];
            
            if (!CreatesImmediateRun(ref state, x, y, t)) return t;
        }
        
        return _colors[Next(state, 0, count)];
    }

    private bool CreatesImmediateRun(ref GameState state, int x, int y, ElementType t)
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
