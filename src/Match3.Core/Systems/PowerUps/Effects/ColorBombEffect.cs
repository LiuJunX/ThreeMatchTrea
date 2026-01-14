using System.Collections.Generic;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.View;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.PowerUps.Effects;

public class ColorBombEffect : IBombEffect
{
    public BombType Type => BombType.Color;

    public void Apply(in GameState state, Position origin, HashSet<Position> affectedTiles)
    {
        // 彩球爆炸：消除出现最多的一种颜色
        var counts = Pools.Obtain<Dictionary<TileType, int>>();

        try
        {
            // 1. 统计颜色数量（只统计普通颜色，不统计 Rainbow/Bomb/None）
            for (int i = 0; i < state.Grid.Length; i++)
            {
                var t = state.Grid[i];
                if (t.Type != TileType.None && t.Type != TileType.Rainbow && t.Type != TileType.Bomb)
                {
                    if (!counts.ContainsKey(t.Type)) counts[t.Type] = 0;
                    counts[t.Type]++;
                }
            }

            // 2. 找出数量最多的颜色
            TileType maxType = TileType.None;
            int maxCount = -1;
            foreach (var kvp in counts)
            {
                if (kvp.Value > maxCount)
                {
                    maxCount = kvp.Value;
                    maxType = kvp.Key;
                }
            }

            // 3. 消除该颜色的所有方块
            if (maxType != TileType.None)
            {
                for (int y = 0; y < state.Height; y++)
                {
                    for (int x = 0; x < state.Width; x++)
                    {
                        if (state.GetType(x, y) == maxType)
                        {
                            affectedTiles.Add(new Position(x, y));
                        }
                    }
                }
            }
        }
        finally
        {
            counts.Clear();
            Pools.Release(counts);
        }
    }
}
