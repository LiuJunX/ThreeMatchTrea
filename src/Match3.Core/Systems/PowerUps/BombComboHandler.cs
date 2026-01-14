using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.PowerUps;

/// <summary>
/// 组合炸弹处理器
///
/// 组合规则：
/// - 火箭 + 火箭 = 十字（1行+1列）
/// - 火箭 + 方块炸弹 = 3行+3列
/// - 火箭 + UFO = 起飞前小十字，落地后消除一行或一列
/// - 火箭 + 彩球 = 最多颜色全变火箭并爆炸
/// - 方块炸弹 + 方块炸弹 = 9x9
/// - 方块炸弹 + UFO = 起飞前小十字，落地后消除5x5
/// - 方块炸弹 + 彩球 = 最多颜色全变3x3炸弹并爆炸
/// - UFO + UFO = 两个原地小十字 + 飞出3个UFO
/// - UFO + 彩球 = 最多颜色全变UFO并起飞
/// - 彩球 + 彩球 = 全屏消除
/// </summary>
public class BombComboHandler
{
    /// <summary>
    /// 尝试应用组合效果
    /// </summary>
    /// <returns>是否触发了组合</returns>
    public bool TryApplyCombo(ref GameState state, Position p1, Position p2, HashSet<Position> affected)
    {
        var t1 = state.GetTile(p1.X, p1.Y);
        var t2 = state.GetTile(p2.X, p2.Y);

        // 检查是否是彩球+普通方块的组合（特殊情况：消除指定颜色）
        if (IsColorBombWithNormalTile(t1, t2))
        {
            ApplyColorBombWithNormalTile(ref state, t1, t2, p1, p2, affected);
            return true;
        }

        // 检查是否两个都是炸弹（或彩球）
        bool isT1Bomb = t1.Bomb != BombType.None || t1.Type == TileType.Rainbow;
        bool isT2Bomb = t2.Bomb != BombType.None || t2.Type == TileType.Rainbow;

        if (!isT1Bomb || !isT2Bomb)
            return false;

        ApplyCombo(ref state, p1, p2, affected);
        return true;
    }

    /// <summary>
    /// 应用组合效果（假设已确认是有效组合）
    /// </summary>
    public void ApplyCombo(ref GameState state, Position p1, Position p2, HashSet<Position> affected)
    {
        var t1 = state.GetTile(p1.X, p1.Y);
        var t2 = state.GetTile(p2.X, p2.Y);

        var b1 = GetEffectiveBombType(t1);
        var b2 = GetEffectiveBombType(t2);

        // 彩球 + 彩球
        if (b1 == BombType.Color && b2 == BombType.Color)
        {
            ApplyColorPlusColor(ref state, affected);
            return;
        }

        // 彩球 + 其他炸弹
        if (b1 == BombType.Color || b2 == BombType.Color)
        {
            var colorBombPos = b1 == BombType.Color ? p1 : p2;
            var otherBombType = b1 == BombType.Color ? b2 : b1;
            var otherTile = b1 == BombType.Color ? t2 : t1;
            ApplyColorBombCombo(ref state, colorBombPos, otherBombType, otherTile, affected);
            return;
        }

        // 火箭 + 火箭
        if (IsRocket(b1) && IsRocket(b2))
        {
            ApplyRocketPlusRocket(ref state, p2, affected);
            return;
        }

        // 火箭 + 方块炸弹
        if ((IsRocket(b1) && b2 == BombType.Square5x5) || (IsRocket(b2) && b1 == BombType.Square5x5))
        {
            ApplyRocketPlusSquare(ref state, p2, affected);
            return;
        }

        // 火箭 + UFO
        if ((IsRocket(b1) && b2 == BombType.Ufo) || (IsRocket(b2) && b1 == BombType.Ufo))
        {
            var rocketType = IsRocket(b1) ? b1 : b2;
            var ufoPos = b1 == BombType.Ufo ? p1 : p2;
            ApplyRocketPlusUfo(ref state, ufoPos, rocketType, affected);
            return;
        }

        // 方块炸弹 + 方块炸弹
        if (b1 == BombType.Square5x5 && b2 == BombType.Square5x5)
        {
            ApplySquarePlusSquare(ref state, p2, affected);
            return;
        }

        // 方块炸弹 + UFO
        if ((b1 == BombType.Square5x5 && b2 == BombType.Ufo) || (b2 == BombType.Square5x5 && b1 == BombType.Ufo))
        {
            var ufoPos = b1 == BombType.Ufo ? p1 : p2;
            ApplySquarePlusUfo(ref state, ufoPos, affected);
            return;
        }

        // UFO + UFO
        if (b1 == BombType.Ufo && b2 == BombType.Ufo)
        {
            ApplyUfoPlusUfo(ref state, p1, p2, affected);
            return;
        }
    }

    #region 组合效果实现

    /// <summary>
    /// 火箭 + 火箭 = 十字（1行+1列）
    /// </summary>
    private void ApplyRocketPlusRocket(ref GameState state, Position center, HashSet<Position> affected)
    {
        // 消除整行
        for (int x = 0; x < state.Width; x++)
            affected.Add(new Position(x, center.Y));

        // 消除整列
        for (int y = 0; y < state.Height; y++)
            affected.Add(new Position(center.X, y));
    }

    /// <summary>
    /// 火箭 + 方块炸弹 = 3行+3列
    /// </summary>
    private void ApplyRocketPlusSquare(ref GameState state, Position center, HashSet<Position> affected)
    {
        // 消除3行
        for (int dy = -1; dy <= 1; dy++)
        {
            int y = center.Y + dy;
            if (y >= 0 && y < state.Height)
            {
                for (int x = 0; x < state.Width; x++)
                    affected.Add(new Position(x, y));
            }
        }

        // 消除3列
        for (int dx = -1; dx <= 1; dx++)
        {
            int x = center.X + dx;
            if (x >= 0 && x < state.Width)
            {
                for (int y = 0; y < state.Height; y++)
                    affected.Add(new Position(x, y));
            }
        }
    }

    /// <summary>
    /// 火箭 + UFO = 小十字 + 一行或一列
    /// </summary>
    private void ApplyRocketPlusUfo(ref GameState state, Position ufoPos, BombType rocketType, HashSet<Position> affected)
    {
        // UFO起飞前小十字
        ApplySmallCross(state, ufoPos, affected);

        // UFO落地后：随机位置消除一行或一列
        var target = GetRandomTarget(ref state, ufoPos, affected);
        if (target.HasValue)
        {
            // 根据火箭类型决定消除行还是列
            bool clearRow = rocketType == BombType.Horizontal ||
                           (rocketType == BombType.Vertical ? false : state.Random.Next(0, 2) == 0);

            if (clearRow)
            {
                for (int x = 0; x < state.Width; x++)
                    affected.Add(new Position(x, target.Value.Y));
            }
            else
            {
                for (int y = 0; y < state.Height; y++)
                    affected.Add(new Position(target.Value.X, y));
            }
        }
    }

    /// <summary>
    /// 方块炸弹 + 方块炸弹 = 9x9
    /// </summary>
    private void ApplySquarePlusSquare(ref GameState state, Position center, HashSet<Position> affected)
    {
        ApplyArea(state, center, 4, affected); // 半径4 = 9x9
    }

    /// <summary>
    /// 方块炸弹 + UFO = 小十字 + 5x5
    /// </summary>
    private void ApplySquarePlusUfo(ref GameState state, Position ufoPos, HashSet<Position> affected)
    {
        // UFO起飞前小十字
        ApplySmallCross(state, ufoPos, affected);

        // UFO落地后：随机位置5x5
        var target = GetRandomTarget(ref state, ufoPos, affected);
        if (target.HasValue)
        {
            ApplyArea(state, target.Value, 2, affected); // 半径2 = 5x5
        }
    }

    /// <summary>
    /// UFO + UFO = 两个小十字 + 3个UFO
    /// </summary>
    private void ApplyUfoPlusUfo(ref GameState state, Position p1, Position p2, HashSet<Position> affected)
    {
        // 两个原地小十字
        ApplySmallCross(state, p1, affected);
        ApplySmallCross(state, p2, affected);

        // 飞出3个UFO，各击中1个随机目标
        for (int i = 0; i < 3; i++)
        {
            var target = GetRandomTarget(ref state, p1, affected);
            if (target.HasValue)
            {
                affected.Add(target.Value);
            }
        }
    }

    /// <summary>
    /// 彩球 + 彩球 = 全屏消除
    /// </summary>
    private void ApplyColorPlusColor(ref GameState state, HashSet<Position> affected)
    {
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                affected.Add(new Position(x, y));
            }
        }
    }

    /// <summary>
    /// 彩球 + 其他炸弹
    /// </summary>
    private void ApplyColorBombCombo(ref GameState state, Position colorBombPos, BombType otherBombType, Tile otherTile, HashSet<Position> affected)
    {
        // 找出数量最多的颜色
        var targetColor = FindMostFrequentColor(ref state);
        if (targetColor == TileType.None)
            return;

        // 收集该颜色的所有位置
        var positions = Pools.ObtainList<Position>();
        try
        {
            for (int y = 0; y < state.Height; y++)
            {
                for (int x = 0; x < state.Width; x++)
                {
                    if (state.GetType(x, y) == targetColor)
                    {
                        positions.Add(new Position(x, y));
                    }
                }
            }

            // 根据组合类型应用效果
            switch (otherBombType)
            {
                case BombType.Horizontal:
                case BombType.Vertical:
                    // 彩球 + 火箭：每个位置变成火箭并爆炸
                    foreach (var pos in positions)
                    {
                        affected.Add(pos);
                        if (otherBombType == BombType.Horizontal)
                        {
                            for (int x = 0; x < state.Width; x++)
                                affected.Add(new Position(x, pos.Y));
                        }
                        else
                        {
                            for (int y = 0; y < state.Height; y++)
                                affected.Add(new Position(pos.X, y));
                        }
                    }
                    break;

                case BombType.Square5x5:
                    // 彩球 + 方块炸弹：每个位置变成3x3炸弹并爆炸
                    foreach (var pos in positions)
                    {
                        ApplyArea(state, pos, 1, affected); // 半径1 = 3x3
                    }
                    break;

                case BombType.Ufo:
                    // 彩球 + UFO：每个位置变成UFO并起飞
                    foreach (var pos in positions)
                    {
                        ApplySmallCross(state, pos, affected);
                        var target = GetRandomTarget(ref state, pos, affected);
                        if (target.HasValue)
                            affected.Add(target.Value);
                    }
                    break;
            }
        }
        finally
        {
            Pools.Release(positions);
        }
    }

    /// <summary>
    /// 彩球 + 普通方块（手动交换）：消除指定颜色
    /// </summary>
    private void ApplyColorBombWithNormalTile(ref GameState state, Tile t1, Tile t2, Position p1, Position p2, HashSet<Position> affected)
    {
        // 确定哪个是彩球，哪个是普通方块
        var targetColor = t1.Type == TileType.Rainbow ? t2.Type : t1.Type;

        // 消除所有该颜色的方块
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                if (state.GetType(x, y) == targetColor)
                {
                    affected.Add(new Position(x, y));
                }
            }
        }

        // 添加彩球和被交换方块的位置
        affected.Add(p1);
        affected.Add(p2);
    }

    #endregion

    #region 辅助方法

    private BombType GetEffectiveBombType(Tile tile)
    {
        if (tile.Type == TileType.Rainbow || tile.Bomb == BombType.Color)
            return BombType.Color;
        return tile.Bomb;
    }

    private bool IsRocket(BombType type)
    {
        return type == BombType.Horizontal || type == BombType.Vertical;
    }

    private bool IsColorBombWithNormalTile(Tile t1, Tile t2)
    {
        bool t1IsColorBomb = t1.Type == TileType.Rainbow || t1.Bomb == BombType.Color;
        bool t2IsColorBomb = t2.Type == TileType.Rainbow || t2.Bomb == BombType.Color;
        bool t1IsNormal = !t1IsColorBomb && t1.Bomb == BombType.None && t1.Type != TileType.None && t1.Type != TileType.Bomb;
        bool t2IsNormal = !t2IsColorBomb && t2.Bomb == BombType.None && t2.Type != TileType.None && t2.Type != TileType.Bomb;

        return (t1IsColorBomb && t2IsNormal) || (t2IsColorBomb && t1IsNormal);
    }

    private void ApplySmallCross(in GameState state, Position center, HashSet<Position> affected)
    {
        affected.Add(center);
        if (center.X > 0) affected.Add(new Position(center.X - 1, center.Y));
        if (center.X < state.Width - 1) affected.Add(new Position(center.X + 1, center.Y));
        if (center.Y > 0) affected.Add(new Position(center.X, center.Y - 1));
        if (center.Y < state.Height - 1) affected.Add(new Position(center.X, center.Y + 1));
    }

    private void ApplyArea(in GameState state, Position center, int radius, HashSet<Position> affected)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = center.X + dx;
                int y = center.Y + dy;
                if (x >= 0 && x < state.Width && y >= 0 && y < state.Height)
                {
                    affected.Add(new Position(x, y));
                }
            }
        }
    }

    private Position? GetRandomTarget(ref GameState state, Position exclude, HashSet<Position> alreadyAffected)
    {
        var candidates = Pools.ObtainList<Position>();
        try
        {
            for (int y = 0; y < state.Height; y++)
            {
                for (int x = 0; x < state.Width; x++)
                {
                    var pos = new Position(x, y);
                    if (pos.X == exclude.X && pos.Y == exclude.Y) continue;
                    if (alreadyAffected.Contains(pos)) continue;
                    if (state.GetType(x, y) != TileType.None)
                    {
                        candidates.Add(pos);
                    }
                }
            }

            if (candidates.Count > 0)
            {
                int idx = state.Random.Next(0, candidates.Count);
                return candidates[idx];
            }
            return null;
        }
        finally
        {
            Pools.Release(candidates);
        }
    }

    private TileType FindMostFrequentColor(ref GameState state)
    {
        var counts = Pools.Obtain<Dictionary<TileType, int>>();
        try
        {
            for (int i = 0; i < state.Grid.Length; i++)
            {
                var t = state.Grid[i];
                if (t.Type != TileType.None && t.Type != TileType.Rainbow && t.Type != TileType.Bomb)
                {
                    if (!counts.ContainsKey(t.Type)) counts[t.Type] = 0;
                    counts[t.Type]++;
                }
            }

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
            return maxType;
        }
        finally
        {
            counts.Clear();
            Pools.Release(counts);
        }
    }

    #endregion
}
