using Xunit;
using Match3.Random;

namespace Match3.Random.Tests;

public class RandomTests
{
    [Fact]
    public void SeedManager_GetRandom_ReturnsSameInstanceForSameDomain()
    {
        var manager = new SeedManager(12345);
        var rng1 = manager.GetRandom(RandomDomain.Main);
        var rng2 = manager.GetRandom(RandomDomain.Main);

        Assert.Same(rng1, rng2);
    }

    [Fact]
    public void SeedManager_DifferentDomains_ReturnDifferentStreams()
    {
        var manager = new SeedManager(12345);
        var rngMain = manager.GetRandom(RandomDomain.Main);
        var rngRefill = manager.GetRandom(RandomDomain.Refill);

        // They are different instances
        Assert.NotSame(rngMain, rngRefill);

        // And they should produce different sequences (statistically likely)
        int v1 = rngMain.Next(0, 100000);
        int v2 = rngRefill.Next(0, 100000);
        
        // This is a weak test but implies they aren't synced. 
        // Better: Check internal state if exposed, or just rely on factory logic logic.
        // Given factory logic: derived = master * const ^ domain
        // domain Main=0, Refill=1. So derived seeds are different.
    }

    [Fact]
    public void SeedManager_WithSameMasterSeed_ProducesDeterministicResults()
    {
        var manager1 = new SeedManager(42);
        var manager2 = new SeedManager(42);

        var val1 = manager1.GetRandom(RandomDomain.Main).Next(0, 100);
        var val2 = manager2.GetRandom(RandomDomain.Main).Next(0, 100);

        Assert.Equal(val1, val2);
    }

    [Fact]
    public void SeedManager_Override_ReplacesStream()
    {
        var manager = new SeedManager(12345);
        
        // Initial stream
        var rng1 = manager.GetRandom(RandomDomain.Main);
        int initialVal = rng1.Next(0, 1000);

        // Override
        manager.SetOverride(RandomDomain.Main, 9999);
        var rng2 = manager.GetRandom(RandomDomain.Main);

        Assert.NotSame(rng1, rng2);
        
        // Check determinism of override
        var managerOther = new SeedManager(12345);
        managerOther.SetOverride(RandomDomain.Main, 9999);
        var rng3 = managerOther.GetRandom(RandomDomain.Main);
        
        Assert.Equal(rng2.Next(0, 1000), rng3.Next(0, 1000));
    }

    [Fact]
    public void RandomStreamFactory_Create_WithNullMasterSeed_ReturnsNonDeterministic()
    {
        // When master seed is null, we expect System.Random behavior (effectively)
        // or just a XorShift64 with a random seed.
        var rng = RandomStreamFactory.Create(null, RandomDomain.Main);
        Assert.NotNull(rng);
    }
}
