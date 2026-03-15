using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Projectiles;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.PowerUps;

/// <summary>
/// 组合炸弹处理器
///
/// 组合规则：
/// - 火箭 + 火箭 = 十字（1行+1列）
/// - 火箭 + 方块炸弹 = 3行+3列
/// - 火箭 + UFO = 起飞前小十字 + 飞出1个UFO飞弹（落地消除一行或一列，由ProjectileSystem处理）
/// - 火箭 + 彩球 = 最多颜色全变火箭并爆炸
/// - 方块炸弹 + 方块炸弹 = 9x9
/// - 方块炸弹 + UFO = 起飞前小十字 + 飞出1个UFO飞弹（落地消除5x5，由ProjectileSystem处理）
/// - 方块炸弹 + 彩球 = 最多颜色全变3x3炸弹并爆炸
/// - UFO + UFO = 两个原地小十字 + 飞出3个UFO飞弹（由ProjectileSystem处理）
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
        return TryApplyCombo(ref state, p1, p2, affected, out _);
    }

    /// <summary>
    /// 尝试应用组合效果，同时输出完整的 <see cref="ComboResult"/>
    /// （包含爆炸时序和 UFO 发射信息）。
    /// </summary>
    public bool TryApplyCombo(ref GameState state, Position p1, Position p2,
        HashSet<Position> affected, out ComboResult comboResult)
    {
        comboResult = default;
        var t1 = state.GetTile(p1.X, p1.Y);
        var t2 = state.GetTile(p2.X, p2.Y);

        // 检查是否是彩球+普通方块的组合（特殊情况：消除指定颜色）
        if (IsColorBombWithNormalTile(t1, t2))
        {
            ApplyColorBombWithNormalTile(ref state, t1, t2, p1, p2, affected);
            comboResult = new ComboResult { HasColorBomb = true };
            return true;
        }

        // 检查是否两个都是炸弹（或彩球）
        bool isT1Bomb = t1.Type.IsBomb();
        bool isT2Bomb = t2.Type.IsBomb();

        if (!isT1Bomb || !isT2Bomb)
            return false;

        ApplyCombo(ref state, p1, p2, affected);

        comboResult = new ComboResult
        {
            IsDoubleColorBomb = t1.Type == ElementType.ColorBomb && t2.Type == ElementType.ColorBomb,
            HasColorBomb = t1.Type == ElementType.ColorBomb || t2.Type == ElementType.ColorBomb,
            UfoLaunch = BuildUfoLaunchInfo(t1, t2, p1, p2)
        };
        return true;
    }

    /// <summary>
    /// 应用组合效果（假设已确认是有效组合）
    /// </summary>
    public void ApplyCombo(ref GameState state, Position p1, Position p2, HashSet<Position> affected)
    {
        var t1 = state.GetTile(p1.X, p1.Y);
        var t2 = state.GetTile(p2.X, p2.Y);

        var b1 = t1.Type;
        var b2 = t2.Type;

        // 彩球 + 彩球
        if (b1 == ElementType.ColorBomb && b2 == ElementType.ColorBomb)
        {
            ApplyColorPlusColor(ref state, affected);
            return;
        }

        // 彩球 + 其他炸弹
        if (b1 == ElementType.ColorBomb || b2 == ElementType.ColorBomb)
        {
            var colorBombPos = b1 == ElementType.ColorBomb ? p1 : p2;
            var otherBombType = b1 == ElementType.ColorBomb ? b2 : b1;
            var otherTile = b1 == ElementType.ColorBomb ? t2 : t1;
            ApplyColorBombCombo(ref state, colorBombPos, otherBombType, otherTile, affected);
            return;
        }

        // 火箭 + 火箭
        if (b1.IsRocket() && b2.IsRocket())
        {
            ApplyRocketPlusRocket(ref state, p2, affected);
            return;
        }

        // 火箭 + 方块炸弹
        if ((b1.IsRocket() && b2 == ElementType.Square5x5) || (b2.IsRocket() && b1 == ElementType.Square5x5))
        {
            ApplyRocketPlusSquare(ref state, p2, affected);
            return;
        }

        // 火箭 + UFO
        if ((b1.IsRocket() && b2 == ElementType.Ufo) || (b2.IsRocket() && b1 == ElementType.Ufo))
        {
            var ufoPos = b1 == ElementType.Ufo ? p1 : p2;
            ApplyRocketPlusUfo(ref state, ufoPos, affected);
            return;
        }

        // 方块炸弹 + 方块炸弹
        if (b1 == ElementType.Square5x5 && b2 == ElementType.Square5x5)
        {
            ApplySquarePlusSquare(ref state, p2, affected);
            return;
        }

        // 方块炸弹 + UFO
        if ((b1 == ElementType.Square5x5 && b2 == ElementType.Ufo) || (b2 == ElementType.Square5x5 && b1 == ElementType.Ufo))
        {
            var ufoPos = b1 == ElementType.Ufo ? p1 : p2;
            ApplySquarePlusUfo(ref state, ufoPos, affected);
            return;
        }

        // UFO + UFO
        if (b1 == ElementType.Ufo && b2 == ElementType.Ufo)
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
            if (state.IsValid(center.X, y))
            {
                for (int x = 0; x < state.Width; x++)
                    affected.Add(new Position(x, y));
            }
        }

        // 消除3列
        for (int dx = -1; dx <= 1; dx++)
        {
            int x = center.X + dx;
            if (state.IsValid(x, center.Y))
            {
                for (int y = 0; y < state.Height; y++)
                    affected.Add(new Position(x, y));
            }
        }
    }

    /// <summary>
    /// 火箭 + UFO = 起飞前小十字，落地后消除一行或一列
    /// 远程目标由 ProjectileSystem 发射 UfoProjectile 处理（携带 Row/Column 载荷）。
    /// </summary>
    private void ApplyRocketPlusUfo(ref GameState state, Position ufoPos, HashSet<Position> affected)
    {
        // UFO起飞前小十字
        ApplySmallCross(state, ufoPos, affected);

        // 远程目标由 ProjectileSystem 发射 UfoProjectile 处理（携带 Rocket 载荷）
    }

    /// <summary>
    /// 方块炸弹 + 方块炸弹 = 9x9
    /// </summary>
    private void ApplySquarePlusSquare(ref GameState state, Position center, HashSet<Position> affected)
    {
        ApplyArea(state, center, 4, affected); // 半径4 = 9x9
    }

    /// <summary>
    /// 方块炸弹 + UFO = 起飞前小十字，落地后消除5x5
    /// 远程目标由 ProjectileSystem 发射 UfoProjectile 处理（携带 Area5x5 载荷）。
    /// </summary>
    private void ApplySquarePlusUfo(ref GameState state, Position ufoPos, HashSet<Position> affected)
    {
        // UFO起飞前小十字
        ApplySmallCross(state, ufoPos, affected);

        // 远程目标由 ProjectileSystem 发射 UfoProjectile 处理（携带 Area5x5 载荷）
    }

    /// <summary>
    /// UFO + UFO = 两个小十字 + 飞出3个UFO飞弹
    /// 远程目标由 PowerUpHandler 通过 ProjectileSystem 发射，不在此处即时消除。
    /// </summary>
    private void ApplyUfoPlusUfo(ref GameState state, Position p1, Position p2, HashSet<Position> affected)
    {
        // 两个原地小十字
        ApplySmallCross(state, p1, affected);
        ApplySmallCross(state, p2, affected);

        // 远程目标由 ProjectileSystem 发射 UfoProjectile 处理（延迟命中 + 动态追踪）
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
                if (state.IsVoid(x, y)) continue;
                if (state.GetTile(x, y).Type == ElementType.None) continue;
                affected.Add(new Position(x, y));
            }
        }
    }

    /// <summary>
    /// 彩球 + 其他炸弹
    /// </summary>
    private void ApplyColorBombCombo(ref GameState state, Position colorBombPos, ElementType otherBombType, Tile otherTile, HashSet<Position> affected)
    {
        // 找出数量最多的颜色
        var targetColor = FindMostFrequentColor(ref state);
        if (targetColor == ElementType.None)
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
                case ElementType.HorizontalRocket:
                case ElementType.VerticalRocket:
                    // 彩球 + 火箭：每个位置变成火箭并爆炸
                    foreach (var pos in positions)
                    {
                        affected.Add(pos);
                        if (otherBombType == ElementType.HorizontalRocket)
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

                case ElementType.Square5x5:
                    // 彩球 + 方块炸弹：每个位置变成3x3炸弹并爆炸
                    foreach (var pos in positions)
                    {
                        ApplyArea(state, pos, 1, affected); // 半径1 = 3x3
                    }
                    break;

                case ElementType.Ufo:
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
        var targetColor = t1.Type == ElementType.ColorBomb ? t2.Type : t1.Type;

        // 消除所有该颜色的方块
        BombComboHelpers.CollectPositionsOfType(in state, targetColor, affected);

        // 添加彩球和被交换方块的位置
        affected.Add(p1);
        affected.Add(p2);
    }

    #endregion

    #region 辅助方法

    // Delegated to BombComboHelpers static class
    private static bool IsColorBombWithNormalTile(Tile t1, Tile t2) => BombComboHelpers.IsColorBombWithNormalTile(t1, t2);
    private static void ApplySmallCross(in GameState state, Position center, HashSet<Position> affected) => BombComboHelpers.ApplySmallCross(state, center, affected);
    private static void ApplyArea(in GameState state, Position center, int radius, HashSet<Position> affected) => BombComboHelpers.ApplyArea(state, center, radius, affected);
    private static Position? GetRandomTarget(ref GameState state, Position exclude, HashSet<Position> alreadyAffected) => BombComboHelpers.GetRandomTarget(ref state, exclude, alreadyAffected);
    private static ElementType FindMostFrequentColor(ref GameState state) => BombComboHelpers.FindMostFrequentColor(ref state);

    /// <summary>
    /// 根据组合的两颗炸弹类型，判断是否需要发射 UFO 飞弹并构建发射信息。
    /// </summary>
    private static UfoLaunchInfo? BuildUfoLaunchInfo(Tile t1, Tile t2, Position p1, Position p2)
    {
        var b1 = t1.Type;
        var b2 = t2.Type;

        // UFO + UFO: 3 projectiles
        if (b1 == ElementType.Ufo && b2 == ElementType.Ufo)
        {
            return new UfoLaunchInfo
            {
                IsUfoUfoCombo = true,
                UfoTileId = t1.Id, UfoPosition = p1,
                OtherTileId = t2.Id, OtherPosition = p2
            };
        }

        // UFO + Rocket: 1 payload projectile
        if ((b1.IsUfo() && b2.IsRocket()) || (b1.IsRocket() && b2.IsUfo()))
        {
            var ufo = b1.IsUfo() ? t1 : t2;
            var other = b1.IsUfo() ? t2 : t1;
            var ufoPos = b1.IsUfo() ? p1 : p2;
            var otherPos = b1.IsUfo() ? p2 : p1;
            return new UfoLaunchInfo
            {
                UfoTileId = ufo.Id, UfoPosition = ufoPos,
                OtherTileId = other.Id, OtherPosition = otherPos,
                Payload = other.Type == ElementType.HorizontalRocket ? UfoPayload.Row : UfoPayload.Column
            };
        }

        // UFO + Square: 1 payload projectile
        if ((b1.IsUfo() && b2.IsAreaBomb()) || (b1.IsAreaBomb() && b2.IsUfo()))
        {
            var ufo = b1.IsUfo() ? t1 : t2;
            var other = b1.IsUfo() ? t2 : t1;
            var ufoPos = b1.IsUfo() ? p1 : p2;
            var otherPos = b1.IsUfo() ? p2 : p1;
            return new UfoLaunchInfo
            {
                UfoTileId = ufo.Id, UfoPosition = ufoPos,
                OtherTileId = other.Id, OtherPosition = otherPos,
                Payload = UfoPayload.Area5x5
            };
        }

        return null;
    }

    #endregion
}
