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

public class UfoEffect : IBombEffect
{
    public BombType Type => BombType.Ufo;

    public void Apply(in GameState state, Position origin, HashSet<Position> affectedTiles)
    {
        // UFO 起飞时：以自身为中心最小十字消除（上下左右各1格）
        affectedTiles.Add(origin); // 中心
        if (origin.X > 0) affectedTiles.Add(new Position(origin.X - 1, origin.Y)); // 左
        if (origin.X < state.Width - 1) affectedTiles.Add(new Position(origin.X + 1, origin.Y)); // 右
        if (origin.Y > 0) affectedTiles.Add(new Position(origin.X, origin.Y - 1)); // 上
        if (origin.Y < state.Height - 1) affectedTiles.Add(new Position(origin.X, origin.Y + 1)); // 下

        // 然后随机击中1个方块
        var candidates = Pools.ObtainList<Position>();
        try
        {
            for (int y = 0; y < state.Height; y++)
            {
                for (int x = 0; x < state.Width; x++)
                {
                    // 跳过小十字范围内的位置
                    if (x == origin.X && y == origin.Y) continue;
                    if (x == origin.X - 1 && y == origin.Y) continue;
                    if (x == origin.X + 1 && y == origin.Y) continue;
                    if (x == origin.X && y == origin.Y - 1) continue;
                    if (x == origin.X && y == origin.Y + 1) continue;

                    var t = state.GetTile(x, y);
                    if (t.Type != TileType.None)
                    {
                        candidates.Add(new Position(x, y));
                    }
                }
            }

            if (candidates.Count > 0)
            {
                int idx = state.Random.Next(0, candidates.Count);
                affectedTiles.Add(candidates[idx]);
            }
        }
        finally
        {
            Pools.Release(candidates);
        }
    }
}
