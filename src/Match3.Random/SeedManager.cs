using System.Collections.Generic;

namespace Match3.Random;

public sealed class SeedManager
{
    private readonly int? _masterSeed;
    private readonly Dictionary<RandomDomain, IRandom> _streams = new();
    private readonly Dictionary<RandomDomain, int> _overrides = new();

    public SeedManager(int? masterSeed)
    {
        _masterSeed = masterSeed;
    }

    public IRandom GetRandom(RandomDomain domain)
    {
        if (_streams.TryGetValue(domain, out var rng))
            return rng;

        if (_overrides.TryGetValue(domain, out var seed))
        {
            rng = new XorShift64((ulong)seed);
        }
        else
        {
            rng = RandomStreamFactory.Create(_masterSeed, domain);
        }
        _streams[domain] = rng;
        return rng;
    }

    /// <summary>
    /// 强制为指定的随机域设置一个固定的种子值，后续对该域的随机数请求都会使用这个种子重新初始化随机流。
    /// 如果该域之前已创建过随机流，则会被移除并在下次访问时重新生成。
    /// </summary>
    /// <param name="domain">要覆盖的随机域</param>
    /// <param name="seed">固定的种子值</param>
    public void SetOverride(RandomDomain domain, int seed)
    {
        _overrides[domain] = seed;
        _streams.Remove(domain);
    }

    public int Next(RandomDomain domain, int minInclusive, int maxExclusive)
    {
        return GetRandom(domain).Next(minInclusive, maxExclusive);
    }
}
