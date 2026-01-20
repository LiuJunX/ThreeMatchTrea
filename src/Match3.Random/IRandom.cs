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

/// <summary>
/// XorShift64 伪随机数生成器，支持状态设置用于确定性模拟
/// </summary>
public sealed class XorShift64 : IRandom
{
    private ulong _state;

    public XorShift64(ulong seed = 12345)
    {
        _state = seed == 0 ? 1 : seed;
    }

    public float NextFloat()
    {
        return (float)(NextULong() & 0xFFFFFF) / 0x1000000;
    }

    public int Next(int max)
    {
        if (max <= 0) return 0;
        return (int)(NextULong() % (ulong)max);
    }

    public int Next(int min, int max)
    {
        if (max <= min) return min;
        return min + (int)(NextULong() % (ulong)(max - min));
    }

    private ulong NextULong()
    {
        ulong x = _state;
        x ^= x << 13;
        x ^= x >> 7;
        x ^= x << 17;
        _state = x;
        return x;
    }

    public void SetState(ulong state)
    {
        _state = state == 0 ? 1 : state;
    }
}
