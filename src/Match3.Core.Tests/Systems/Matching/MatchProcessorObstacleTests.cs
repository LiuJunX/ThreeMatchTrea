using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Obstacles;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Matching;

/// <summary>
/// Tests for StandardMatchProcessor obstacle integration:
/// adjacency notifications, dedup, and global reactions.
/// </summary>
public class MatchProcessorObstacleTests
{
    private static GameState CreateState(int width = 8, int height = 8)
    {
        return GameStateBuilder.CreateEmptyState(width, height);
    }

    private static StandardMatchProcessor CreateProcessor(IObstacleSystem? obstacleSystem = null)
    {
        var scoreSystem = new StubScoreSystem();
        var coverSystem = new CoverSystem();
        var groundSystem = new GroundSystem();
        var bombRegistry = BombEffectRegistry.CreateDefault();
        var cellEliminator = new CellEliminator(coverSystem, groundSystem, obstacleSystem: obstacleSystem);
        return new StandardMatchProcessor(scoreSystem, cellEliminator, bombRegistry, obstacleSystem);
    }

    #region Adjacent Box — damaged by any match

    [Fact]
    public void Match_AdjacentToBox_DamagesBox()
    {
        var state = CreateState();
        // 3-match at row 0: (0,0)(1,0)(2,0)
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        // Box adjacent to (1,0) at (1,1)
        state.SetObstacle(1, 1, new Obstacle(ObstacleType.Box, 4));

        var processor = CreateProcessor(new ObstacleSystem());
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            }
        };

        processor.ProcessMatches(ref state, groups);

        Assert.Equal(3, state.GetObstacle(1, 1).Stage); // 4 → 3
    }

    [Fact]
    public void Match_MultipleAdjacentTiles_BoxDamagedOnce_Dedup()
    {
        // L-shape match: (1,0)(1,1)(1,2)(0,2)
        // Box at (0,1) is adjacent to both (1,1) and (0,2) — 2 adjacencies, should dedup to 1
        var state = CreateState();
        state.SetTile(1, 0, new Tile(1, ElementType.Item3, 1, 0));
        state.SetTile(1, 1, new Tile(2, ElementType.Item3, 1, 1));
        state.SetTile(1, 2, new Tile(3, ElementType.Item3, 1, 2));
        state.SetTile(0, 2, new Tile(4, ElementType.Item3, 0, 2));
        state.SetObstacle(0, 1, new Obstacle(ObstacleType.Box, 4));

        var processor = CreateProcessor(new ObstacleSystem());
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item3,
                Positions = new HashSet<Position> { new(1, 0), new(1, 1), new(1, 2), new(0, 2) }
            }
        };

        processor.ProcessMatches(ref state, groups);

        // Dedup: Box should take only 1 damage despite 2 adjacent eliminations in same group
        Assert.Equal(3, state.GetObstacle(0, 1).Stage);
    }

    #endregion

    #region Adjacent Safe — immune to match adjacency

    [Fact]
    public void Match_AdjacentToSafe_SafeUnaffected()
    {
        var state = CreateState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        state.SetObstacle(1, 1, new Obstacle(ObstacleType.Safe, 5));

        var processor = CreateProcessor(new ObstacleSystem());
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            }
        };

        processor.ProcessMatches(ref state, groups);

        Assert.Equal(5, state.GetObstacle(1, 1).Stage); // undamaged
    }

    #endregion

    #region Adjacent ColorBox — color-specific

    [Fact]
    public void Match_AdjacentToColorBox_MatchingColor_Damaged()
    {
        var state = CreateState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        // ColorBox(Item1) at (1,1) — same color as match
        state.SetObstacle(1, 1, new Obstacle(ObstacleType.ColorBox, 1, (byte)ElementType.Item1));

        var processor = CreateProcessor(new ObstacleSystem());
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            }
        };

        processor.ProcessMatches(ref state, groups);

        Assert.False(state.HasObstacle(1, 1)); // destroyed (1 HP)
    }

    [Fact]
    public void Match_AdjacentToColorBox_WrongColor_Unaffected()
    {
        var state = CreateState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item2, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item2, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item2, 2, 0));
        // ColorBox(Item1) at (1,1) — different color from match (Item2)
        state.SetObstacle(1, 1, new Obstacle(ObstacleType.ColorBox, 1, (byte)ElementType.Item1));

        var processor = CreateProcessor(new ObstacleSystem());
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item2,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            }
        };

        processor.ProcessMatches(ref state, groups);

        Assert.Equal(1, state.GetObstacle(1, 1).Stage); // undamaged
    }

    #endregion

    #region Two independent groups — each damages obstacle

    [Fact]
    public void TwoGroups_AdjacentToSameBox_BoxTakesTwoDamage()
    {
        var state = CreateState();
        // Group 1: match at (0,0)(1,0)(2,0) — Box at (1,1) adjacent to (1,0)
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        // Group 2: match at (0,2)(1,2)(2,2) — Box at (1,1) adjacent to (1,2)
        state.SetTile(0, 2, new Tile(4, ElementType.Item3, 0, 2));
        state.SetTile(1, 2, new Tile(5, ElementType.Item3, 1, 2));
        state.SetTile(2, 2, new Tile(6, ElementType.Item3, 2, 2));
        // Box at (1,1) — adjacent to (1,0) from group 1 and (1,2) from group 2
        state.SetObstacle(1, 1, new Obstacle(ObstacleType.Box, 4));

        var processor = CreateProcessor(new ObstacleSystem());
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            },
            new MatchGroup
            {
                Type = ElementType.Item3,
                Positions = new HashSet<Position> { new(0, 2), new(1, 2), new(2, 2) }
            }
        };

        processor.ProcessMatches(ref state, groups);

        // Two independent groups = two dedup scopes = 2 hits
        Assert.Equal(2, state.GetObstacle(1, 1).Stage); // 4 → 2
    }

    #endregion

    #region Curtain global reaction

    [Fact]
    public void Match_GlobalCurtain_MatchingColor_Damaged()
    {
        var state = CreateState();
        // Match red tiles far from Curtain
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        // Curtain(Item1) at (7,7) — far away, but same color
        state.SetObstacle(7, 7, new Obstacle(ObstacleType.Curtain, 2, (byte)ElementType.Item1));

        var processor = CreateProcessor(new ObstacleSystem());
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            }
        };

        processor.ProcessMatches(ref state, groups);

        Assert.Equal(1, state.GetObstacle(7, 7).Stage); // 2 → 1
    }

    [Fact]
    public void Match_GlobalCurtain_WrongColor_Unaffected()
    {
        var state = CreateState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item2, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item2, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item2, 2, 0));
        // Curtain(Item1) — different color
        state.SetObstacle(7, 7, new Obstacle(ObstacleType.Curtain, 2, (byte)ElementType.Item1));

        var processor = CreateProcessor(new ObstacleSystem());
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item2,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            }
        };

        processor.ProcessMatches(ref state, groups);

        Assert.Equal(2, state.GetObstacle(7, 7).Stage); // undamaged
    }

    #endregion

    #region Events

    [Fact]
    public void Match_AdjacentObstacle_EmitsCorrectEvents()
    {
        var state = CreateState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        state.SetObstacle(1, 1, new Obstacle(ObstacleType.Box, 1)); // 1 HP → will be destroyed

        var processor = CreateProcessor(new ObstacleSystem());
        var events = new BufferedEventCollector();
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            }
        };

        processor.ProcessMatches(ref state, groups, 5, 1.0f, events);

        var allEvents = events.GetEvents();
        var obstacleEvent = Assert.Single(allEvents, e => e is ObstacleDestroyedEvent) as ObstacleDestroyedEvent;
        Assert.NotNull(obstacleEvent);
        Assert.Equal(new Position(1, 1), obstacleEvent!.GridPosition);
        Assert.Equal(ObstacleType.Box, obstacleEvent.Type);
    }

    #endregion

    #region No obstacle system — backward compatible

    [Fact]
    public void NoObstacleSystem_MatchWorks_NoCrash()
    {
        var state = CreateState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        state.SetObstacle(1, 1, new Obstacle(ObstacleType.Box, 4));

        // No obstacle system — should not crash, obstacles untouched
        var processor = CreateProcessor(obstacleSystem: null);
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            }
        };

        processor.ProcessMatches(ref state, groups);

        Assert.Equal(4, state.GetObstacle(1, 1).Stage); // untouched
    }

    #endregion

    #region Cover blocks adjacency trigger

    [Fact]
    public void Match_CoverAbsorbs_NoAdjacentTrigger()
    {
        var state = CreateState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        // Cover on (1,0) — tile survives, outcome is Absorbed not Eliminated
        state.SetCover(1, 0, new Cover(CoverType.Cage, 1));
        // Box at (1,1) adjacent to (1,0)
        state.SetObstacle(1, 1, new Obstacle(ObstacleType.Box, 4));

        var processor = CreateProcessor(new ObstacleSystem());
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            }
        };

        processor.ProcessMatches(ref state, groups);

        // (1,0) cover absorbed → not Eliminated → no adjacency trigger
        // (0,0) and (2,0) eliminated but not adjacent to Box at (1,1)
        Assert.Equal(4, state.GetObstacle(1, 1).Stage); // undamaged
    }

    #endregion
}
