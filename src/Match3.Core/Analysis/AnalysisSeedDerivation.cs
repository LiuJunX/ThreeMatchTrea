namespace Match3.Core.Analysis;

/// <summary>
/// 分析服务的种子派生公式集中管理
/// </summary>
internal static class AnalysisSeedDerivation
{
    /// <summary>
    /// 从 simulation index 派生种子（LevelAnalysis / StrategyDriven / MCTS 通用）
    /// </summary>
    public static ulong FromSimulationIndex(int index)
        => (ulong)(index * 7919 + 12345);

    /// <summary>
    /// 从 tier/player/game 三维索引派生种子（DeepAnalysis 专用）
    /// </summary>
    public static ulong FromPlayerGame(int tierIndex, int playerIndex, int gameIndex)
        => (ulong)(tierIndex + 1) * 1000000UL + (ulong)playerIndex * 1000UL + (ulong)gameIndex;
}
