using System.Collections.Generic;
using System.Linq;
using Match3.Core.Choreography;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Xunit;

namespace Match3.Core.Tests.Choreography;

public class ChoreographerGoalTests
{
    private readonly Match3.Core.Choreography.Choreographer _choreographer;

    public ChoreographerGoalTests()
    {
        _choreographer = new Match3.Core.Choreography.Choreographer();
        _choreographer.Config = new ChoreographyConfig(); // Use defaults
    }

    [Fact]
    public void Visit_TileDestroyedEvent_WithIsGoal_DoesNotEmitShowEffectCommand()
    {
        // Arrange
        var evt = new TileDestroyedEvent
        {
            Tick = 1,
            SimulationTime = 0.1f,
            TileId = 1,
            GridPosition = new Position(0, 0),
            Type = ElementType.Item1,
            Reason = DestroyReason.Match,
            IsGoal = true
        };

        // Act
        var commands = _choreographer.Choreograph(new[] { evt });

        // Assert
        // Goal tile: no destroy animation, no effects — only RemoveTileCommand
        Assert.DoesNotContain(commands, c => c is DestroyTileCommand);
        Assert.Contains(commands, c => c is RemoveTileCommand);
        Assert.DoesNotContain(commands, c => c is ShowEffectCommand);
    }

    [Fact]
    public void Visit_TileDestroyedEvent_WithoutIsGoal_EmitsShowEffectCommand()
    {
        // Arrange
        var evt = new TileDestroyedEvent
        {
            Tick = 1,
            SimulationTime = 0.1f,
            TileId = 1,
            GridPosition = new Position(0, 0),
            Type = ElementType.Item1,
            Reason = DestroyReason.Match,
            IsGoal = false
        };

        // Act
        var commands = _choreographer.Choreograph(new[] { evt });

        // Assert
        Assert.Contains(commands, c => c is ShowEffectCommand);
    }

    [Fact]
    public void Visit_CoverDestroyedEvent_WithIsGoal_DoesNotEmitShowEffectCommand()
    {
        // Arrange
        var evt = new CoverDestroyedEvent
        {
            Tick = 1,
            SimulationTime = 0.1f,
            GridPosition = new Position(0, 0),
            Type = CoverType.Cage,
            IsGoal = true
        };

        // Act
        var commands = _choreographer.Choreograph(new[] { evt });

        // Assert
        Assert.Contains(commands, c => c is DestroyCoverCommand);
        Assert.DoesNotContain(commands, c => c is ShowEffectCommand);
    }

    [Fact]
    public void Visit_GroundDestroyedEvent_WithIsGoal_DoesNotEmitShowEffectCommand()
    {
        // Arrange
        var evt = new GroundDestroyedEvent
        {
            Tick = 1,
            SimulationTime = 0.1f,
            GridPosition = new Position(0, 0),
            Type = GroundType.Ice,
            IsGoal = true
        };

        // Act
        var commands = _choreographer.Choreograph(new[] { evt });

        // Assert
        Assert.Contains(commands, c => c is DestroyGroundCommand);
        Assert.DoesNotContain(commands, c => c is ShowEffectCommand);
    }
}
