using System;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Utility;

namespace Match3.Core.Analysis;

/// <summary>
/// 快速移动评分器 - 使用启发式规则快速评估移动质量
/// 相比完整模拟预览，性能提升 10-100 倍
/// </summary>
public static class FastMoveScorer
{
    /// <summary>
    /// 快速评估移动分数（无需完整模拟）
    /// </summary>
    /// <param name="state">当前游戏状态</param>
    /// <param name="move">要评估的移动</param>
    /// <param name="skillLevel">技能水平 0.0-1.0，影响评分权重</param>
    /// <returns>启发式分数（越高越好）</returns>
    public static float ScoreMove(in GameState state, ValidMove move, float skillLevel = 0.7f)
    {
        float score = 0f;

        // 1. 匹配长度评估 (O(1) 局部检查)
        var (fromMatchLen, fromDirection) = CountMatchLength(in state, move.From, move.To);
        var (toMatchLen, toDirection) = CountMatchLength(in state, move.To, move.From);

        int totalMatchLen = fromMatchLen + toMatchLen;

        // 基础消除分数
        score += totalMatchLen * 15f;

        // 2. 炸弹形成检测
        var bombBonus = EvaluateBombPotential(fromMatchLen, fromDirection, toMatchLen, toDirection);
        score += bombBonus * 80f;

        // 3. 位置偏好 - 底部和中间区域优先（重力更快触发连锁）
        float positionBonus = CalculatePositionBonus(in state, move, skillLevel);
        score += positionBonus;

        // 4. 高技能玩家考虑目标进度
        if (skillLevel > 0.5f)
        {
            float objectiveBonus = EstimateObjectiveContribution(in state, move);
            score += objectiveBonus * skillLevel;
        }

        // 5. 优先选择有炸弹的格子参与移动
        var fromTile = state.GetTile(move.From);
        var toTile = state.GetTile(move.To);

        if (fromTile.Type.IsBomb())
            score += 50f; // 激活炸弹
        if (toTile.Type.IsBomb())
            score += 50f;

        return score;
    }

    /// <summary>
    /// 计算交换后的匹配长度（仅检查交换位置附近）
    /// </summary>
    private static (int length, MatchDirection direction) CountMatchLength(
        in GameState state, Position pos, Position swapWith)
    {
        // 获取交换后该位置的类型
        var type = state.GetType(swapWith);

        if (type == ElementType.None || type == ElementType.ColorBomb)
            return (0, MatchDirection.None);

        if (!state.CanMatch(pos))
            return (0, MatchDirection.None);

        int w = state.Width;
        int h = state.Height;
        int x = pos.X;
        int y = pos.Y;

        // 检查水平方向
        int hCount = 1;
        for (int i = x - 1; i >= 0; i--)
        {
            if (i == swapWith.X && y == swapWith.Y) break; // 跳过交换位置
            if (!CanMatchType(in state, i, y, type, swapWith)) break;
            hCount++;
        }
        for (int i = x + 1; i < w; i++)
        {
            if (i == swapWith.X && y == swapWith.Y) break;
            if (!CanMatchType(in state, i, y, type, swapWith)) break;
            hCount++;
        }

        // 检查垂直方向
        int vCount = 1;
        for (int i = y - 1; i >= 0; i--)
        {
            if (x == swapWith.X && i == swapWith.Y) break;
            if (!CanMatchType(in state, x, i, type, swapWith)) break;
            vCount++;
        }
        for (int i = y + 1; i < h; i++)
        {
            if (x == swapWith.X && i == swapWith.Y) break;
            if (!CanMatchType(in state, x, i, type, swapWith)) break;
            vCount++;
        }

        // 返回最长的匹配和方向
        if (hCount >= 3 && vCount >= 3)
            return (hCount + vCount - 1, MatchDirection.Both); // L/T形
        if (hCount >= 3)
            return (hCount, MatchDirection.Horizontal);
        if (vCount >= 3)
            return (vCount, MatchDirection.Vertical);

        // 检查 2x2 方块
        if (Has2x2Square(in state, x, y, type, swapWith))
            return (4, MatchDirection.Square);

        return (0, MatchDirection.None);
    }

    private static bool CanMatchType(in GameState state, int x, int y, ElementType type, Position exclude)
    {
        if (x == exclude.X && y == exclude.Y) return false;
        return state.CanMatch(x, y) && state.GetType(x, y) == type;
    }

    private static bool Has2x2Square(in GameState state, int x, int y, ElementType type, Position exclude)
    {
        int w = state.Width;
        int h = state.Height;

        // 检查4个可能的2x2方块
        // 1. (x,y) 作为左上角
        if (x + 1 < w && y + 1 < h &&
            CanMatchType(in state, x + 1, y, type, exclude) &&
            CanMatchType(in state, x, y + 1, type, exclude) &&
            CanMatchType(in state, x + 1, y + 1, type, exclude))
            return true;

        // 2. (x,y) 作为右上角
        if (x - 1 >= 0 && y + 1 < h &&
            CanMatchType(in state, x - 1, y, type, exclude) &&
            CanMatchType(in state, x - 1, y + 1, type, exclude) &&
            CanMatchType(in state, x, y + 1, type, exclude))
            return true;

        // 3. (x,y) 作为左下角
        if (x + 1 < w && y - 1 >= 0 &&
            CanMatchType(in state, x, y - 1, type, exclude) &&
            CanMatchType(in state, x + 1, y - 1, type, exclude) &&
            CanMatchType(in state, x + 1, y, type, exclude))
            return true;

        // 4. (x,y) 作为右下角
        if (x - 1 >= 0 && y - 1 >= 0 &&
            CanMatchType(in state, x - 1, y - 1, type, exclude) &&
            CanMatchType(in state, x, y - 1, type, exclude) &&
            CanMatchType(in state, x - 1, y, type, exclude))
            return true;

        return false;
    }

    /// <summary>
    /// 评估炸弹形成潜力
    /// </summary>
    private static float EvaluateBombPotential(
        int fromLen, MatchDirection fromDir,
        int toLen, MatchDirection toDir)
    {
        float bonus = 0f;

        // 5个及以上 = 彩虹/Color炸弹
        if (fromLen >= 5 || toLen >= 5)
            bonus += 3.0f;

        // L/T形 = 5x5 爆炸
        if (fromDir == MatchDirection.Both || toDir == MatchDirection.Both)
            bonus += 2.5f;

        // 4个 = 直线炸弹
        if (fromLen == 4 || toLen == 4)
            bonus += 1.5f;

        // 2x2方块 = UFO
        if (fromDir == MatchDirection.Square || toDir == MatchDirection.Square)
            bonus += 1.0f;

        return bonus;
    }

    /// <summary>
    /// 计算位置偏好分数
    /// </summary>
    private static float CalculatePositionBonus(in GameState state, ValidMove move, float skillLevel)
    {
        // 底部优先（重力会触发更多连锁）
        float bottomBonus = move.From.Y * 2f;

        // 中间水平位置略微优先
        int centerX = state.Width / 2;
        float horizontalDistance = Math.Abs(move.From.X - centerX);
        float centerBonus = (state.Width / 2f - horizontalDistance) * 0.5f;

        // 高技能玩家受位置影响较小
        float positionWeight = 1f - skillLevel * 0.5f;

        return (bottomBonus + centerBonus) * positionWeight;
    }

    /// <summary>
    /// 估算移动对目标进度的贡献
    /// </summary>
    private static float EstimateObjectiveContribution(in GameState state, ValidMove move)
    {
        float contribution = 0f;

        // 检查移动涉及的颜色是否是目标颜色
        var fromType = state.GetType(move.From);
        var toType = state.GetType(move.To);

        for (int i = 0; i < 4; i++)
        {
            var progress = state.ObjectiveProgress[i];
            if (progress.TargetCount <= 0) continue;

            // 如果目标是特定颜色的 Tile
            if (progress.TargetLayer == Models.Enums.ObjectiveTargetLayer.Tile)
            {
                var targetType = (ElementType)progress.ElementType;

                if (fromType == targetType || toType == targetType)
                {
                    // 越接近完成，奖励越高
                    float completionRatio = (float)progress.CurrentCount / progress.TargetCount;
                    contribution += 30f * (1f + completionRatio);
                }
            }

            // 如果目标是 Cover 或 Ground，检查移动位置附近是否有这些元素
            if (progress.TargetLayer == Models.Enums.ObjectiveTargetLayer.Cover)
            {
                if (HasNearbyLayer(in state, move.From, checkCover: true) ||
                    HasNearbyLayer(in state, move.To, checkCover: true))
                {
                    float completionRatio = (float)progress.CurrentCount / progress.TargetCount;
                    contribution += 25f * (1f + completionRatio);
                }
            }

            if (progress.TargetLayer == Models.Enums.ObjectiveTargetLayer.Ground)
            {
                if (HasNearbyLayer(in state, move.From, checkCover: false) ||
                    HasNearbyLayer(in state, move.To, checkCover: false))
                {
                    float completionRatio = (float)progress.CurrentCount / progress.TargetCount;
                    contribution += 25f * (1f + completionRatio);
                }
            }
        }

        return contribution;
    }

    private static bool HasNearbyLayer(in GameState state, Position pos, bool checkCover)
    {
        // 检查该位置及其4个邻居
        if (checkCover)
        {
            if (state.HasCover(pos)) return true;
        }
        else
        {
            if (state.HasGround(pos)) return true;
        }

        // 检查邻居
        int[] dx = { -1, 1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };

        for (int i = 0; i < 4; i++)
        {
            int nx = pos.X + dx[i];
            int ny = pos.Y + dy[i];

            if (!state.IsValid(nx, ny)) continue;

            if (checkCover)
            {
                if (state.HasCover(nx, ny)) return true;
            }
            else
            {
                if (state.HasGround(nx, ny)) return true;
            }
        }

        return false;
    }

    private enum MatchDirection
    {
        None,
        Horizontal,
        Vertical,
        Both,   // L/T形
        Square  // 2x2
    }
}
