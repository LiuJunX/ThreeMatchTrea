using System;
using System.Collections.Generic;

namespace Match3.Core.Progress;

/// <summary>
/// Tracks per-level best stars and unlock state.
/// Pure C# model — no Unity dependencies.
/// </summary>
[Serializable]
public sealed class PlayerProgress
{
    public Dictionary<string, int> BestStars { get; set; } = new();
    public HashSet<string> UnlockedLevels { get; set; } = new();

    public bool IsLevelUnlocked(string levelId) => UnlockedLevels.Contains(levelId);

    public int GetBestStars(string levelId) =>
        BestStars.TryGetValue(levelId, out var s) ? s : 0;

    /// <summary>
    /// Update best stars if the new value is higher.
    /// </summary>
    public void SetBestStars(string levelId, int stars)
    {
        if (stars > GetBestStars(levelId))
            BestStars[levelId] = stars;
    }

    /// <summary>
    /// Stars from remaining moves: >50% → 3, >20% → 2, else 1.
    /// </summary>
    public static int CalculateStars(int movesRemaining, int moveLimit)
    {
        if (moveLimit <= 0) return 1;
        float ratio = (float)movesRemaining / moveLimit;
        return ratio > 0.5f ? 3 : ratio > 0.2f ? 2 : 1;
    }
}
