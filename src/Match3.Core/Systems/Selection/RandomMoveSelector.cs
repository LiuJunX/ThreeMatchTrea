using System;
using System.Collections.Generic;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching;
using Match3.Core.Utility;
using Match3.Core.Utility.Pools;
using Match3.Random;

namespace Match3.Core.Systems.Selection;

/// <summary>
/// 随机移动选择器
/// 通过随机尝试位置和方向来查找有效移动
/// 适用于简单的 AI 对手或快速测试
/// </summary>
public sealed class RandomMoveSelector : IMoveSelector
{
    private static readonly Direction[] Directions = { Direction.Up, Direction.Down, Direction.Left, Direction.Right };

    private readonly IMatchFinder _matchFinder;
    private readonly MoveSelectionConfig _config;

    public string Name => "Random";

    /// <summary>
    /// 创建随机移动选择器
    /// </summary>
    /// <param name="matchFinder">匹配检测器</param>
    /// <param name="config">配置（可选，使用默认配置）</param>
    public RandomMoveSelector(IMatchFinder matchFinder, MoveSelectionConfig? config = null)
    {
        _matchFinder = matchFinder ?? throw new ArgumentNullException(nameof(matchFinder));
        _config = config ?? MoveSelectionConfig.Default;
    }

    /// <inheritdoc />
    public bool TryGetMove(in GameState state, out MoveAction action)
    {
        action = default;

        // 获取状态的随机数生成器
        var random = state.Random;
        int attempts = _config.RandomSelector.MaxAttempts;
        int w = state.Width;
        int h = state.Height;

        // 需要创建可修改的状态副本用于临时交换检测
        var stateCopy = state;

        for (int i = 0; i < attempts; i++)
        {
            int x = random.Next(0, w);
            int y = random.Next(0, h);
            var pos = new Position(x, y);

            foreach (var dir in Directions)
            {
                var neighbor = GridUtility.GetNeighbor(pos, dir);

                // 检查基本有效性
                if (!GridUtility.IsSwapValid(in state, pos, neighbor))
                    continue;

                // 临时交换检测匹配
                GridUtility.SwapTilesForCheck(ref stateCopy, pos, neighbor);
                bool hasMatch = _matchFinder.HasMatchAt(in stateCopy, pos) ||
                               _matchFinder.HasMatchAt(in stateCopy, neighbor);
                GridUtility.SwapTilesForCheck(ref stateCopy, pos, neighbor); // 交换回来

                if (hasMatch)
                {
                    action = MoveAction.Swap(pos, neighbor, _config.Weights.Normal);
                    return true;
                }
            }
        }

        return false;
    }

    /// <inheritdoc />
    public IReadOnlyList<MoveAction> GetAllCandidates(in GameState state)
    {
        var candidates = Pools.ObtainList<MoveAction>();
        var stateCopy = state;

        // 水平交换
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width - 1; x++)
            {
                var from = new Position(x, y);
                var to = new Position(x + 1, y);

                if (!GridUtility.IsSwapValid(in state, from, to))
                    continue;

                GridUtility.SwapTilesForCheck(ref stateCopy, from, to);
                bool hasMatch = _matchFinder.HasMatchAt(in stateCopy, from) ||
                               _matchFinder.HasMatchAt(in stateCopy, to);
                GridUtility.SwapTilesForCheck(ref stateCopy, from, to);

                if (hasMatch)
                {
                    candidates.Add(MoveAction.Swap(from, to, _config.Weights.Normal));
                }
            }
        }

        // 垂直交换
        for (int y = 0; y < state.Height - 1; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                var from = new Position(x, y);
                var to = new Position(x, y + 1);

                if (!GridUtility.IsSwapValid(in state, from, to))
                    continue;

                GridUtility.SwapTilesForCheck(ref stateCopy, from, to);
                bool hasMatch = _matchFinder.HasMatchAt(in stateCopy, from) ||
                               _matchFinder.HasMatchAt(in stateCopy, to);
                GridUtility.SwapTilesForCheck(ref stateCopy, from, to);

                if (hasMatch)
                {
                    candidates.Add(MoveAction.Swap(from, to, _config.Weights.Normal));
                }
            }
        }

        return candidates;
    }

    /// <inheritdoc />
    public void InvalidateCache()
    {
        // RandomMoveSelector 不使用缓存
    }
}
