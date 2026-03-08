using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Utility;

/// <summary>
/// 表示一个有效移动
/// </summary>
public readonly struct ValidMove
{
    public Position From { get; }
    public Position To { get; }

    public ValidMove(Position from, Position to)
    {
        From = from;
        To = to;
    }

    public void Deconstruct(out Position from, out Position to)
    {
        from = From;
        to = To;
    }
}

/// <summary>
/// 有效移动检测工具
/// 提供快速检测和枚举棋盘上所有可行移动的功能
/// </summary>
public static class ValidMoveDetector
{
    /// <summary>
    /// 快速检查是否存在至少一个有效移动（早期退出优化）
    /// 包括：交换产生匹配、炸弹点击、炸弹交换
    /// </summary>
    /// <param name="state">游戏状态</param>
    /// <param name="matchFinder">匹配检测器</param>
    /// <returns>如果存在至少一个有效移动则返回 true，否则返回 false</returns>
    public static bool HasValidMoves(in GameState state, IMatchFinder matchFinder)
    {
        // 0. 检查可点击的炸弹（点击即激活，不需要交换）
        if (HasTappableBomb(in state))
            return true;

        var stateCopy = state;

        // 检查所有水平相邻对
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width - 1; x++)
            {
                var from = new Position(x, y);
                var to = new Position(x + 1, y);

                if (IsValidSwapMove(in state, ref stateCopy, from, to, matchFinder))
                    return true;
            }
        }

        // 检查所有垂直相邻对
        for (int y = 0; y < state.Height - 1; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var from = new Position(x, y);
                var to = new Position(x, y + 1);

                if (IsValidSwapMove(in state, ref stateCopy, from, to, matchFinder))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 查找所有有效移动（用于调试、测试、AI）
    /// 包括：交换产生匹配、炸弹交换
    /// 注意：炸弹点击不包含在结果中（不是交换操作），使用 HasTappableBomb 检测
    /// </summary>
    /// <param name="state">游戏状态</param>
    /// <param name="matchFinder">匹配检测器</param>
    /// <returns>所有有效移动的列表，调用者负责使用 Pools.Release() 释放</returns>
    public static List<ValidMove> FindAllValidMoves(
        in GameState state, IMatchFinder matchFinder)
    {
        var validMoves = Match3.Core.Utility.Pools.Pools.ObtainList<ValidMove>();
        var stateCopy = state;

        // 水平交换
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width - 1; x++)
            {
                var from = new Position(x, y);
                var to = new Position(x + 1, y);

                if (IsValidSwapMove(in state, ref stateCopy, from, to, matchFinder))
                    validMoves.Add(new ValidMove(from, to));
            }
        }

        // 垂直交换
        for (int y = 0; y < state.Height - 1; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var from = new Position(x, y);
                var to = new Position(x, y + 1);

                if (IsValidSwapMove(in state, ref stateCopy, from, to, matchFinder))
                    validMoves.Add(new ValidMove(from, to));
            }
        }

        return validMoves;
    }

    /// <summary>
    /// 检查棋盘上是否有可点击激活的炸弹
    /// </summary>
    public static bool HasTappableBomb(in GameState state)
    {
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var tile = state.GetTile(x, y);
                if (tile.Type.IsBomb()
                    && state.CanInteract(new Position(x, y)))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 判定一次交换是否为有效移动。
    /// 有效条件：交换产生颜色匹配，或任一方是炸弹（炸弹交换始终激活）。
    /// </summary>
    private static bool IsValidSwapMove(
        in GameState state, ref GameState stateCopy,
        Position from, Position to, IMatchFinder matchFinder)
    {
        if (!GridUtility.IsSwapValid(in state, from, to))
            return false;

        var tileA = state.GetTile(from.X, from.Y);
        var tileB = state.GetTile(to.X, to.Y);

        // 炸弹交换始终有效（激活炸弹效果或触发 combo）
        if (tileA.Type.IsBomb() || tileB.Type.IsBomb())
            return true;

        // 普通交换：检查是否产生匹配
        GridUtility.SwapTilesForCheck(ref stateCopy, from, to);
        bool hasMatch = matchFinder.HasMatchAt(in stateCopy, from) ||
                       matchFinder.HasMatchAt(in stateCopy, to);
        GridUtility.SwapTilesForCheck(ref stateCopy, from, to);

        return hasMatch;
    }
}
