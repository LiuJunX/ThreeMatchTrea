using System.Collections.Generic;
using Match3.Core.Utility.Pools;
using Xunit;

namespace Match3.Tests;

public class BucketedListPoolTests
{
    [Fact]
    public void Get_NoArgs_ReturnsSmallList()
    {
        var list = Pools.ObtainList<int>();
        Assert.NotNull(list);
        Assert.True(list.Capacity < 128); // Usually 0 or 4
        Pools.Release(list);
    }

    [Fact]
    public void Get_WithCapacity_ReturnsListWithAtLeastThatCapacity()
    {
        var list = Pools.ObtainList<int>(100);
        Assert.True(list.Capacity >= 100);
        Pools.Release(list);
    }

    [Fact]
    public void Return_SeparatesSmallAndLargeLists()
    {
        // 1. Get a small list and return it
        var smallList = Pools.ObtainList<int>(10);
        Pools.Release(smallList);

        // 2. Get a large list and return it
        var largeList = Pools.ObtainList<int>(500); // > 128
        Pools.Release(largeList);

        // 3. Request small list - should get the small one
        var reusedSmall = Pools.ObtainList<int>(10);
        Assert.Same(smallList, reusedSmall);
        Assert.NotSame(largeList, reusedSmall);

        // 4. Request large list - should get the large one
        var reusedLarge = Pools.ObtainList<int>(500);
        Assert.Same(largeList, reusedLarge);
        Assert.NotSame(smallList, reusedLarge);
    }

    [Fact]
    public void Return_DropsHugeLists()
    {
        // 1. Create a huge list
        var hugeList = Pools.ObtainList<int>();
        hugeList.Capacity = 5000; // > 2048 (MaxRetainCapacity)
        
        // 2. Return it
        Pools.Release(hugeList);

        // 3. Requesting a large list should NOT return the huge one (it was dropped)
        // Note: We need to ensure the pool doesn't have other large lists.
        // Since tests run in parallel or share static state, this might be flaky if we rely on global Pools.
        // But for this specific instance check, it should be fine if we assume a clean state or just check equality.
        
        var newList = Pools.ObtainList<int>(1000);
        Assert.NotSame(hugeList, newList);
    }

    [Fact]
    public void Disposable_Pattern_Works()
    {
        List<int> refToList;
        using (var handle = Pools.ObtainDisposableList<int>(out var list))
        {
            refToList = list;
            list.Add(1);
            Assert.Single(list);
        }

        // After using block, list should be returned to pool and cleared.
        // We can check if getting a new list returns the same object.
        var newList = Pools.ObtainList<int>();
        Assert.Same(refToList, newList);
        Assert.Empty(newList);
    }

    [Fact]
    public void Mixed_Usage_Scenario()
    {
        // Simulate game loop
        // Frame 1: Need small list
        var l1 = Pools.ObtainList<int>();
        Pools.Release(l1);

        // Frame 2: Need large list (pathfinding)
        var l2 = Pools.ObtainList<int>(1000);
        Pools.Release(l2);

        // Frame 3: Need small list again
        var l3 = Pools.ObtainList<int>();
        
        // Should reuse l1, NOT l2
        Assert.Same(l1, l3);
        Assert.NotSame(l2, l3);
    }
}
