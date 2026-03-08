using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Matching;

/// <summary>
/// Tests for scrap absorption rules.
/// Core principle: 有结构依据的吸收 (Structured absorption)
/// - Simple3: No absorption
/// - Line4/Line5: Collinear + continuous
/// - Square (2x2): Orthogonal adjacent (recursive)
/// - Cross (T/L/+): Collinear with intersection + continuous
/// </summary>
public class AbsorptionRuleTests
{
    private readonly BombGenerator _generator = new();

    #region Simple3 - No Absorption

    [Fact]
    public void Simple3_Horizontal_DoesNotAbsorbAdjacentScrap()
    {
        // A A A
        //     S   ← S should NOT be absorbed
        var component = new HashSet<Position>
        {
            new(0, 0), new(1, 0), new(2, 0),  // Horizontal 3-line
            new(2, 1)                          // Scrap below
        };

        var results = _generator.Generate(component);

        Assert.Single(results);
        Assert.Equal(3, results[0].Positions.Count);
        Assert.DoesNotContain(new Position(2, 1), results[0].Positions);
    }

    [Fact]
    public void Simple3_Vertical_DoesNotAbsorbAdjacentScrap()
    {
        // A S
        // A
        // A
        var component = new HashSet<Position>
        {
            new(0, 0), new(0, 1), new(0, 2),  // Vertical 3-line
            new(1, 0)                          // Scrap to the right
        };

        var results = _generator.Generate(component);

        Assert.Single(results);
        Assert.Equal(3, results[0].Positions.Count);
        Assert.DoesNotContain(new Position(1, 0), results[0].Positions);
    }

    #endregion

    #region Line4 - Collinear + Continuous

    [Fact]
    public void Line4_Horizontal_AbsorbsCollinearContinuousScrap()
    {
        // A A A A S   ← S is collinear and adjacent, should be absorbed
        var component = new HashSet<Position>
        {
            new(0, 0), new(1, 0), new(2, 0), new(3, 0),  // Horizontal 4-line
            new(4, 0)                                      // Scrap at end
        };

        var results = _generator.Generate(component);

        // Should become a 5-line (Rainbow) after absorption
        Assert.Single(results);
        Assert.Equal(5, results[0].Positions.Count);
        Assert.Contains(new Position(4, 0), results[0].Positions);
    }

    [Fact]
    public void Line4_Horizontal_DoesNotAbsorbPerpendicularScrap()
    {
        // A A A A
        //       S   ← S is perpendicular, should NOT be absorbed
        var component = new HashSet<Position>
        {
            new(0, 0), new(1, 0), new(2, 0), new(3, 0),  // Horizontal 4-line
            new(3, 1)                                      // Scrap below
        };

        var results = _generator.Generate(component);

        Assert.Single(results);
        Assert.Equal(4, results[0].Positions.Count);
        Assert.DoesNotContain(new Position(3, 1), results[0].Positions);
    }

    [Fact]
    public void Line4_Horizontal_DoesNotAbsorbNonContinuousScrap()
    {
        // A A A A _ S   ← S is collinear but not continuous (gap), should NOT be absorbed
        var component = new HashSet<Position>
        {
            new(0, 0), new(1, 0), new(2, 0), new(3, 0),  // Horizontal 4-line
            new(5, 0)                                      // Scrap with gap
        };

        var results = _generator.Generate(component);

        Assert.Single(results);
        Assert.Equal(4, results[0].Positions.Count);
        Assert.DoesNotContain(new Position(5, 0), results[0].Positions);
    }

    [Fact]
    public void Line4_Vertical_AbsorbsCollinearContinuousScrap()
    {
        // S
        // A
        // A
        // A
        // A
        var component = new HashSet<Position>
        {
            new(0, 1), new(0, 2), new(0, 3), new(0, 4),  // Vertical 4-line
            new(0, 0)                                      // Scrap at top
        };

        var results = _generator.Generate(component);

        Assert.Single(results);
        Assert.Equal(5, results[0].Positions.Count);
        Assert.Contains(new Position(0, 0), results[0].Positions);
    }

    #endregion

    #region Line5 - Collinear + Continuous

    [Fact]
    public void Line5_AbsorbsCollinearContinuousScrap()
    {
        // S A A A A A S   ← Both S should be absorbed
        var component = new HashSet<Position>
        {
            new(1, 0), new(2, 0), new(3, 0), new(4, 0), new(5, 0),  // Horizontal 5-line
            new(0, 0), new(6, 0)                                       // Scraps at both ends
        };

        var results = _generator.Generate(component);

        Assert.Single(results);
        Assert.Equal(7, results[0].Positions.Count);
        Assert.Contains(new Position(0, 0), results[0].Positions);
        Assert.Contains(new Position(6, 0), results[0].Positions);
    }

    [Fact]
    public void Line5_DoesNotAbsorbPerpendicularScrap()
    {
        // A A A A A
        //     S       ← S should NOT be absorbed
        var component = new HashSet<Position>
        {
            new(0, 0), new(1, 0), new(2, 0), new(3, 0), new(4, 0),  // Horizontal 5-line
            new(2, 1)                                                   // Scrap below
        };

        var results = _generator.Generate(component);

        Assert.Single(results);
        Assert.Equal(5, results[0].Positions.Count);
        Assert.DoesNotContain(new Position(2, 1), results[0].Positions);
    }

    #endregion

    #region Square (2x2) - Orthogonal Adjacent + Recursive

    [Fact]
    public void Square_AbsorbsOrthogonallyAdjacentScrap_Above()
    {
        //   S
        // A A
        // A A
        var component = new HashSet<Position>
        {
            new(0, 1), new(1, 1),  // Top row of 2x2
            new(0, 2), new(1, 2),  // Bottom row of 2x2
            new(1, 0)              // Scrap above
        };

        var results = _generator.Generate(component);

        Assert.Single(results);
        Assert.Equal(5, results[0].Positions.Count);
        Assert.Contains(new Position(1, 0), results[0].Positions);
    }

    [Fact]
    public void Square_AbsorbsOrthogonallyAdjacentScrap_Right()
    {
        // A A S
        // A A
        var component = new HashSet<Position>
        {
            new(0, 0), new(1, 0),  // Top row
            new(0, 1), new(1, 1),  // Bottom row
            new(2, 0)              // Scrap to right
        };

        var results = _generator.Generate(component);

        Assert.Single(results);
        Assert.Equal(5, results[0].Positions.Count);
        Assert.Contains(new Position(2, 0), results[0].Positions);
    }

    [Fact]
    public void Square_AbsorbsOrthogonallyConnectedScrap()
    {
        // S A A   ← S is orthogonally adjacent to A at (1,0), so it's absorbed
        //   A A
        var component = new HashSet<Position>
        {
            new(1, 0), new(2, 0),  // Top row
            new(1, 1), new(2, 1),  // Bottom row
            new(0, 0)              // Scrap - orthogonally adjacent to (1,0)
        };

        var results = _generator.Generate(component);

        Assert.Single(results);
        // S should be absorbed because it's orthogonally adjacent to the 2x2
        Assert.Equal(5, results[0].Positions.Count);
        Assert.Contains(new Position(0, 0), results[0].Positions);
    }

    [Fact]
    public void Square_RecursivelyAbsorbsOrthogonalChain_BentShape()
    {
        // Bent chain to avoid creating a 4-line
        // S2
        // S1 A A
        //    A A
        var component = new HashSet<Position>
        {
            new(1, 1), new(2, 1),  // Top row of 2x2
            new(1, 2), new(2, 2),  // Bottom row of 2x2
            new(0, 1),             // S1 - orthogonal to 2x2 (left of top-left)
            new(0, 0)              // S2 - orthogonal to S1 (above S1)
        };

        var results = _generator.Generate(component);

        Assert.Single(results);
        Assert.Equal(6, results[0].Positions.Count);
        Assert.Contains(new Position(0, 1), results[0].Positions);
        Assert.Contains(new Position(0, 0), results[0].Positions);
    }

    [Fact]
    public void Square_AllConnectedScrapsAbsorbed_LongerChain()
    {
        // In a connected component with only a 2x2, all scraps will be absorbed
        // because of recursive orthogonal absorption
        // Using a bent chain to avoid creating a 4-line
        // S3 S2
        //    S1 A A
        //       A A
        var component = new HashSet<Position>
        {
            new(2, 1), new(3, 1),  // Top row of 2x2
            new(2, 2), new(3, 2),  // Bottom row of 2x2
            new(1, 1),             // S1 - orthogonal to 2x2
            new(1, 0),             // S2 - orthogonal to S1
            new(0, 0)              // S3 - orthogonal to S2
        };

        var results = _generator.Generate(component);

        Assert.Single(results);
        // All 7 cells absorbed (recursive)
        Assert.Equal(7, results[0].Positions.Count);
        Assert.Contains(new Position(1, 1), results[0].Positions);
        Assert.Contains(new Position(1, 0), results[0].Positions);
        Assert.Contains(new Position(0, 0), results[0].Positions);
    }

    #endregion

    #region Cross (T/L) - Collinear with Intersection + Continuous

    [Fact]
    public void TShape_AbsorbsCollinearContinuousScrap_Horizontal()
    {
        // S A A A     ← S is collinear with intersection (1,0) and continuous
        //     A
        //     A
        var component = new HashSet<Position>
        {
            new(1, 0), new(2, 0), new(3, 0),  // Horizontal line
            new(2, 1), new(2, 2),              // Vertical line (shares (2,0) but we count from here)
            new(0, 0)                          // Scrap at left
        };

        // Wait, this T shape has intersection at (2,0)
        // Horizontal: (1,0), (2,0), (3,0) - 3 cells
        // Vertical: (2,0), (2,1), (2,2) - 3 cells
        // Union: 5 cells, this is a valid T

        var results = _generator.Generate(component);

        // S at (0,0) should be absorbed because it's collinear with intersection (2,0) on same row
        // and the path from (0,0) to (2,0) needs to be continuous
        // (0,0) -> (1,0) -> (2,0), all exist, so should absorb
        Assert.Single(results);
        Assert.Equal(6, results[0].Positions.Count);
        Assert.Contains(new Position(0, 0), results[0].Positions);
    }

    [Fact]
    public void TShape_AbsorbsCollinearContinuousScrap_Vertical()
    {
        // A A A
        //   A
        //   A
        //   S     ← S is collinear with intersection and continuous
        var component = new HashSet<Position>
        {
            new(0, 0), new(1, 0), new(2, 0),  // Horizontal line
            new(1, 1), new(1, 2),              // Vertical line
            new(1, 3)                          // Scrap below
        };

        var results = _generator.Generate(component);

        Assert.Single(results);
        Assert.Equal(6, results[0].Positions.Count);
        Assert.Contains(new Position(1, 3), results[0].Positions);
    }

    [Fact]
    public void TShape_DoesNotAbsorbNonCollinearScrap()
    {
        // A A A .
        //   A . S   ← S is not collinear with intersection
        //   A
        var component = new HashSet<Position>
        {
            new(0, 0), new(1, 0), new(2, 0),  // Horizontal line
            new(1, 1), new(1, 2),              // Vertical line
            new(3, 1)                          // Scrap - not collinear
        };

        var results = _generator.Generate(component);

        Assert.Single(results);
        Assert.Equal(5, results[0].Positions.Count);
        Assert.DoesNotContain(new Position(3, 1), results[0].Positions);
    }

    [Fact]
    public void TShape_DoesNotAbsorbNonContinuousScrap()
    {
        // A A A . S   ← S is collinear but not continuous (gap at (3,0))
        //   A
        //   A
        var component = new HashSet<Position>
        {
            new(0, 0), new(1, 0), new(2, 0),  // Horizontal line
            new(1, 1), new(1, 2),              // Vertical line
            new(4, 0)                          // Scrap - collinear but gap
        };

        var results = _generator.Generate(component);

        Assert.Single(results);
        Assert.Equal(5, results[0].Positions.Count);
        Assert.DoesNotContain(new Position(4, 0), results[0].Positions);
    }

    [Fact]
    public void LShape_AbsorbsCollinearContinuousScrap()
    {
        // A A A S   ← S is collinear with intersection (0,0) and continuous
        // A
        // A
        var component = new HashSet<Position>
        {
            new(0, 0), new(1, 0), new(2, 0),  // Horizontal line
            new(0, 1), new(0, 2),              // Vertical line
            new(3, 0)                          // Scrap to the right
        };

        var results = _generator.Generate(component);

        Assert.Single(results);
        Assert.Equal(6, results[0].Positions.Count);
        Assert.Contains(new Position(3, 0), results[0].Positions);
    }

    #endregion

    #region Plus (+) Shape - Collinear with Center + Continuous

    [Fact]
    public void PlusShape_AbsorbsCollinearContinuousScrap()
    {
        //     S
        //     A
        //   A A A   ← Center/intersection at (1,2)
        //     A
        var component = new HashSet<Position>
        {
            new(0, 2), new(1, 2), new(2, 2),  // Horizontal line
            new(1, 1), new(1, 3),              // Vertical line (1,2 is shared)
            new(1, 0)                          // Scrap above
        };

        var results = _generator.Generate(component);

        Assert.Single(results);
        Assert.Equal(6, results[0].Positions.Count);
        Assert.Contains(new Position(1, 0), results[0].Positions);
    }

    [Fact]
    public void PlusShape_DoesNotAbsorbNonCollinearScrap()
    {
        //   S A
        //   A A A
        //     A
        var component = new HashSet<Position>
        {
            new(1, 1), new(2, 1), new(3, 1),  // Horizontal line
            new(2, 0), new(2, 2),              // Vertical line
            new(1, 0)                          // Scrap - not collinear with center (2,1)
        };

        var results = _generator.Generate(component);

        Assert.Single(results);
        Assert.Equal(5, results[0].Positions.Count);
        Assert.DoesNotContain(new Position(1, 0), results[0].Positions);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void NotTShape_HorizontalOnly3_VerticalOnly2_NoAbsorption()
    {
        // A A A
        //   A     ← This is NOT a T-shape (vertical only has 2 cells including intersection)
        // It's Simple3 + stray, stray should not be absorbed
        var component = new HashSet<Position>
        {
            new(0, 0), new(1, 0), new(2, 0),  // Horizontal 3-line
            new(1, 1)                          // Single cell below - not enough for T
        };

        var results = _generator.Generate(component);

        // Should detect as Simple3 only
        Assert.Single(results);
        Assert.Equal(3, results[0].Positions.Count);
        Assert.DoesNotContain(new Position(1, 1), results[0].Positions);
    }

    [Fact]
    public void MultipleScrapsContinuous_AllAbsorbed()
    {
        // S1 S2 A A A A   ← S1, S2 both continuous, both absorbed
        var component = new HashSet<Position>
        {
            new(2, 0), new(3, 0), new(4, 0), new(5, 0),  // Horizontal 4-line
            new(0, 0), new(1, 0)                           // Two scraps to the left
        };

        var results = _generator.Generate(component);

        Assert.Single(results);
        Assert.Equal(6, results[0].Positions.Count);
        Assert.Contains(new Position(0, 0), results[0].Positions);
        Assert.Contains(new Position(1, 0), results[0].Positions);
    }

    [Fact]
    public void UnabsorbedScraps_FormValidLine_CreateSeparateGroup()
    {
        // This tests that scraps that can't be absorbed but form valid lines
        // are treated as separate match groups
        // T-shape + separate 3-line that can't be absorbed

        // A A A     ← T-shape
        //   A
        //   A
        //
        // B B B     ← Separate 3-line (same color in component)
        var component = new HashSet<Position>
        {
            new(0, 0), new(1, 0), new(2, 0),  // T horizontal
            new(1, 1), new(1, 2),              // T vertical
            new(0, 4), new(1, 4), new(2, 4)   // Separate horizontal line
        };

        var results = _generator.Generate(component);

        // Should have 2 groups: T-shape and Simple3
        Assert.Equal(2, results.Count);
    }

    #endregion

    #region Plus vs T/L Bomb Type

    [Fact]
    public void PlusShape_GeneratesUFO()
    {
        // Plus (+): symmetric cross with center at (1,1)
        //   A       y=0
        // A A A     y=1
        //   A       y=2
        var component = new HashSet<Position>
        {
            new(1, 0),              // Top
            new(0, 1), new(1, 1), new(2, 1),  // Middle row
            new(1, 2)               // Bottom
        };

        var results = _generator.Generate(component);

        Assert.Single(results);
        Assert.Equal(ElementType.Ufo, results[0].SpawnBombType);
        Assert.Equal(5, results[0].Positions.Count);
    }

    [Fact]
    public void TShape_GeneratesTNT()
    {
        // T-shape: intersection NOT at center of horizontal line
        // A A A     y=0  (intersection at (1,0), which is center of horizontal)
        //   A       y=1
        //   A       y=2
        // But vertical line has 3 cells, intersection at (1,0) is at the END of vertical
        var component = new HashSet<Position>
        {
            new(0, 0), new(1, 0), new(2, 0),  // Horizontal 3
            new(1, 1), new(1, 2)               // Vertical continues down
        };

        var results = _generator.Generate(component);

        Assert.Single(results);
        Assert.Equal(ElementType.Square5x5, results[0].SpawnBombType); // TNT
        Assert.Equal(5, results[0].Positions.Count);
    }

    [Fact]
    public void LShape_GeneratesTNT()
    {
        // L-shape: intersection at corner (0,0)
        // A A A     y=0
        // A         y=1
        // A         y=2
        var component = new HashSet<Position>
        {
            new(0, 0), new(1, 0), new(2, 0),  // Horizontal 3
            new(0, 1), new(0, 2)               // Vertical 3 (shares (0,0))
        };

        var results = _generator.Generate(component);

        Assert.Single(results);
        Assert.Equal(ElementType.Square5x5, results[0].SpawnBombType); // TNT
        Assert.Equal(5, results[0].Positions.Count);
    }

    [Fact]
    public void LongTShape_GeneratesTNT()
    {
        // Long T: horizontal 4, vertical 3
        // A A A A   y=0
        //   A       y=1
        //   A       y=2
        var component = new HashSet<Position>
        {
            new(0, 0), new(1, 0), new(2, 0), new(3, 0),  // Horizontal 4
            new(1, 1), new(1, 2)                          // Vertical continues down
        };

        var results = _generator.Generate(component);

        Assert.Single(results);
        Assert.Equal(ElementType.Square5x5, results[0].SpawnBombType); // TNT (not UFO)
    }

    #endregion
}
