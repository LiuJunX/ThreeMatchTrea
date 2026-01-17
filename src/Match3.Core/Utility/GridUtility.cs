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
}
