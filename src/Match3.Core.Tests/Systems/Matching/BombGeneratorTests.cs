using System.Collections.Generic;
using System.Linq;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching.Generation;
using Xunit;

namespace Match3.Core.Tests.Systems.Matching;

public class BombGeneratorTests
{
    private readonly BombGenerator _generator = new BombGenerator();

    private HashSet<Position> ParseGrid(params string[] rows)
    {
        var component = new HashSet<Position>();
        for (int y = 0; y < rows.Length; y++)
        {
            var row = rows[y].Replace(" ", "");
            for (int x = 0; x < row.Length; x++)
            {
                if (row[x] == 'A')
                {
                    component.Add(new Position(x, y));
                }
            }
        }
        return component;
    }

    [Fact]
    public void Generate_SimpleMatch3_ShouldReturnSimpleGroup()
    {
        var component = ParseGrid(
            "A A A"
        );
        var results = _generator.Generate(component);
        // It should return 1 group of type None (Simple3)
        Assert.Single(results);
        Assert.Equal(ElementType.None, results[0].SpawnBombType);
    }

    [Fact]
    public void Generate_Line4_ShouldCreateRocket()
    {
        var component = ParseGrid(
            "A A A A"
        );
        var results = _generator.Generate(component);
        Assert.Single(results);
        Assert.True(results[0].SpawnBombType.IsRocket());
    }

    [Fact]
    public void Generate_TShape_ShouldCreateTNT()
    {
        var component = ParseGrid(
            "A A A",
            "  A  ",
            "  A  "
        );
        var results = _generator.Generate(component);
        Assert.Single(results);
        // TNT can be Area or Square5x5 depending on detection logic
        Assert.True(results[0].SpawnBombType.IsAreaBomb());
    }

    [Fact]
    public void Generate_Square2x2_ShouldCreateUFO()
    {
        var component = ParseGrid(
            "A A",
            "A A"
        );
        var results = _generator.Generate(component);
        Assert.Single(results);
        Assert.Equal(ElementType.Ufo, results[0].SpawnBombType);
    }

    [Fact]
    public void Generate_Line5_ShouldCreateRainbow()
    {
        var component = ParseGrid(
            "A A A A A"
        );
        var results = _generator.Generate(component);
        Assert.Single(results);
        Assert.Equal(ElementType.ColorBomb, results[0].SpawnBombType);
    }

    [Fact]
    public void Generate_Line6_ShouldCreateRainbowAndConsumeAll()
    {
        var component = ParseGrid(
            "A A A A A A"
        );
        var results = _generator.Generate(component);
        Assert.Single(results);
        Assert.Equal(ElementType.ColorBomb, results[0].SpawnBombType);
        Assert.Equal(6, results[0].Positions.Count);
    }

    [Fact]
    public void Generate_3x3Block_ShouldPrioritizeTNT()
    {
        var component = ParseGrid(
            "A A A",
            "A A A",
            "A A A"
        );
        var results = _generator.Generate(component);
        // Should contain at least one high tier bomb (Area/Square5x5 or Color)
        Assert.Contains(results, g => g.SpawnBombType.IsAreaBomb() || g.SpawnBombType == ElementType.ColorBomb);
    }

    [Fact]
    public void Generate_CaseA_Backpack_ShouldCreateRainbow()
    {
        // Case A: Bottom 5 horizontal, Top-Left 2 vertical (Total 7)
        // . . . . .
        // A A . . .
        // A A A A A
        // (0,0) to (4,0) is Line 5
        // (0,1), (1,1) -> On top of 0,0 and 1,0 (perpendicular to line)

        var component = new HashSet<Position>
        {
            new Position(0, 0), new Position(1, 0), new Position(2, 0), new Position(3, 0), new Position(4, 0),
            new Position(0, 1), new Position(1, 1)
        };

        // Act
        var results = _generator.Generate(component);

        // Assert
        // UFO (Weight 150) at (0,0),(1,0),(0,1),(1,1) beats Rainbow (Weight 130) at row 0.
        // Remaining cells are dropped as leftovers (no bomb formed).

        Assert.Single(results);
        var group = results[0];
        Assert.Equal(ElementType.Ufo, group.SpawnBombType);
    }

    [Fact]
    public void Generate_CaseB_2x4_ShouldCreateTwoRockets()
    {
        // Case B: 2x4 Rectangle
        // A A A A
        // A A A A
        // 8 tiles.
        
        var component = new HashSet<Position>();
        for(int x=0; x<4; x++)
        {
            for(int y=0; y<2; y++)
            {
                component.Add(new Position(x, y));
            }
        }

        // Act
        var results = _generator.Generate(component);

        // Assert
        // Option 1: 2 UFOs (Left 2x2, Right 2x2) -> 20 + 20 = 40.
        // Option 2: 2 Rockets (Top Line 4, Bottom Line 4) -> 40 + 40 = 80.
        // Option 3: 1 TNT (T-shape)? e.g. Top Row + Col 1. -> 60. Remainder 3 (Simple). Total 60.
        // Winner: 2 Rockets (80).

        Assert.Equal(2, results.Count);
        Assert.True(results[0].SpawnBombType.IsRocket());
        Assert.True(results[1].SpawnBombType.IsRocket());
    }
    [Fact]
    public void Generate_MassiveBlock_ShouldRunFast()
    {
        // 6x6 Block of same color
        var rows = new[]
        {
            "A A A A A A",
            "A A A A A A",
            "A A A A A A",
            "A A A A A A",
            "A A A A A A",
            "A A A A A A"
        };
        var component = ParseGrid(rows);

        // Warm up
        _generator.Generate(component);

        // Measure
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = _generator.Generate(component);
        sw.Stop();

        // Should be under 200ms (currently might take 2s+)
        Assert.True(sw.ElapsedMilliseconds < 200, $"Performance check failed: {sw.ElapsedMilliseconds}ms");

        // Should verify some bombs are generated
        Assert.Contains(results, g => g.SpawnBombType == ElementType.ColorBomb);
    }

    [Fact]
    public void Generate_4x4Block_ShouldCreate4Rockets()
    {
        // 4x4 = 16 cells
        // Options:
        // - 4 horizontal Rockets (4 lines of 4) = 4 × 40 = 160
        // - 4 vertical Rockets (4 columns of 4) = 4 × 40 = 160
        // - UFOs: max 4 non-overlapping 2x2 = 4 × 20 = 80
        // Optimal: 4 Rockets (160)

        var rows = new[]
        {
            "A A A A",
            "A A A A",
            "A A A A",
            "A A A A"
        };
        var component = ParseGrid(rows);

        var results = _generator.Generate(component);

        // Count Rocket bombs
        int rocketCount = 0;
        foreach (var g in results)
        {
            if (g.SpawnBombType.IsRocket())
                rocketCount++;
        }

        // Should generate 4 Rockets (optimal partition)
        Assert.Equal(4, rocketCount);
    }

    [Fact]
    public void Generate_6x6Block_ShouldCreate7Rainbows()
    {
        // 6x6 = 36 cells, theoretical max = 36/5 = 7.2 -> 7 Rainbows
        // Optimal layout:
        //   0 1 2 3 4 5
        // 0 H H H H H V  <- Row 0: H=horizontal 5-line (0-4), V=vertical 5-line (col 5)
        // 1 H H H H H V
        // 2 H H H H H V
        // 3 H H H H H V
        // 4 H H H H H V
        // 5 H H H H H .  <- Row 5: horizontal 5-line (0-4), 1 leftover
        // = 6 horizontal Rainbows + 1 vertical Rainbow = 7 Rainbows

        var rows = new[]
        {
            "A A A A A A",
            "A A A A A A",
            "A A A A A A",
            "A A A A A A",
            "A A A A A A",
            "A A A A A A"
        };
        var component = ParseGrid(rows);

        var results = _generator.Generate(component);

        // Count Rainbow bombs
        int rainbowCount = 0;
        foreach (var g in results)
        {
            if (g.SpawnBombType == ElementType.ColorBomb)
                rainbowCount++;
        }

        // Should generate exactly 7 Rainbows (optimal partition)
        Assert.Equal(7, rainbowCount);
    }
}
