using System;

namespace Match3.Random;

/// <summary>
/// Pseudo-Random Distribution calculator.
/// Ensures more consistent triggering than true random by escalating probability on each miss.
/// Formula: P(N) = C × N, where N is consecutive miss count.
/// Reference: Dota 2 crit/evasion system.
/// </summary>
public struct PrdCalculator
{
    /// <summary>C factor that determines the slope of probability increase.</summary>
    public float C;

    /// <summary>Number of consecutive misses since last trigger.</summary>
    public int MissCount;

    /// <summary>
    /// Lookup table: CTable[i] = correct C value for target probability i/TableSize.
    /// Built once at static initialization via binary search solver.
    /// </summary>
    private const int TableSize = 100;
    private static readonly float[] CTable = BuildTable();

    /// <summary>
    /// Creates a PRD calculator with the given C factor directly.
    /// </summary>
    public PrdCalculator(float c)
    {
        C = c;
        MissCount = 0;
    }

    /// <summary>
    /// Recommended minimum probability for accurate C derivation.
    /// Below this threshold, the 1% table granularity causes significant interpolation error.
    /// </summary>
    public const float MinRecommendedProbability = 0.01f;

    /// <summary>
    /// Creates a PRD calculator from a desired average probability.
    /// Uses a precomputed lookup table (1% granularity) for precise C value derivation.
    /// </summary>
    /// <param name="targetProbability">Desired average trigger rate (0.0-1.0).
    /// Values below <see cref="MinRecommendedProbability"/> lose precision due to table granularity.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when targetProbability is outside [0, 1].</exception>
    public static PrdCalculator FromProbability(float targetProbability)
    {
        if (targetProbability < 0f || targetProbability > 1f)
            throw new ArgumentOutOfRangeException(nameof(targetProbability), targetProbability,
                "Must be between 0.0 and 1.0.");

        float index = targetProbability * TableSize;
        int lo = (int)index;
        int hi = Math.Min(lo + 1, TableSize);
        float t = index - lo;
        float c = CTable[lo] * (1f - t) + CTable[hi] * t;
        return new PrdCalculator { C = c, MissCount = 0 };
    }

    /// <summary>
    /// Executes a PRD roll. Returns true on hit (resets counter), false on miss (increments counter).
    /// </summary>
    /// <param name="roll">Random value in [0, scale).</param>
    /// <param name="scale">Upper bound of the random range (default 10000).</param>
    /// <param name="cMultiplier">Multiplier applied to C (e.g., reduction after objective met).</param>
    public bool Roll(int roll, int scale = 10000, float cMultiplier = 1f)
    {
        float effectiveC = C * cMultiplier;
        int threshold = (int)(effectiveC * (MissCount + 1) * scale);
        if (threshold > scale) threshold = scale;

        if (roll < threshold)
        {
            MissCount = 0;
            return true;
        }

        MissCount++;
        return false;
    }

    /// <summary>
    /// Returns the precomputed C value at the given table index (0-100).
    /// Exposed for testing table integrity.
    /// </summary>
    internal static float GetTableValue(int index) => CTable[index];

    /// <summary>
    /// Returns the table size (number of entries minus 1, i.e., percentage granularity).
    /// </summary>
    internal static int GetTableSize() => TableSize;

    #region Table Builder (static initialization)

    private static float[] BuildTable()
    {
        var table = new float[TableSize + 1];
        table[0] = 0f;
        for (int i = 1; i <= TableSize; i++)
        {
            float targetP = i / (float)TableSize;
            table[i] = SolveC(targetP);
        }
        return table;
    }

    /// <summary>
    /// Binary search for the C value that produces the desired average probability.
    /// </summary>
    private static float SolveC(float targetP)
    {
        if (targetP >= 1f) return 1f;

        float lo = 0f, hi = targetP;
        for (int iter = 0; iter < 30; iter++)
        {
            float mid = (lo + hi) * 0.5f;
            if (ComputeAverageProbability(mid) > targetP)
                hi = mid;
            else
                lo = mid;
        }
        return (lo + hi) * 0.5f;
    }

    /// <summary>
    /// Computes the actual average trigger probability for a given C value.
    /// p = 1 / E[N], where E[N] = sum of N × P(exactly hit at N).
    /// </summary>
    private static float ComputeAverageProbability(float c)
    {
        if (c <= 0f) return 0f;
        if (c >= 1f) return 1f;

        int maxN = (int)Math.Ceiling(1.0 / c);
        float expectedN = 0f;
        float survival = 1f;

        for (int n = 1; n <= maxN; n++)
        {
            float hitProb = Math.Min(c * n, 1f);
            float pExactlyN = survival * hitProb;
            expectedN += n * pExactlyN;
            survival *= (1f - hitProb);
        }

        // Remaining survival probability (floating point residual)
        if (survival > 1e-6f)
            expectedN += (maxN + 1) * survival;

        return expectedN > 0f ? 1f / expectedN : 0f;
    }

    #endregion
}
