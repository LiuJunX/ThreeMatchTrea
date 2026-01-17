using System.Collections.Generic;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Selection;

/// <summary>
/// 移动选择器接口
/// 用于从当前游戏状态中选择一个有效的移动
/// </summary>
public interface IMoveSelector
{
    /// <summary>
    /// 选择器名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 尝试获取一个有效的移动
    /// </summary>
    /// <param name="state">当前游戏状态（只读）</param>
    /// <param name="action">输出的移动操作</param>
    /// <returns>如果找到有效移动返回 true</returns>
    bool TryGetMove(in GameState state, out MoveAction action);

    /// <summary>
    /// 获取所有有效的移动候选
    /// </summary>
    /// <param name="state">当前游戏状态（只读）</param>
    /// <returns>所有有效移动的列表（调用者负责释放池化列表）</returns>
    IReadOnlyList<MoveAction> GetAllCandidates(in GameState state);

    /// <summary>
    /// 使缓存失效（棋盘状态改变时调用）
    /// </summary>
    void InvalidateCache();
}
