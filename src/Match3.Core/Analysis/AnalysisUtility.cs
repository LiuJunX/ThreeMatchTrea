using System;
using System.Collections.Generic;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Objectives;
using Match3.Random;

namespace Match3.Core.Analysis;

/// <summary>
/// 分析服务共享工具类
/// </summary>
internal static class AnalysisUtility
{
    private static readonly TileType[] AllTileTypes =
    {
        TileType.Red, TileType.Blue, TileType.Green,
        TileType.Yellow, TileType.Purple, TileType.Orange
    };

    /// <summary>
    /// 从 LevelData 创建初始状态
    /// </summary>
    public static GameState CreateInitialState(LevelData levelData, ulong seed = 12345)
    {
        var random = new XorShift64(seed);
        var state = new GameState(levelData.Width, levelData.Height, levelData.TileTypesCount, random)
        {
            MoveLimit = levelData.MoveLimit
        };

        var types = GetTileTypes(levelData.TileTypesCount);
        for (int y = 0; y < levelData.Height; y++)
        {
            for (int x = 0; x < levelData.Width; x++)
            {
                int idx = y * levelData.Width + x;
                TileType type;
                do
                {
                    type = types[random.Next(types.Length)];
                } while (WouldCreateMatch(in state, x, y, type));

                state.SetTile(x, y, new Tile(idx + 1, type, x, y));
            }
        }

        return state;
    }

    /// <summary>
    /// 从 LevelConfig 创建初始状态
    /// </summary>
    public static GameState CreateInitialStateFromConfig(LevelConfig levelConfig, XorShift64 random)
    {
        int tileTypesCount = CountDistinctTileTypes(levelConfig.Grid);
        if (tileTypesCount == 0) tileTypesCount = 6;

        var state = new GameState(levelConfig.Width, levelConfig.Height, tileTypesCount, random)
        {
            MoveLimit = levelConfig.MoveLimit,
            TargetDifficulty = levelConfig.TargetDifficulty
        };

        for (int y = 0; y < levelConfig.Height; y++)
        {
            for (int x = 0; x < levelConfig.Width; x++)
            {
                int idx = y * levelConfig.Width + x;

                var type = levelConfig.Grid[idx];
                var bomb = BombType.None;
                if (levelConfig.Bombs != null && idx < levelConfig.Bombs.Length)
                {
                    bomb = levelConfig.Bombs[idx];
                }

                if (type == TileType.None)
                {
                    var types = GetTileTypes(tileTypesCount);
                    do
                    {
                        type = types[random.Next(types.Length)];
                    } while (WouldCreateMatch(in state, x, y, type));
                }

                state.SetTile(x, y, new Tile(state.NextTileId++, type, x, y, bomb));

                if (levelConfig.Grounds != null && idx < levelConfig.Grounds.Length)
                {
                    var groundType = levelConfig.Grounds[idx];
                    if (groundType != GroundType.None)
                    {
                        byte health = GroundRules.GetDefaultHealth(groundType);
                        if (levelConfig.GroundHealths != null && idx < levelConfig.GroundHealths.Length && levelConfig.GroundHealths[idx] > 0)
                        {
                            health = levelConfig.GroundHealths[idx];
                        }
                        state.SetGround(x, y, new Ground(groundType, health));
                    }
                }

                if (levelConfig.Covers != null && idx < levelConfig.Covers.Length)
                {
                    var coverType = levelConfig.Covers[idx];
                    if (coverType != CoverType.None)
                    {
                        byte health = CoverRules.GetDefaultHealth(coverType);
                        if (levelConfig.CoverHealths != null && idx < levelConfig.CoverHealths.Length && levelConfig.CoverHealths[idx] > 0)
                        {
                            health = levelConfig.CoverHealths[idx];
                        }
                        bool isDynamic = CoverRules.IsDynamicType(coverType);
                        state.SetCover(x, y, new Cover(coverType, health, isDynamic));
                    }
                }
            }
        }

        var objectiveSystem = new LevelObjectiveSystem();
        objectiveSystem.Initialize(ref state, levelConfig);

        return state;
    }

    /// <summary>
    /// 统计关卡中使用的不同颜色数量
    /// </summary>
    public static int CountDistinctTileTypes(TileType[]? grid)
    {
        if (grid == null || grid.Length == 0) return 0;

        var seen = new HashSet<TileType>();
        foreach (var type in grid)
        {
            if (type != TileType.None && type != TileType.Rainbow)
            {
                seen.Add(type);
            }
        }
        return seen.Count;
    }

    /// <summary>
    /// 获取指定数量的棋子类型
    /// </summary>
    public static TileType[] GetTileTypes(int count)
    {
        var result = new TileType[Math.Min(count, AllTileTypes.Length)];
        Array.Copy(AllTileTypes, result, result.Length);
        return result;
    }

    /// <summary>
    /// 检查在指定位置放置指定类型是否会产生匹配
    /// </summary>
    public static bool WouldCreateMatch(in GameState state, int x, int y, TileType type)
    {
        if (x >= 2 &&
            state.GetType(x - 1, y) == type &&
            state.GetType(x - 2, y) == type)
            return true;

        if (y >= 2 &&
            state.GetType(x, y - 1) == type &&
            state.GetType(x, y - 2) == type)
            return true;

        return false;
    }

    /// <summary>
    /// 计算目标完成进度 (0-1)
    /// </summary>
    public static float CalculateObjectiveProgress(in GameState state)
    {
        float totalProgress = 0;
        int activeObjectives = 0;

        for (int i = 0; i < 4; i++)
        {
            var obj = state.ObjectiveProgress[i];
            if (obj.TargetCount > 0)
            {
                totalProgress += Math.Min(1f, (float)obj.CurrentCount / obj.TargetCount);
                activeObjectives++;
            }
        }

        return activeObjectives > 0 ? totalProgress / activeObjectives : 0;
    }

    /// <summary>
    /// 统计棋盘上的棋子数量
    /// </summary>
    public static int CountTiles(in GameState state)
    {
        int count = 0;
        for (int i = 0; i < state.Grid.Length; i++)
        {
            if (state.Grid[i].Type != TileType.None) count++;
        }
        return count;
    }
}
