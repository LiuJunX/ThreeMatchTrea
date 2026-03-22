using System.Linq;
using Match3.Core.Choreography;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Xunit;

namespace Match3.Core.Tests.Choreography;

/// <summary>
/// Tests for Bush-specific choreography: damage/destroy durations,
/// and Bush destroy → Grass spawn command timing sequence.
/// </summary>
public class BushChoreographerTests
{
    private readonly Choreographer _choreographer = new();
    private static readonly Position BushPos = new(2, 2);

    #region Bush-specific animation durations

    [Fact]
    public void Bush_Damage_Duration_Is035()
    {
        var events = new GameEvent[]
        {
            new ObstacleDamagedEvent
            {
                GridPosition = BushPos,
                Type = ObstacleType.Bush,
                RemainingStage = 4,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var damageCmd = commands.OfType<DamageObstacleCommand>().Single();
        Assert.Equal(ObstacleType.Bush, damageCmd.ObstacleType);
        Assert.Equal(0.35f, damageCmd.Duration, 0.001f);
    }

    [Fact]
    public void Bush_Destroy_Duration_Is04()
    {
        var events = new GameEvent[]
        {
            new ObstacleDestroyedEvent
            {
                GridPosition = BushPos,
                Type = ObstacleType.Bush,
                SimulationTime = 0f
            }
        };

        var commands = _choreographer.Choreograph(events);

        var destroyCmd = commands.OfType<DestroyObstacleCommand>().Single();
        Assert.Equal(ObstacleType.Bush, destroyCmd.ObstacleType);
        Assert.Equal(0.4f, destroyCmd.Duration, 0.001f);
    }

    #endregion

    #region Bush destroy → Grass spawn timing sequence

    [Fact]
    public void Bush_DestroyThenGrassSpawn_GrassStartsAfterOrDuringDestroy()
    {
        // Simulate the event sequence produced by a Bush death:
        // ObstacleDestroyedEvent followed by GroundSpawnedEvents
        var events = new GameEvent[]
        {
            new ObstacleDestroyedEvent
            {
                GridPosition = BushPos,
                Type = ObstacleType.Bush,
                SimulationTime = 1.0f
            },
            new GroundSpawnedEvent
            {
                GridPosition = new Position(1, 2),
                Type = GroundType.Grass,
                SourcePosition = BushPos,
                SimulationTime = 1.0f // Same tick as destroy
            },
            new GroundSpawnedEvent
            {
                GridPosition = new Position(3, 2),
                Type = GroundType.Grass,
                SourcePosition = BushPos,
                SimulationTime = 1.0f
            },
            new GroundSpawnedEvent
            {
                GridPosition = new Position(2, 1),
                Type = GroundType.Grass,
                SourcePosition = BushPos,
                SimulationTime = 1.0f
            },
            new GroundSpawnedEvent
            {
                GridPosition = new Position(2, 3),
                Type = GroundType.Grass,
                SourcePosition = BushPos,
                SimulationTime = 1.0f
            },
        };

        var commands = _choreographer.Choreograph(events);

        var destroyCmd = commands.OfType<DestroyObstacleCommand>().Single();
        var grassCmds = commands.OfType<SpawnGroundCommand>().ToList();

        // 4 Grass spawn commands
        Assert.Equal(4, grassCmds.Count);

        // All Grass spawns start at the same time or after the Bush destroy starts
        // (same SimulationTime means same choreography start time)
        foreach (var grassCmd in grassCmds)
        {
            Assert.True(grassCmd.StartTime >= destroyCmd.StartTime,
                $"Grass spawn at ({grassCmd.GridPos.X},{grassCmd.GridPos.Y}) " +
                $"starts at {grassCmd.StartTime} before Bush destroy at {destroyCmd.StartTime}");
        }
    }

    [Fact]
    public void Bush_MultiDamage_ThenDestroy_CommandSequenceCorrect()
    {
        // Simulate 2 damage events + 1 destroy + grass spawns
        var events = new GameEvent[]
        {
            new ObstacleDamagedEvent
            {
                GridPosition = BushPos, Type = ObstacleType.Bush,
                RemainingStage = 2, SimulationTime = 0.0f
            },
            new ObstacleDamagedEvent
            {
                GridPosition = BushPos, Type = ObstacleType.Bush,
                RemainingStage = 1, SimulationTime = 0.1f
            },
            new ObstacleDestroyedEvent
            {
                GridPosition = BushPos, Type = ObstacleType.Bush,
                SimulationTime = 0.2f
            },
            new GroundSpawnedEvent
            {
                GridPosition = new Position(1, 2), Type = GroundType.Grass,
                SourcePosition = BushPos, SimulationTime = 0.2f
            },
        };

        var commands = _choreographer.Choreograph(events);

        var damageCmds = commands.OfType<DamageObstacleCommand>().ToList();
        var destroyCmd = commands.OfType<DestroyObstacleCommand>().Single();
        var grassCmd = commands.OfType<SpawnGroundCommand>().Single();
        var removeCmd = commands.OfType<RemoveObstacleCommand>().Single();

        // 2 damage commands
        Assert.Equal(2, damageCmds.Count);

        // Damage → Destroy → Remove ordering by start time
        Assert.True(damageCmds[0].StartTime < damageCmds[1].StartTime);
        Assert.True(damageCmds[1].StartTime < destroyCmd.StartTime);
        Assert.True(destroyCmd.StartTime < removeCmd.StartTime);

        // Remove scheduled after destroy animation ends
        Assert.Equal(destroyCmd.StartTime + destroyCmd.Duration, removeCmd.StartTime, 0.001f);

        // Grass spawn concurrent with destroy (same SimulationTime)
        Assert.Equal(destroyCmd.StartTime, grassCmd.StartTime, 0.001f);
    }

    #endregion
}
