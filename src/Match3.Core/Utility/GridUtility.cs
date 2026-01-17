using System;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;

namespace Match3.Core.Utility;

/// <summary>
/// 棋盘格子操作工具类
/// </summary>
public static class GridUtility
{
    /// <summary>
    /// 临时交换两个位置的 Tile，仅用于匹配检测。
    /// 不修改 Tile.Position 属性，避免与动画系统冲突。
    /// 调用后需要再次调用以交换回来。
    /// </summary>
    /// <param name="state">游戏状态</param>
    /// <param name="a">位置 A</param>
    /// <param name="b">位置 B</param>
    public static void SwapTilesForCheck(ref GameState state, Position a, Position b)
    {
        var idxA = a.Y * state.Width + a.X;
        var idxB = b.Y * state.Width + b.X;
        (state.Grid[idxA], state.Grid[idxB]) = (state.Grid[idxB], state.Grid[idxA]);
    }

    /// <summary>
    /// 检查两个位置之间的交换是否基本有效（不检查匹配）。
    /// 验证：非空、非下落、可交互。
    /// </summary>
    /// <param name="state">游戏状态</param>
    /// <param name="from">起始位置</param>
    /// <param name="to">目标位置</param>
    /// <returns>如果交换基本有效返回 true</returns>
    public static bool IsSwapValid(in GameState state, Position from, Position to)
    {
        // 边界检查
        if (from.X < 0 || from.X >= state.Width || from.Y < 0 || from.Y >= state.Height)
            return false;
        if (to.X < 0 || to.X >= state.Width || to.Y < 0 || to.Y >= state.Height)
            return false;

        var tileFrom = state.GetTile(from.X, from.Y);
        var tileTo = state.GetTile(to.X, to.Y);

        // 不能交换空格
        if (tileFrom.Type == TileType.None || tileTo.Type == TileType.None)
            return false;

        // 不能交换正在下落的方块
        if (tileFrom.IsFalling || tileTo.IsFalling)
            return false;

        // 不能交换被覆盖层阻挡的方块
        if (!state.CanInteract(from) || !state.CanInteract(to))
            return false;

        return true;
    }

    /// <summary>
    /// 检查位置是否相邻（上下左右）
    /// </summary>
    public static bool AreAdjacent(Position a, Position b)
    {
        int dx = Math.Abs(a.X - b.X);
        int dy = Math.Abs(a.Y - b.Y);
        return (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
    }

    /// <summary>
    /// 获取指定方向的相邻位置
    /// </summary>
    public static Position GetNeighbor(Position pos, Direction direction)
    {
        return direction switch
        {
            Direction.Up => new Position(pos.X, pos.Y - 1),
            Direction.Down => new Position(pos.X, pos.Y + 1),
            Direction.Left => new Position(pos.X - 1, pos.Y),
            Direction.Right => new Position(pos.X + 1, pos.Y),
            _ => pos
        };
    }
}
