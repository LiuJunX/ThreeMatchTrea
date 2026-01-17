using System;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Selection;

namespace Match3.Core.Systems.Input;

/// <summary>
/// Bot 系统接口
/// </summary>
[Obsolete("请使用 IMoveSelector 接口代替。IBotSystem 将在后续版本移除。")]
public interface IBotSystem
{
    /// <summary>
    /// 尝试获取一个随机有效移动
    /// </summary>
    [Obsolete("请使用 IMoveSelector.TryGetMove() 代替")]
    bool TryGetRandomMove(ref GameState state, IInteractionSystem interactionSystem, out Move move);
}

/// <summary>
/// IBotSystem 的扩展方法，提供与 IMoveSelector 的桥接
/// </summary>
public static class BotSystemExtensions
{
    /// <summary>
    /// 将 IMoveSelector 适配为 IBotSystem 的调用方式
    /// </summary>
    public static bool TryGetMove(this IMoveSelector selector, in GameState state, out Move move)
    {
        move = default;
        if (selector.TryGetMove(in state, out var action))
        {
            move = action.ToMove();
            return true;
        }
        return false;
    }
}
