using System;

namespace Match3.Random;

/// <summary>
/// Fail-fast sentinel: any call to Next() throws immediately.
/// Assigned by GameState.Clone() so that callers who forget to set Random
/// get a clear error instead of silently sharing the original RNG.
/// </summary>
public sealed class NullRandom : IRandom
{
    public static readonly NullRandom Instance = new();

    public int Next(int minInclusive, int maxExclusive) =>
        throw new InvalidOperationException(
            "GameState.Random not initialized. Use Clone(IRandom) or assign Random after Clone().");
}
