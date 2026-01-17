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
/// 加权移动选择器
/// 支持穷举搜索、加权随机选择、炸弹点击等功能
/// 适用于 Auto Play 和高级 AI
/// </summary>
public sealed class WeightedMoveSelector : IMoveSelector
{
    private readonly IMatchFinder _matchFinder;
    private readonly IRandom _random;
    private readonly MoveSelectionConfig _config;
    private readonly MoveSelectionConfig.WeightConfig _weights;

    // 缓存
    private List<MoveAction>? _cachedCandidates;
    private bool _cacheValid;

    public string Name => "Weighted";

    /// <summary>
    /// 创建加权移动选择器
    /// </summary>
    /// <param name="matchFinder">匹配检测器</param>
    /// <param name="random">随机数生成器</param>
    /// <param name="config">配置（可选，使用默认配置）</param>
    public WeightedMoveSelector(IMatchFinder matchFinder, IRandom random, MoveSelectionConfig? config = null)
    {
        _matchFinder = matchFinder ?? throw new ArgumentNullException(nameof(matchFinder));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _config = config ?? MoveSelectionConfig.Default;
        _weights = _config.Weights;
    }

    /// <inheritdoc />
    public bool TryGetMove(in GameState state, out MoveAction action)
    {
        action = default;

        var candidates = GetAllCandidatesInternal(in state);
        if (candidates.Count == 0)
        {
            ReleaseCandidates(candidates);
            return false;
        }

        action = WeightedRandomSelect(candidates, _random);
        ReleaseCandidates(candidates);
        return true;
    }

    /// <inheritdoc />
    public IReadOnlyList<MoveAction> GetAllCandidates(in GameState state)
    {
        return GetAllCandidatesInternal(in state);
    }

    /// <inheritdoc />
    public void InvalidateCache()
    {
        _cacheValid = false;
        if (_cachedCandidates != null)
        {
            Pools.Release(_cachedCandidates);
            _cachedCandidates = null;
        }
    }

    private List<MoveAction> GetAllCandidatesInternal(in GameState state)
    {
        // 使用缓存
        if (_config.WeightedSelector.EnableCaching && _cacheValid && _cachedCandidates != null)
        {
            // 返回缓存的副本
            var copy = Pools.ObtainList<MoveAction>();
            copy.AddRange(_cachedCandidates);
            return copy;
        }

        var candidates = Pools.ObtainList<MoveAction>();
        var stateCopy = state;

        // 1. 搜索所有有效交换（水平）
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width - 1; x++)
            {
                TryAddSwapCandidate(ref stateCopy, new Position(x, y), new Position(x + 1, y), candidates);
            }
        }

        // 2. 搜索所有有效交换（垂直）
        for (int y = 0; y < state.Height - 1; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                TryAddSwapCandidate(ref stateCopy, new Position(x, y), new Position(x, y + 1), candidates);
            }
        }

        // 3. 搜索可点击的炸弹
        if (_config.WeightedSelector.EnableTapBombs)
        {
            for (int y = 0; y < state.Height; y++)
            {
                for (int x = 0; x < state.Width; x++)
                {
                    var pos = new Position(x, y);
                    var tile = state.GetTile(x, y);

                    if (IsTappableBomb(in tile) && state.CanInteract(pos) && !tile.IsFalling)
                    {
                        candidates.Add(MoveAction.Tap(pos, _weights.GetWeight(tile.Bomb)));
                    }
                }
            }
        }

        // 更新缓存
        if (_config.WeightedSelector.EnableCaching)
        {
            _cachedCandidates ??= Pools.ObtainList<MoveAction>();
            _cachedCandidates.Clear();
            _cachedCandidates.AddRange(candidates);
            _cacheValid = true;
        }

        return candidates;
    }

    private void TryAddSwapCandidate(ref GameState state, Position from, Position to, List<MoveAction> candidates)
    {
        // 使用共享的有效性验证
        if (!GridUtility.IsSwapValid(in state, from, to))
            return;

        var tileA = state.GetTile(from.X, from.Y);
        var tileB = state.GetTile(to.X, to.Y);

        int weightA = _weights.GetWeight(tileA.Bomb);
        int weightB = _weights.GetWeight(tileB.Bomb);
        bool isBombA = tileA.Bomb != BombType.None;
        bool isBombB = tileB.Bomb != BombType.None;

        int weight;
        if (isBombA && isBombB)
        {
            // 炸弹+炸弹：使用乘法或加法权重
            weight = _config.WeightedSelector.UseBombMultiplier
                ? weightA * weightB
                : weightA + weightB;
        }
        else
        {
            // 普通消除或炸弹+普通：检查匹配并计算新炸弹权重
            GridUtility.SwapTilesForCheck(ref state, from, to);

            var foci = new[] { from, to };
            var matchGroups = _matchFinder.FindMatchGroups(in state, foci);

            GridUtility.SwapTilesForCheck(ref state, from, to); // 交换回来

            if (matchGroups.Count == 0)
            {
                ClassicMatchFinder.ReleaseGroups(matchGroups);
                return; // 无匹配
            }

            // 基础权重
            weight = isBombA || isBombB ? weightA + weightB : _weights.Normal;

            // 加上将生成的新炸弹权重
            foreach (var group in matchGroups)
            {
                if (group.SpawnBombType != BombType.None)
                {
                    weight += _weights.GetWeight(group.SpawnBombType);
                }
            }

            ClassicMatchFinder.ReleaseGroups(matchGroups);
        }

        candidates.Add(MoveAction.Swap(from, to, weight));
    }

    private static bool IsTappableBomb(in Tile tile)
    {
        return tile.Bomb != BombType.None;
    }

    private static MoveAction WeightedRandomSelect(List<MoveAction> actions, IRandom random)
    {
        int totalWeight = 0;
        foreach (var action in actions)
        {
            totalWeight += action.Weight;
        }

        if (totalWeight <= 0)
            return actions[0];

        int randomValue = random.Next(0, totalWeight);
        int cumulative = 0;

        foreach (var action in actions)
        {
            cumulative += action.Weight;
            if (randomValue < cumulative)
            {
                return action;
            }
        }

        return actions[^1]; // fallback
    }

    private void ReleaseCandidates(List<MoveAction> candidates)
    {
        // 不释放缓存的列表
        if (candidates != _cachedCandidates)
        {
            Pools.Release(candidates);
        }
    }
}
