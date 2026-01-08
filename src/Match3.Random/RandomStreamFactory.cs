namespace Match3.Random;

public static class RandomStreamFactory
{
    public static IRandom Create(int? masterSeed, RandomDomain domain)
    {
        if (!masterSeed.HasValue) return new DefaultRandom(null);
        int tagHash = (int)domain;
        int derived = unchecked(masterSeed.Value * 16777619 ^ tagHash);
        if (derived == 0) derived = 1;
        return new DefaultRandom(derived);
    }

    public static IRandom Create(int? masterSeed, string tag)
    {
        if (!masterSeed.HasValue) return new DefaultRandom(null);
        int tagHash = StableHash(tag);
        int derived = unchecked(masterSeed.Value * 16777619 ^ tagHash);
        if (derived == 0) derived = 1;
        return new DefaultRandom(derived);
    }

    private static int StableHash(string s)
    {
        unchecked
        {
            const int p = 16777619;
            int hash = (int)2166136261;
            foreach (var ch in s)
            {
                hash ^= ch;
                hash *= p;
            }
            return hash;
        }
    }
}
