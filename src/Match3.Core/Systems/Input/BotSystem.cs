using System;
using Match3.Core.Config;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Selection;

namespace Match3.Core.Systems.Input;

/// <summary>
/// Bot 系统实现
/// 现在内部使用 RandomMoveSelector，保留此类是为了向后兼容
/// </summary>
[Obsolete("请使用 RandomMoveSelector 代替。BotSystem 将在后续版本移除。")]
public class BotSystem : IBotSystem
{
    private readonly RandomMoveSelector _selector;

    public BotSystem(IMatchFinder matchFinder)
    {
        _selector = new RandomMoveSelector(matchFinder);
    }

    public BotSystem(IMatchFinder matchFinder, MoveSelectionConfig config)
    {
        _selector = new RandomMoveSelector(matchFinder, config);
    }

    /// <summary>
    /// 获取内部使用的 IMoveSelector
    /// </summary>
    public IMoveSelector Selector => _selector;

    [Obsolete("请使用 Selector.TryGetMove() 代替")]
    public bool TryGetRandomMove(ref GameState state, IInteractionSystem interactionSystem, out Move move)
    {
        move = default;

        // 使用新的选择器
        if (_selector.TryGetMove(in state, out var action))
        {
            move = action.ToMove();
            return true;
        }

        return false;
    }
}
