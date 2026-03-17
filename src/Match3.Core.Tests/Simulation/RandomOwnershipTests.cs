using System;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Tests.TestFixtures;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Simulation;

/// <summary>
/// Safety-net tests for GameState.Clone() Random ownership.
/// Ensures Clone() returns NullRandom (fail-fast) and Clone(IRandom) assigns correctly.
/// </summary>
public class RandomOwnershipTests
{
    [Fact]
    public void Clone_WithoutRandom_ReturnsNullRandom()
    {
        var state = new GameState(5, 5, 4, new StubRandom());
        var clone = state.Clone();

        Assert.IsType<NullRandom>(clone.Random);
    }

    [Fact]
    public void Clone_WithoutRandom_ThrowsOnNextCall()
    {
        var state = new GameState(5, 5, 4, new StubRandom());
        var clone = state.Clone();

        var ex = Assert.Throws<InvalidOperationException>(() => clone.Random.Next(0, 10));
        Assert.Contains("not initialized", ex.Message);
    }

    [Fact]
    public void Clone_WithRandom_AssignsCorrectly()
    {
        var state = new GameState(5, 5, 4, new StubRandom());
        var rng = new XorShift64(42);

        var clone = state.Clone(rng);

        Assert.Same(rng, clone.Random);
    }

    [Fact]
    public void Clone_ArraysAreIndependent()
    {
        var rng = new StubRandom();
        var state = new GameState(5, 5, 4, rng);
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));

        var clone = state.Clone(new XorShift64(42));

        // Mutate clone's grid
        clone.SetTile(0, 0, new Tile(99, ElementType.Item3, 0, 0));

        // Original should be unaffected
        Assert.Equal(1, state.GetTile(0, 0).Id);
        Assert.Equal(ElementType.Item1, state.GetTile(0, 0).Type);
    }

    [Fact]
    public void SimulationEngine_Clone_RequiresRandom()
    {
        var state = TestEngineFactory.CreateTestState();
        var engine = TestEngineFactory.CreateEngine(state);

        var clone = engine.Clone(new XorShift64(42));

        Assert.NotNull(clone);
        Assert.True(clone.IsStable());
    }
}
