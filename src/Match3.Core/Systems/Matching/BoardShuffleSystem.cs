using System;
using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.Matching;

/// <summary>
/// 棋盘洗牌系统实现
/// 使用 Fisher-Yates 算法重新洗牌普通色块，同时保留特殊棋子和 Cover 阻挡
/// </summary>
public sealed class BoardShuffleSystem : IBoardShuffleSystem
{
    private readonly IDeadlockDetectionSystem _deadlockDetector;

    /// <summary>
    /// 创建棋盘洗牌系统
    /// </summary>
    /// <param name="deadlockDetector">死锁检测系统，用于验证洗牌后有可行移动</param>
    public BoardShuffleSystem(IDeadlockDetectionSystem deadlockDetector)
    {
        _deadlockDetector = deadlockDetector ?? throw new ArgumentNullException(nameof(deadlockDetector));
    }

    /// <inheritdoc />
    public void Shuffle(ref GameState state, IEventCollector events)
    {
        var changes = ShuffleAndGetChanges(ref state);
        Pools.Release(changes); // 不需要保留，调用者如果需要会用 ShuffleUntilSolvable
    }

    /// <summary>
    /// 执行洗牌并返回变化列表
    /// </summary>
    private List<TileTypeChange> ShuffleAndGetChanges(ref GameState state)
    {
        var types = Pools.ObtainList<TileType>();
        var oldTypes = Pools.ObtainList<(Position Pos, TileType OldType, long TileId)>();
        var changes = Pools.ObtainList<TileTypeChange>();

        try
        {
            // 1. 收集阶段：收集所有可洗牌的普通色块类型及其位置
            for (int y = 0; y < state.Height; y++)
            {
                for (int x = 0; x < state.Width; x++)
                {
                    var pos = new Position(x, y);

                    // 跳过被 Cover 阻挡的位置
                    if (!state.CanMatch(pos))
                        continue;

                    var tile = state.GetTile(pos);

                    // 只收集普通色块（排除特殊棋子）
                    if (IsShuffleableTileType(tile.Type) && tile.Bomb == BombType.None)
                    {
                        types.Add(tile.Type);
                        oldTypes.Add((pos, tile.Type, tile.Id));
                    }
                }
            }

            // 2. 洗牌阶段：使用 Fisher-Yates 算法
            ShuffleTileTypes(types, state.Random);

            // 2.5 智能调整：确保洗牌后有有效移动配置
            EnsureValidMoveInTypes(types, state.Width, state.Height);

            // 3. 分配阶段：重新分配洗牌后的类型到棋盘，并记录变化
            int index = 0;
            for (int y = 0; y < state.Height; y++)
            {
                for (int x = 0; x < state.Width; x++)
                {
                    var pos = new Position(x, y);

                    if (!state.CanMatch(pos))
                        continue;

                    var tile = state.GetTile(pos);

                    if (IsShuffleableTileType(tile.Type) && tile.Bomb == BombType.None)
                    {
                        var newType = types[index];
                        var oldInfo = oldTypes[index];
                        index++;

                        // 只记录实际改变的棋子
                        if (oldInfo.OldType != newType)
                        {
                            changes.Add(new TileTypeChange(tile.Id, pos, newType));
                        }

                        tile.Type = newType;
                        state.SetTile(pos, tile);
                    }
                }
            }

            return changes;
        }
        finally
        {
            Pools.Release(types);
            Pools.Release(oldTypes);
        }
    }

    /// <inheritdoc />
    public bool ShuffleUntilSolvable(ref GameState state, IEventCollector events, int maxAttempts = 10)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var changes = ShuffleAndGetChanges(ref state);

            try
            {
                // 发射洗牌事件（包含变化信息）
                if (events.IsEnabled)
                {
                    events.Emit(new BoardShuffledEvent
                    {
                        AttemptCount = attempt,
                        ScoreBefore = state.Score,
                        Changes = changes.ToArray()
                    });
                }

                // 检查是否有可行移动
                if (_deadlockDetector.HasValidMoves(in state))
                {
                    return true;
                }
            }
            finally
            {
                Pools.Release(changes);
            }
        }

        // 达到最大尝试次数仍无解
        return false;
    }

    /// <summary>
    /// 判断是否是可洗牌的普通色块类型（Red ~ Orange）
    /// </summary>
    private static bool IsShuffleableTileType(TileType type)
    {
        return type == TileType.Red
            || type == TileType.Green
            || type == TileType.Blue
            || type == TileType.Yellow
            || type == TileType.Purple
            || type == TileType.Orange;
    }

    /// <summary>
    /// 使用 Fisher-Yates 算法洗牌
    /// </summary>
    private static void ShuffleTileTypes(System.Collections.Generic.List<TileType> types, Random.IRandom random)
    {
        int n = types.Count;
        while (n > 1)
        {
            n--;
            int k = random.Next(0, n + 1);
            (types[k], types[n]) = (types[n], types[k]);
        }
    }

    /// <summary>
    /// 智能调整：确保 types 列表分配到棋盘后至少有一个有效移动
    /// 策略：找到数量>=3的颜色，确保其中2个相邻，第3个在可交换位置
    /// </summary>
    private static void EnsureValidMoveInTypes(List<TileType> types, int width, int height)
    {
        if (types.Count < 3 || width < 2 || height < 2)
            return;

        // 统计每种颜色的数量和位置
        var colorPositions = new Dictionary<TileType, List<int>>();
        for (int i = 0; i < types.Count; i++)
        {
            var type = types[i];
            if (!colorPositions.ContainsKey(type))
                colorPositions[type] = new List<int>();
            colorPositions[type].Add(i);
        }

        // 找到数量 >= 3 的颜色
        TileType? targetColor = null;
        List<int>? targetPositions = null;
        foreach (var kvp in colorPositions)
        {
            if (kvp.Value.Count >= 3)
            {
                targetColor = kvp.Key;
                targetPositions = kvp.Value;
                break;
            }
        }

        if (targetColor == null || targetPositions == null)
            return; // 没有足够的同色方块，无法保证有效移动

        // 创建有效移动配置：
        // 配置 A（垂直）：位置 (0,0), (0,1), (0,2) 放目标颜色
        //   - 已经是垂直3连，会直接消除 - 不好
        //
        // 配置 B（L形）：位置 (0,0), (0,1), (1,0) 放目标颜色
        //   - 交换 (1,0) 和 (1,1) 可形成垂直3连
        //   - 或交换 (0,1) 和 (1,1) 可形成水平3连
        //
        // 使用配置 B
        int pos0 = 0;                    // (0, 0)
        int pos1 = width;                // (0, 1)
        int pos2 = 1;                    // (1, 0)

        if (pos1 >= types.Count)
        {
            // 只有一行，尝试水平配置
            // 位置 0, 1 相邻，位置 2 在旁边但不连续
            // 例如 (0,0), (1,0), (2,0) - 但这是3连，不好
            // 换成 (0,0), (2,0) 相邻需要中间的，这不可能
            // 对于只有一行的情况，只能放弃
            return;
        }

        // 确保目标颜色在正确位置
        EnsureColorAtPosition(types, targetPositions, pos0);
        EnsureColorAtPosition(types, targetPositions, pos1);
        EnsureColorAtPosition(types, targetPositions, pos2);
    }

    /// <summary>
    /// 确保目标颜色在指定位置
    /// </summary>
    private static void EnsureColorAtPosition(List<TileType> types, List<int> targetPositions, int desiredPos)
    {
        if (desiredPos >= types.Count)
            return;

        // 检查目标位置是否已经是目标颜色
        if (targetPositions.Contains(desiredPos))
            return;

        // 找到一个目标颜色的位置来交换
        int sourcePos = -1;
        for (int i = 0; i < targetPositions.Count; i++)
        {
            int pos = targetPositions[i];
            // 选择一个不在已使用位置的
            if (pos != desiredPos)
            {
                sourcePos = pos;
                break;
            }
        }

        if (sourcePos == -1)
            return;

        // 交换
        (types[sourcePos], types[desiredPos]) = (types[desiredPos], types[sourcePos]);

        // 更新位置列表
        targetPositions.Remove(sourcePos);
        targetPositions.Add(desiredPos);
    }
}
