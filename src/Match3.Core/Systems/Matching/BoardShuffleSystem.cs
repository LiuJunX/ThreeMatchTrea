using System;
using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.Matching;

/// <summary>
/// 负责处理棋盘洗牌逻辑 (Shuffle)
/// 当没有可移动步骤时触发
/// </summary>
public class BoardShuffleSystem : IBoardShuffleSystem
{
    private readonly IDeadlockDetectionSystem _deadlockDetector;

    public BoardShuffleSystem(IDeadlockDetectionSystem deadlockDetector)
    {
        _deadlockDetector = deadlockDetector;
    }

    /// <inheritdoc />
    public bool NeedsShuffle(in GameState state)
    {
        return !_deadlockDetector.HasValidMoves(in state);
    }

    /// <inheritdoc />
    public void Shuffle(ref GameState state, IEventCollector events)
    {
        var (changes, allTiles) = ShuffleAndGetChanges(ref state);
        Pools.Release(changes);
        Pools.Release(allTiles);
    }

    /// <summary>
    /// 执行洗牌并返回变化列表和所有参与 tile 列表
    /// </summary>
    private (List<TileTypeChange> changes, List<ShuffledTileInfo> allTiles) ShuffleAndGetChanges(ref GameState state)
    {
        var types = Pools.ObtainList<ElementType>();
        var oldTypes = Pools.ObtainList<(Position Pos, ElementType OldType, int TileId)>();
        var changes = Pools.ObtainList<TileTypeChange>();
        var allTiles = Pools.ObtainList<ShuffledTileInfo>();

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
                    if (IsShuffleableTileType(tile.Type) && !tile.Type.IsBomb())
                    {
                        types.Add(tile.Type);
                        oldTypes.Add((pos, tile.Type, tile.Id));
                    }
                }
            }

            // 2. 洗牌阶段：使用 Fisher-Yates 算法
            ShuffleTileTypes(types, state.Random);

            // 3. 应用阶段：将洗好的类型填回棋盘
            for (int i = 0; i < oldTypes.Count; i++)
            {
                var (pos, oldType, tileId) = oldTypes[i];
                var newType = types[i];

                // 记录所有参与洗牌的 tile（供动画使用）
                allTiles.Add(new ShuffledTileInfo(tileId, pos, newType));

                if (oldType != newType)
                {
                    // 更新棋盘
                    var tile = state.GetTile(pos);
                    state.SetTile(pos.X, pos.Y, new Tile(tile.Id, newType, tile.Position));

                    // 记录变化
                    changes.Add(new TileTypeChange
                    {
                        TileId = tile.Id,
                        Position = pos,
                        FromType = oldType,
                        ToType = newType
                    });
                }
            }

            return (changes, allTiles);
        }
        finally
        {
            Pools.Release(types);
            Pools.Release(oldTypes);
            // changes 和 allTiles 不释放，由调用者负责
        }
    }

    /// <inheritdoc />
    public bool ShuffleUntilSolvable(ref GameState state, IEventCollector events,
        int maxAttempts = 20, int tick = 0, float simulationTime = 0f)
    {
        List<TileTypeChange>? lastChanges = null;
        List<ShuffledTileInfo>? lastAllTiles = null;
        int attemptCount = 0;
        bool success = false;

        for (int i = 0; i < maxAttempts; i++)
        {
            // 释放上一轮的结果
            if (lastChanges != null) Pools.Release(lastChanges);
            if (lastAllTiles != null) Pools.Release(lastAllTiles);

            var (changes, allTiles) = ShuffleAndGetChanges(ref state);
            lastChanges = changes;
            lastAllTiles = allTiles;
            attemptCount = i + 1;

            // 检查洗牌后是否有解
            if (_deadlockDetector.HasValidMoves(in state))
            {
                success = true;
                break;
            }
        }

        // 只在最终结果时发射一次事件
        if (events.IsEnabled && lastChanges != null && lastAllTiles != null)
        {
            events.Emit(new BoardShuffledEvent
            {
                Tick = tick,
                SimulationTime = simulationTime,
                AttemptCount = attemptCount,
                Changes = new List<TileTypeChange>(lastChanges),
                AllShuffledTiles = new List<ShuffledTileInfo>(lastAllTiles)
            });
        }

        if (lastChanges != null) Pools.Release(lastChanges);
        if (lastAllTiles != null) Pools.Release(lastAllTiles);

        return success;
    }

    /// <summary>
    /// 判断是否是可洗牌的普通色块类型（Red ~ Orange）
    /// </summary>
    private static bool IsShuffleableTileType(ElementType type)
    {
        return type == ElementType.Item1
            || type == ElementType.Item2
            || type == ElementType.Item3
            || type == ElementType.Item4
            || type == ElementType.Item5
            || type == ElementType.Item6;
    }

    /// <summary>
    /// 使用 Fisher-Yates 算法洗牌
    /// </summary>
    private static void ShuffleTileTypes(System.Collections.Generic.List<ElementType> types, Random.IRandom random)
    {
        int n = types.Count;
        while (n > 1)
        {
            n--;
            int k = random.Next(0, n + 1);
            (types[k], types[n]) = (types[n], types[k]);
        }
    }
}
