namespace Match3.Core.Systems.Spawning;

/// <summary>
/// Context information for spawn decision making.
/// Provides the spawn model with game state metadata for difficulty control.
/// </summary>
public struct SpawnContext
{
    /// <summary>
    /// Target difficulty level (0.0 = easy, 1.0 = hard).
    /// The spawn model should try to generate tiles that result in this difficulty.
    /// </summary>
    public float TargetDifficulty;

    /// <summary>
    /// Remaining moves in the current level.
    /// Lower values may trigger "mercy" spawns to help the player.
    /// </summary>
    public int RemainingMoves;

    /// <summary>
    /// Current goal progress (0.0 = just started, 1.0 = completed).
    /// Used to determine if player needs help or challenge.
    /// </summary>
    public float GoalProgress;

    /// <summary>
    /// Number of consecutive failed attempts on this level.
    /// Higher values may trigger easier spawns.
    /// </summary>
    public int FailedAttempts;

    /// <summary>
    /// Whether the player is in a "flow" state (good performance).
    /// </summary>
    public bool InFlowState;

    public static SpawnContext Default => new SpawnContext
    {
        TargetDifficulty = 0.5f,
        RemainingMoves = 20,
        GoalProgress = 0f,
        FailedAttempts = 0,
        InFlowState = true
    };
}
