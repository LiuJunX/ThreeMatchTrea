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
/// Tests for StandardMatchProcessor cover adjacency integration:
/// verifies NotifyBatchElimination is called through the full match pipeline.
/// Parallel to <see cref="MatchProcessorObstacleTests"/> for obstacles.
/// </summary>
public class MatchProcessorCoverAdjacencyTests
{
    private static GameState CreateState(int width = 8, int height = 8)
    {
        return GameStateBuilder.CreateEmptyState(width, height);
    }

    private static StandardMatchProcessor CreateProcessor(
        ICoverSystem? coverSystem = null,
        IObstacleSystem? obstacleSystem = null)
    {
        var scoreSystem = new StubScoreSystem();
        var cover = coverSystem ?? new CoverSystem();
        var groundSystem = new GroundSystem();
        var bombRegistry = BombEffectRegistry.CreateDefault();
        var cellEliminator = new CellEliminator(cover, groundSystem, obstacleSystem: obstacleSystem);
        return new StandardMatchProcessor(scoreSystem, cellEliminator, bombRegistry, obstacleSystem, cover);
    }

    #region Adjacent Honey — damaged by match

    [Fact]
    public void Match_AdjacentToHoney_DestroysHoney()
    {
        // Board:  [R][R][R]
        //            [H]       ← Honey at (1,1), adjacent to (1,0)
        var state = CreateState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        state.SetTile(1, 1, new Tile(4, ElementType.Item2, 1, 1)); // tile under Honey
        state.SetCover(new Position(1, 1), new Cover(CoverType.Honey, health: 1));

        var processor = CreateProcessor();
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

        // Honey destroyed by adjacency
        Assert.Equal(CoverType.None, state.GetCover(new Position(1, 1)).Type);
        // Tile under Honey survives (Honey was not directly hit, only adjacency)
        Assert.Equal(ElementType.Item2, state.GetTile(1, 1).Type);
        // Event emitted
        Assert.Contains(events.GetEvents(), e =>
            e is CoverDestroyedEvent cd && cd.Type == CoverType.Honey && cd.GridPosition == new Position(1, 1));
    }

    #endregion

    #region Dedup — same group, multiple adjacencies

    [Fact]
    public void Match_MultipleAdjacentTiles_HoneyDamagedOnce_Dedup()
    {
        // L-shape match: (1,0)(1,1)(1,2)(0,2)
        // Honey at (0,1) adjacent to both (1,1) and (0,2)
        var state = CreateState();
        state.SetTile(1, 0, new Tile(1, ElementType.Item3, 1, 0));
        state.SetTile(1, 1, new Tile(2, ElementType.Item3, 1, 1));
        state.SetTile(1, 2, new Tile(3, ElementType.Item3, 1, 2));
        state.SetTile(0, 2, new Tile(4, ElementType.Item3, 0, 2));
        state.SetTile(0, 1, new Tile(5, ElementType.Item2, 0, 1)); // tile under Honey
        state.SetCover(new Position(0, 1), new Cover(CoverType.Honey, health: 2));

        var processor = CreateProcessor();
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item3,
                Positions = new HashSet<Position> { new(1, 0), new(1, 1), new(1, 2), new(0, 2) }
            }
        };

        processor.ProcessMatches(ref state, groups);

        // Dedup: Honey damaged only once despite 2 adjacent eliminations
        Assert.Equal(CoverType.Honey, state.GetCover(new Position(0, 1)).Type);
        Assert.Equal(1, state.GetCover(new Position(0, 1)).Health);
    }

    #endregion

    #region Two groups — separate dedup scopes

    [Fact]
    public void TwoGroups_AdjacentToSameHoney_HoneyTakesTwoDamage()
    {
        // Group 1: match at (0,0)(1,0)(2,0) — Honey at (1,1) adjacent to (1,0)
        // Group 2: match at (0,2)(1,2)(2,2) — Honey at (1,1) adjacent to (1,2)
        var state = CreateState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        state.SetTile(0, 2, new Tile(4, ElementType.Item3, 0, 2));
        state.SetTile(1, 2, new Tile(5, ElementType.Item3, 1, 2));
        state.SetTile(2, 2, new Tile(6, ElementType.Item3, 2, 2));
        state.SetTile(1, 1, new Tile(7, ElementType.Item2, 1, 1)); // tile under Honey
        state.SetCover(new Position(1, 1), new Cover(CoverType.Honey, health: 2));

        var processor = CreateProcessor();
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

        // Two groups = two dedup scopes = 2 hits → Honey destroyed
        Assert.Equal(CoverType.None, state.GetCover(new Position(1, 1)).Type);
    }

    #endregion

    #region Honey on match position — direct hit absorbs, tile survives

    [Fact]
    public void Match_TileUnderHoney_CoverAbsorbsDirectHit_TileSurvives()
    {
        // Board: [R][R+H][R]  ← Honey covers (1,0), tile is Item1
        // Match includes (1,0) but cover guard chain absorbs the hit
        var state = CreateState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        state.SetCover(new Position(1, 0), new Cover(CoverType.Honey, health: 1));

        var processor = CreateProcessor();
        var events = new BufferedEventCollector();
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            }
        };

        processor.ProcessMatches(ref state, groups, 1, 0f, events);

        // Cover destroyed by direct hit (CellEliminator guard chain)
        Assert.Equal(CoverType.None, state.GetCover(new Position(1, 0)).Type);
        // Tile under cover survives (Absorbed outcome, not Eliminated)
        Assert.Equal(ElementType.Item1, state.GetTile(1, 0).Type);
        // Other tiles eliminated
        Assert.Equal(ElementType.None, state.GetTile(0, 0).Type);
        Assert.Equal(ElementType.None, state.GetTile(2, 0).Type);
    }

    [Fact]
    public void Match_HoneyAbsorbedTile_DoesNotTriggerAdjacentHoney()
    {
        // Board: [R][R+H][R]
        //            [H]       ← Second Honey at (1,1)
        // (1,0) has Honey → absorbed (not Eliminated) → should NOT trigger adjacency for (1,1)
        // But (0,0) and (2,0) are eliminated, neither is adjacent to (1,1)
        var state = CreateState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        state.SetTile(1, 1, new Tile(4, ElementType.Item2, 1, 1));
        state.SetCover(new Position(1, 0), new Cover(CoverType.Honey, health: 1));
        state.SetCover(new Position(1, 1), new Cover(CoverType.Honey, health: 1));

        var processor = CreateProcessor();
        processor.ProcessMatches(ref state, new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            }
        });

        // (1,0) Honey destroyed by direct hit
        Assert.Equal(CoverType.None, state.GetCover(new Position(1, 0)).Type);
        // (1,1) Honey unchanged — absorbed tiles don't enter groupEliminated → no adjacency
        Assert.Equal(CoverType.Honey, state.GetCover(new Position(1, 1)).Type);
    }

    #endregion

    #region Honey + Obstacle both adjacent

    [Fact]
    public void Match_HoneyAndObstacleBothAdjacent_BothDamaged()
    {
        // Board: [R][R][R]
        //         [H]  [B]    ← Honey at (0,1), Box at (2,1)
        var state = CreateState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        state.SetTile(0, 1, new Tile(4, ElementType.Item2, 0, 1)); // tile under Honey
        state.SetCover(new Position(0, 1), new Cover(CoverType.Honey, health: 1));
        state.SetObstacle(2, 1, new Obstacle(ObstacleType.Box, 4));

        var processor = CreateProcessor(obstacleSystem: new ObstacleSystem());
        processor.ProcessMatches(ref state, new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            }
        });

        // Both damaged
        Assert.Equal(CoverType.None, state.GetCover(new Position(0, 1)).Type);
        Assert.Equal(3, state.GetObstacle(2, 1).Stage); // 4 → 3
    }

    #endregion

    #region Multiple Honeys adjacent to same eliminated tile

    [Fact]
    public void Match_MultipleHoneysAdjacentToSameElimination_AllDamaged()
    {
        // Board:      [H]       ← Honey at (1,0)
        //         [H][R][H]    ← Honeys at (0,1) and (2,1), match tile at (1,1)
        //              [H]      ← Honey at (1,2)
        // Match: vertical (1,1)(1,2)(1,3) — but we'll use simple group
        // Actually let's do horizontal match at row 1
        var state = CreateState(5, 5);
        // Match 3 at row 2: (0,2)(1,2)(2,2)
        state.SetTile(0, 2, new Tile(1, ElementType.Item1, 0, 2));
        state.SetTile(1, 2, new Tile(2, ElementType.Item1, 1, 2));
        state.SetTile(2, 2, new Tile(3, ElementType.Item1, 2, 2));
        // Honeys around (1,2): up (1,1), down (1,3), left (0,2) is match tile, right (2,2) is match tile
        // Let's put Honeys at non-match positions adjacent to eliminated tiles
        state.SetTile(1, 1, new Tile(10, ElementType.Item2, 1, 1));
        state.SetCover(new Position(1, 1), new Cover(CoverType.Honey, health: 1));
        state.SetTile(1, 3, new Tile(11, ElementType.Item2, 1, 3));
        state.SetCover(new Position(1, 3), new Cover(CoverType.Honey, health: 1));
        state.SetTile(0, 1, new Tile(12, ElementType.Item2, 0, 1));
        state.SetCover(new Position(0, 1), new Cover(CoverType.Honey, health: 1)); // adjacent to (0,2)

        var processor = CreateProcessor();
        processor.ProcessMatches(ref state, new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 2), new(1, 2), new(2, 2) }
            }
        });

        // All three Honeys destroyed
        Assert.Equal(CoverType.None, state.GetCover(new Position(1, 1)).Type);
        Assert.Equal(CoverType.None, state.GetCover(new Position(1, 3)).Type);
        Assert.Equal(CoverType.None, state.GetCover(new Position(0, 1)).Type);
    }

    #endregion

    #region Honey at board corner — edge safety

    [Fact]
    public void Match_HoneyAtCorner_NoCrash()
    {
        // Honey at (0,0), match at (1,0)(2,0)(3,0) — (0,0) is adjacent to (1,0)
        var state = CreateState();
        state.SetTile(0, 0, new Tile(10, ElementType.Item2, 0, 0));
        state.SetCover(new Position(0, 0), new Cover(CoverType.Honey, health: 1));
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(2, ElementType.Item1, 2, 0));
        state.SetTile(3, 0, new Tile(3, ElementType.Item1, 3, 0));

        var processor = CreateProcessor();
        processor.ProcessMatches(ref state, new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(1, 0), new(2, 0), new(3, 0) }
            }
        });

        // Honey at corner destroyed (adjacent to (1,0)), no out-of-bounds crash
        Assert.Equal(CoverType.None, state.GetCover(new Position(0, 0)).Type);
    }

    #endregion

    #region No cover system — backward compatible

    [Fact]
    public void NoCoverSystem_MatchWorks_NoCrash()
    {
        var state = CreateState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        state.SetTile(1, 1, new Tile(4, ElementType.Item2, 1, 1));
        state.SetCover(new Position(1, 1), new Cover(CoverType.Honey, health: 1));

        // No coverSystem → NotifyBatchElimination not called
        var scoreSystem = new StubScoreSystem();
        var bombRegistry = BombEffectRegistry.CreateDefault();
        var cellEliminator = new CellEliminator(new CoverSystem(), new GroundSystem());
        var processor = new StandardMatchProcessor(scoreSystem, cellEliminator, bombRegistry);

        processor.ProcessMatches(ref state, new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            }
        });

        // Honey untouched (no cover adjacency wiring)
        Assert.Equal(CoverType.Honey, state.GetCover(new Position(1, 1)).Type);
    }

    #endregion

    #region Cage on match position — absorbed, no adjacency for neighbor Honey

    [Fact]
    public void Match_CageAbsorbedTile_DoesNotTriggerAdjacentHoney()
    {
        // Board: [R][R+Cage][R]
        //              [H]       ← Honey at (1,1)
        // (1,0) has Cage → absorbed → not Eliminated → no adjacency for (1,1)
        // (0,0) eliminated but not adjacent to (1,1)
        // (2,0) eliminated but not adjacent to (1,1)
        var state = CreateState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        state.SetTile(1, 1, new Tile(4, ElementType.Item2, 1, 1));
        state.SetCover(new Position(1, 0), new Cover(CoverType.Cage, health: 1));
        state.SetCover(new Position(1, 1), new Cover(CoverType.Honey, health: 1));

        var processor = CreateProcessor();
        processor.ProcessMatches(ref state, new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            }
        });

        // Cage destroyed by direct hit
        Assert.Equal(CoverType.None, state.GetCover(new Position(1, 0)).Type);
        // Honey at (1,1) unchanged — absorbed tile (1,0) doesn't count as Eliminated
        Assert.Equal(CoverType.Honey, state.GetCover(new Position(1, 1)).Type);
    }

    #endregion
}
