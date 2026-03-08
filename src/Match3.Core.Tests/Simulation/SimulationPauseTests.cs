using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Simulation;

public class SimulationPauseTests
{
    private GameState CreateStableState()
    {
        var state = new GameState(5, 5, 4, new StubRandom());
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                state.SetTile(x, y, new Tile(y * 5 + x, ElementType.Item3, x, y));
            }
        }
        return state;
    }

    [Fact]
    public void IsPaused_DefaultsToFalse()
    {
        var engine = TestEngineFactory.CreateEngine(CreateStableState());
        Assert.False(engine.IsPaused);
    }

    [Fact]
    public void SetPaused_UpdatesIsPaused()
    {
        var engine = TestEngineFactory.CreateEngine(CreateStableState());
        
        engine.SetPaused(true);
        Assert.True(engine.IsPaused);
        
        engine.SetPaused(false);
        Assert.False(engine.IsPaused);
    }

    [Fact]
    public void Tick_WhenPaused_DoesNotAdvanceSimulation()
    {
        var engine = TestEngineFactory.CreateEngine(CreateStableState());
        var initialTick = engine.CurrentTick;
        var initialTime = engine.ElapsedTime;

        engine.SetPaused(true);
        engine.Tick(0.1f);

        Assert.Equal(initialTick, engine.CurrentTick);
        Assert.Equal(initialTime, engine.ElapsedTime);
    }

    [Fact]
    public void Tick_WhenPaused_ReturnsZeroDeltaTime()
    {
        var engine = TestEngineFactory.CreateEngine(CreateStableState());
        
        engine.SetPaused(true);
        var result = engine.Tick(0.1f);

        Assert.Equal(0f, result.DeltaTime);
    }
}


