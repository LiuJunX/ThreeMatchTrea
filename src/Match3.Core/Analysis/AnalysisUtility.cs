using System;
using System.Collections.Generic;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Objectives;
using Match3.Core.Systems.Obstacles;
using Match3.Random;

namespace Match3.Core.Analysis;

/// <summary>
/// 分析服务共享工具类
/// </summary>
internal static class AnalysisUtility
{
    private static readonly ElementType[] AllTileTypes =
    {
        ElementType.Item1, ElementType.Item2, ElementType.Item3,
        ElementType.Item4, ElementType.Item5, ElementType.Item6
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
                ElementType type;
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
        int tileTypesCount = levelConfig.TileTypesCount
            ?? CountDistinctTileTypes(levelConfig.Grid);
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

                // 1. Cell layer (Structure)
                if (levelConfig.Cells != null && idx < levelConfig.Cells.Length)
                {
                    state.SetCell(x, y, levelConfig.Cells[idx]);
                }

                var cellKind = state.GetCell(x, y);
                if (cellKind == CellKind.Void || cellKind == CellKind.Wall)
                    continue;

                // 2. Obstacle layer (before tile — obstacle occupies the cell)
                if (levelConfig.Obstacles != null && idx < levelConfig.Obstacles.Length)
                {
                    var obstacleType = levelConfig.Obstacles[idx];
                    if (obstacleType != ObstacleType.None)
                    {
                        byte stage = ObstacleRules.GetDefaultStage(obstacleType);
                        if (levelConfig.ObstacleStages != null && idx < levelConfig.ObstacleStages.Length && levelConfig.ObstacleStages[idx] > 0)
                        {
                            stage = (byte)levelConfig.ObstacleStages[idx];
                        }
                        byte obstacleState = 0;
                        if (levelConfig.ObstacleStates != null && idx < levelConfig.ObstacleStates.Length)
                        {
                            obstacleState = (byte)levelConfig.ObstacleStates[idx];
                        }
                        if (obstacleState == 0)
                        {
                            obstacleState = ObstacleRules.GetDefaultState(obstacleType);
                        }
                        if (obstacleType == ObstacleType.PotionBottle)
                        {
                            stage = 0;
                            for (uint bits = obstacleState; bits != 0; bits &= bits - 1)
                                stage++;
                        }
                        state.SetObstacle(x, y, new Obstacle(obstacleType, stage, obstacleState));
                        continue; // Obstacle occupies cell — no tile
                    }
                }

                // 3. Tile layer
                var type = levelConfig.Grid[idx];

                if (type == ElementType.KeepEmpty)
                {
                    // Intentionally empty — skip tile but still init Ground/Cover
                }
                else
                {
                    if (type == ElementType.None)
                    {
                        var types = GetTileTypes(tileTypesCount);
                        do
                        {
                            type = types[random.Next(types.Length)];
                        } while (WouldCreateMatch(in state, x, y, type));
                    }

                    state.SetTile(x, y, new Tile(state.NextTileId++, type, x, y));
                }

                // 4. Ground layer
                if (levelConfig.Grounds != null && idx < levelConfig.Grounds.Length)
                {
                    var groundType = levelConfig.Grounds[idx];
                    if (groundType != GroundType.None)
                    {
                        byte health = GroundRules.GetDefaultHealth(groundType);
                        if (levelConfig.GroundHealths != null && idx < levelConfig.GroundHealths.Length && levelConfig.GroundHealths[idx] > 0)
                        {
                            health = (byte)levelConfig.GroundHealths[idx];
                        }
                        state.SetGround(x, y, new Ground(groundType, health));
                    }
                }

                // 5. Cover layer
                if (levelConfig.Covers != null && idx < levelConfig.Covers.Length)
                {
                    var coverType = levelConfig.Covers[idx];
                    if (coverType != CoverType.None)
                    {
                        byte health = CoverRules.GetDefaultHealth(coverType);
                        if (levelConfig.CoverHealths != null && idx < levelConfig.CoverHealths.Length && levelConfig.CoverHealths[idx] > 0)
                        {
                            health = (byte)levelConfig.CoverHealths[idx];
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
    public static int CountDistinctTileTypes(ElementType[]? grid)
    {
        if (grid == null || grid.Length == 0) return 0;

        var seen = new HashSet<ElementType>();
        foreach (var type in grid)
        {
            if (type != ElementType.None && type != ElementType.ColorBomb && type != ElementType.KeepEmpty)
            {
                seen.Add(type);
            }
        }
        return seen.Count;
    }

    /// <summary>
    /// 获取指定数量的棋子类型
    /// </summary>
    public static ElementType[] GetTileTypes(int count)
    {
        var result = new ElementType[Math.Min(count, AllTileTypes.Length)];
        Array.Copy(AllTileTypes, result, result.Length);
        return result;
    }

    /// <summary>
    /// 检查在指定位置放置指定类型是否会产生匹配
    /// </summary>
    public static bool WouldCreateMatch(in GameState state, int x, int y, ElementType type)
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
            if (state.Grid[i].Type != ElementType.None) count++;
        }
        return count;
    }
}
