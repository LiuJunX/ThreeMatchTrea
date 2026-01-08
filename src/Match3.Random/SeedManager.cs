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
            rng = new DefaultRandom(seed);
        }
        else
        {
            rng = RandomStreamFactory.Create(_masterSeed, domain);
        }
        _streams[domain] = rng;
        return rng;
    }

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
