using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Grid;
using Xunit;

namespace Match3.Core.Tests.Events;

public class EventCollectorTests
{
    #region NullEventCollector Tests

    [Fact]
    public void NullEventCollector_IsEnabled_ReturnsFalse()
    {
        var collector = NullEventCollector.Instance;

        Assert.False(collector.IsEnabled);
    }

    [Fact]
    public void NullEventCollector_Emit_DoesNotThrow()
    {
        var collector = NullEventCollector.Instance;
        var evt = new TileDestroyedEvent
        {
            Tick = 1,
            SimulationTime = 0.016f,
            TileId = 1,
            GridPosition = new Position(0, 0),
            Type = Match3.Core.Models.Enums.ElementType.Item1,
            Reason = ElimSource.Match
        };

        // Should not throw
        collector.Emit(evt);
    }

    [Fact]
    public void NullEventCollector_IsSingleton()
    {
        var a = NullEventCollector.Instance;
        var b = NullEventCollector.Instance;

        Assert.Same(a, b);
    }

    #endregion

    #region BufferedEventCollector Tests

    [Fact]
    public void BufferedEventCollector_IsEnabled_ReturnsTrue()
    {
        var collector = new BufferedEventCollector();

        Assert.True(collector.IsEnabled);
    }

    [Fact]
    public void BufferedEventCollector_Emit_CollectsEvents()
    {
        var collector = new BufferedEventCollector();
        var evt = new TileDestroyedEvent
        {
            Tick = 1,
            SimulationTime = 0.016f,
            TileId = 1,
            GridPosition = new Position(0, 0),
            Type = Match3.Core.Models.Enums.ElementType.Item1,
            Reason = ElimSource.Match
        };

        collector.Emit(evt);

        Assert.Equal(1, collector.Count);
    }

    [Fact]
    public void BufferedEventCollector_GetEvents_ReturnsAllEvents()
    {
        var collector = new BufferedEventCollector();
        var evt1 = new TileDestroyedEvent { Tick = 1, TileId = 1 };
        var evt2 = new TileDestroyedEvent { Tick = 2, TileId = 2 };

        collector.Emit(evt1);
        collector.Emit(evt2);

        var events = collector.GetEvents();
        Assert.Equal(2, events.Count);
        Assert.Equal(1, ((TileDestroyedEvent)events[0]).TileId);
        Assert.Equal(2, ((TileDestroyedEvent)events[1]).TileId);
    }

    [Fact]
    public void BufferedEventCollector_GetEvents_DoesNotClearBuffer()
    {
        var collector = new BufferedEventCollector();
        collector.Emit(new TileDestroyedEvent { Tick = 1 });

        collector.GetEvents();
        collector.GetEvents();

        Assert.Equal(1, collector.Count);
    }

    [Fact]
    public void BufferedEventCollector_DrainEvents_ClearsBuffer()
    {
        var collector = new BufferedEventCollector();
        collector.Emit(new TileDestroyedEvent { Tick = 1 });
        collector.Emit(new TileDestroyedEvent { Tick = 2 });

        var events = collector.DrainEvents();

        Assert.Equal(2, events.Count);
        Assert.Equal(0, collector.Count);
    }

    [Fact]
    public void BufferedEventCollector_Clear_RemovesAllEvents()
    {
        var collector = new BufferedEventCollector();
        collector.Emit(new TileDestroyedEvent { Tick = 1 });
        collector.Emit(new TileDestroyedEvent { Tick = 2 });

        collector.Clear();

        Assert.Equal(0, collector.Count);
    }

    [Fact]
    public void BufferedEventCollector_EmitBatch_CollectsMultipleEvents()
    {
        var collector = new BufferedEventCollector();
        var events = new GameEvent[]
        {
            new TileDestroyedEvent { Tick = 1 },
            new TileDestroyedEvent { Tick = 2 },
            new TileDestroyedEvent { Tick = 3 }
        };

        collector.EmitBatch(events);

        Assert.Equal(3, collector.Count);
    }

    [Fact]
    public void BufferedEventCollector_PreservesEventOrder()
    {
        var collector = new BufferedEventCollector();

        for (int i = 0; i < 10; i++)
        {
            collector.Emit(new TileDestroyedEvent { Tick = i });
        }

        var events = collector.GetEvents();
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(i, events[i].Tick);
        }
    }

    #endregion
}

