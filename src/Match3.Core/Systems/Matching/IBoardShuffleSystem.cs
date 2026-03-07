using Match3.Core.Events;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Matching;

/// <summary>
/// 棋盘洗牌系统接口
/// 负责在死锁时重新洗牌棋盘以生成新的可行移动
/// </summary>
public interface IBoardShuffleSystem
{
    /// <summary>
    /// Checks if the board needs shuffling (e.g. no possible moves).
    /// </summary>
    bool NeedsShuffle(in GameState state);

    /// <summary>
    /// 洗牌棋盘上的普通色块
    /// </summary>
    /// <param name="state">游戏状态</param>
    /// <param name="events">事件收集器</param>
    void Shuffle(ref GameState state, IEventCollector events);

    /// <summary>
    /// 持续洗牌直到棋盘有至少一个可行移动
    /// </summary>
    /// <param name="state">游戏状态</param>
    /// <param name="events">事件收集器</param>
    /// <param name="maxAttempts">最大尝试次数</param>
    void ShuffleUntilSolvable(ref GameState state, IEventCollector events, int maxAttempts = 10);
}
