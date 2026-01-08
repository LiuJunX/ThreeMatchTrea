using System;

namespace Match3.Random;

public interface IRandom
{
    int Next(int minInclusive, int maxExclusive);
}

public sealed class DefaultRandom : IRandom
{
    private readonly System.Random _random;
    public DefaultRandom(int? seed = null)
    {
        _random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
    }
    public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
}
