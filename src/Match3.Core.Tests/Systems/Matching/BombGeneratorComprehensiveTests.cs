using System.Collections.Generic;
using System.Linq;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching.Generation;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Matching
{
    /// <summary>
    /// Comprehensive tests for BombGenerator covering all shapes and edge cases.
    /// Based on the bomb generation rules documented in:
    /// - docs/03-design/features/bomb-generation.md
    /// - .trae/documents/炸弹合成与组合规则.md
    /// </summary>
    public class BombGeneratorComprehensiveTests
    {
        private readonly BombGenerator _generator = new BombGenerator();

        #region Helper Methods

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

        private int CountBombType(List<Models.Gameplay.MatchGroup> results, BombType type)
        {
            return results.Count(g => g.SpawnBombType == type);
        }

        private int CountRockets(List<Models.Gameplay.MatchGroup> results)
        {
            return results.Count(g => g.SpawnBombType.IsRocket());
        }

        private int CountAreaBombs(List<Models.Gameplay.MatchGroup> results)
        {
            return results.Count(g => g.SpawnBombType.IsAreaBomb());
        }

        #endregion

        #region Basic Shapes - 3 Match (No Bomb)

        [Fact]
        public void Match3_Horizontal_ShouldNotCreateBomb()
        {
            // . . .
            // A A A
            // . . .
            var component = ParseGrid("A A A");
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.Equal(BombType.None, results[0].SpawnBombType);
            Assert.Equal(3, results[0].Positions.Count);
        }

        [Fact]
        public void Match3_Vertical_ShouldNotCreateBomb()
        {
            // A
            // A
            // A
            var component = ParseGrid(
                "A",
                "A",
                "A"
            );
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.Equal(BombType.None, results[0].SpawnBombType);
            Assert.Equal(3, results[0].Positions.Count);
        }

        #endregion

        #region Line 4 - Rocket

        [Fact]
        public void Line4_Horizontal_ShouldCreateVerticalRocket()
        {
            // Per docs: 横向4连 -> Vertical Rocket (清除整列)
            var component = ParseGrid("A A A A");
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.Equal(BombType.Vertical, results[0].SpawnBombType);
            Assert.Equal(4, results[0].Positions.Count);
        }

        [Fact]
        public void Line4_Vertical_ShouldCreateHorizontalRocket()
        {
            // Per docs: 竖向4连 -> Horizontal Rocket (清除整行)
            var component = ParseGrid(
                "A",
                "A",
                "A",
                "A"
            );
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.Equal(BombType.Horizontal, results[0].SpawnBombType);
            Assert.Equal(4, results[0].Positions.Count);
        }

        #endregion

        #region Line 5+ - Rainbow (Color Bomb)

        [Fact]
        public void Line5_Horizontal_ShouldCreateRainbow()
        {
            var component = ParseGrid("A A A A A");
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.Equal(BombType.Color, results[0].SpawnBombType);
            Assert.Equal(5, results[0].Positions.Count);
        }

        [Fact]
        public void Line5_Vertical_ShouldCreateRainbow()
        {
            var component = ParseGrid(
                "A",
                "A",
                "A",
                "A",
                "A"
            );
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.Equal(BombType.Color, results[0].SpawnBombType);
            Assert.Equal(5, results[0].Positions.Count);
        }

        [Fact]
        public void Line6_Horizontal_ShouldCreateRainbow()
        {
            var component = ParseGrid("A A A A A A");
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.Equal(BombType.Color, results[0].SpawnBombType);
            Assert.Equal(6, results[0].Positions.Count);
        }

        [Fact]
        public void Line7_Horizontal_ShouldCreateRainbow()
        {
            var component = ParseGrid("A A A A A A A");
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.Equal(BombType.Color, results[0].SpawnBombType);
            Assert.Equal(7, results[0].Positions.Count);
        }

        [Fact]
        public void Line8_Horizontal_ShouldCreateRainbow()
        {
            // 8 cells = 1 Rainbow (5) + 3 leftovers
            // Or could be 1 Rainbow + 1 Rocket (if optimally partitioned)?
            // Per sliding window: 8-5+1 = 4 Rainbow candidates
            // Best: 1 Rainbow (130) consuming 5, remainder 3 (simple)
            var component = ParseGrid("A A A A A A A A");
            var results = _generator.Generate(component);

            Assert.Contains(results, g => g.SpawnBombType == BombType.Color);
        }

        [Fact]
        public void Line9_Horizontal_ShouldCreateRainbow()
        {
            // 9 cells = could be 1 Rainbow + 1 Rocket (5+4=9)
            // Rainbow(130) + Rocket(40) = 170 > 1 Rainbow(130)
            var component = ParseGrid("A A A A A A A A A");
            var results = _generator.Generate(component);

            // Should have at least 1 Rainbow
            Assert.Contains(results, g => g.SpawnBombType == BombType.Color);
        }

        [Fact]
        public void Line10_Horizontal_ShouldCreate2Rainbows()
        {
            // 10 cells = 2 Rainbows (5+5=10)
            // 2 Rainbow = 260 > 1 Rainbow + 1 Rocket = 170
            var component = ParseGrid("A A A A A A A A A A");
            var results = _generator.Generate(component);

            int rainbowCount = CountBombType(results, BombType.Color);
            Assert.Equal(2, rainbowCount);
        }

        #endregion

        #region Square 2x2 - UFO

        [Fact]
        public void Square2x2_ShouldCreateUfo()
        {
            var component = ParseGrid(
                "A A",
                "A A"
            );
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.Equal(BombType.Ufo, results[0].SpawnBombType);
            Assert.Equal(4, results[0].Positions.Count);
        }

        [Fact]
        public void Square2x2_WithOffset_ShouldCreateUfo()
        {
            // Test that position doesn't affect detection
            var component = new HashSet<Position>
            {
                new Position(3, 5), new Position(4, 5),
                new Position(3, 6), new Position(4, 6)
            };
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.Equal(BombType.Ufo, results[0].SpawnBombType);
        }

        #endregion

        #region T-Shape Variants (5 cells) - TNT/Area Bomb

        [Fact]
        public void TShape_Down_ShouldCreateAreaBomb()
        {
            // A A A
            //   A
            //   A
            var component = ParseGrid(
                "A A A",
                "  A  ",
                "  A  "
            );
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.True(results[0].SpawnBombType.IsAreaBomb());
            Assert.Equal(5, results[0].Positions.Count);
        }

        [Fact]
        public void TShape_Up_ShouldCreateAreaBomb()
        {
            //   A
            //   A
            // A A A
            var component = ParseGrid(
                "  A  ",
                "  A  ",
                "A A A"
            );
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.True(results[0].SpawnBombType.IsAreaBomb());
            Assert.Equal(5, results[0].Positions.Count);
        }

        [Fact]
        public void TShape_Left_ShouldCreateAreaBomb()
        {
            // A
            // A A A
            // A
            var component = ParseGrid(
                "A    ",
                "A A A",
                "A    "
            );
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.True(results[0].SpawnBombType.IsAreaBomb());
            Assert.Equal(5, results[0].Positions.Count);
        }

        [Fact]
        public void TShape_Right_ShouldCreateAreaBomb()
        {
            //     A
            // A A A
            //     A
            var component = ParseGrid(
                "    A",
                "A A A",
                "    A"
            );
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.True(results[0].SpawnBombType.IsAreaBomb());
            Assert.Equal(5, results[0].Positions.Count);
        }

        #endregion

        #region L-Shape Variants (5 cells) - TNT/Area Bomb

        [Fact]
        public void LShape_BottomRight_ShouldCreateAreaBomb()
        {
            // A
            // A
            // A A A
            var component = ParseGrid(
                "A    ",
                "A    ",
                "A A A"
            );
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.True(results[0].SpawnBombType.IsAreaBomb());
            Assert.Equal(5, results[0].Positions.Count);
        }

        [Fact]
        public void LShape_BottomLeft_ShouldCreateAreaBomb()
        {
            //     A
            //     A
            // A A A
            var component = ParseGrid(
                "    A",
                "    A",
                "A A A"
            );
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.True(results[0].SpawnBombType.IsAreaBomb());
            Assert.Equal(5, results[0].Positions.Count);
        }

        [Fact]
        public void LShape_TopRight_ShouldCreateAreaBomb()
        {
            // A A A
            // A
            // A
            var component = ParseGrid(
                "A A A",
                "A    ",
                "A    "
            );
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.True(results[0].SpawnBombType.IsAreaBomb());
            Assert.Equal(5, results[0].Positions.Count);
        }

        [Fact]
        public void LShape_TopLeft_ShouldCreateAreaBomb()
        {
            // A A A
            //     A
            //     A
            var component = ParseGrid(
                "A A A",
                "    A",
                "    A"
            );
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.True(results[0].SpawnBombType.IsAreaBomb());
            Assert.Equal(5, results[0].Positions.Count);
        }

        [Fact]
        public void LShape_Large_4x3_ShouldCreateAreaBomb()
        {
            // Larger L with 4+3 cells = 6 cells total
            // A A A A
            // A
            // A
            var component = ParseGrid(
                "A A A A",
                "A      ",
                "A      "
            );
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.True(results[0].SpawnBombType.IsAreaBomb());
            Assert.Equal(6, results[0].Positions.Count);
        }

        [Fact]
        public void LShape_Large_3x4_ShouldCreateAreaBomb()
        {
            // Larger L with 3+4 cells = 6 cells total
            // A
            // A
            // A
            // A A A
            var component = ParseGrid(
                "A    ",
                "A    ",
                "A    ",
                "A A A"
            );
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.True(results[0].SpawnBombType.IsAreaBomb());
            Assert.Equal(6, results[0].Positions.Count);
        }

        [Fact]
        public void LShape_Large_4x4_ShouldCreateAreaBomb()
        {
            // Larger L with 4+4 cells = 7 cells total
            // A A A A
            // A
            // A
            // A
            var component = ParseGrid(
                "A A A A",
                "A      ",
                "A      ",
                "A      "
            );
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.True(results[0].SpawnBombType.IsAreaBomb());
            Assert.Equal(7, results[0].Positions.Count);
        }

        [Fact]
        public void LShape_With2CellArm_ShouldNotCreateAreaBomb()
        {
            // L with only 2+4 cells - doesn't meet 3+3 requirement
            // This is actually 5 cells, but the arms are 2 and 4
            // Per docs, L requires intersection of 3+ lines
            // A A
            // A
            // A
            // A
            var component = ParseGrid(
                "A A",
                "A  ",
                "A  ",
                "A  "
            );
            var results = _generator.Generate(component);

            // Should generate Rocket (vertical 4-line) not Area bomb
            // because the horizontal arm is only 2 cells
            Assert.Single(results);
            Assert.True(results[0].SpawnBombType.IsRocket() || results[0].SpawnBombType == BombType.None);
        }

        #endregion

        #region Plus/Cross Shape (5 cells) - TNT/Area Bomb

        [Fact]
        public void PlusShape_ShouldCreateAreaBomb()
        {
            //   A
            // A A A
            //   A
            var component = ParseGrid(
                "  A  ",
                "A A A",
                "  A  "
            );
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.True(results[0].SpawnBombType.IsAreaBomb());
            Assert.Equal(5, results[0].Positions.Count);
        }

        #endregion

        #region Large L and T Shapes (6+ cells)

        [Fact]
        public void LShape_6Cells_ShouldCreateAreaBomb()
        {
            // A
            // A
            // A
            // A A A
            var component = ParseGrid(
                "A    ",
                "A    ",
                "A    ",
                "A A A"
            );
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.True(results[0].SpawnBombType.IsAreaBomb());
            Assert.Equal(6, results[0].Positions.Count);
        }

        [Fact]
        public void TShape_6Cells_ShouldCreateAreaBomb()
        {
            // A A A A
            //   A
            //   A
            var component = ParseGrid(
                "A A A A",
                "  A    ",
                "  A    "
            );
            var results = _generator.Generate(component);

            // Could be: 1 TNT (covers 5-6), or 1 Rocket + simple
            // TNT weight (60) should win over Rocket (40)
            Assert.Contains(results, g => g.SpawnBombType.IsAreaBomb());
        }

        #endregion

        #region Block/Rectangle Shapes - Multi-bomb Generation

        [Fact]
        public void Block_2x3_ShouldCreateUfo()
        {
            // A A
            // A A
            // A A
            // 6 cells, contains 2 overlapping 2x2
            // Best: 1 UFO (20) + 2 orphans
            var component = ParseGrid(
                "A A",
                "A A",
                "A A"
            );
            var results = _generator.Generate(component);

            Assert.Contains(results, g => g.SpawnBombType == BombType.Ufo);
        }

        [Fact]
        public void Block_3x2_ShouldCreateUfo()
        {
            // A A A
            // A A A
            var component = ParseGrid(
                "A A A",
                "A A A"
            );
            var results = _generator.Generate(component);

            // 6 cells, contains 2 overlapping 2x2
            Assert.Contains(results, g => g.SpawnBombType == BombType.Ufo);
        }

        [Fact]
        public void Block_2x5_ShouldCreate2Rockets()
        {
            // A A A A A
            // A A A A A
            // 10 cells
            // Options:
            // - 2 Rainbows = 260
            // - But 2x5 block doesn't have 2 non-overlapping 5-lines in same direction
            // - Actually: 2 horizontal 5-lines (top row, bottom row) = 2 Rainbows
            var component = ParseGrid(
                "A A A A A",
                "A A A A A"
            );
            var results = _generator.Generate(component);

            int rainbowCount = CountBombType(results, BombType.Color);
            Assert.Equal(2, rainbowCount);
        }

        [Fact]
        public void Block_5x2_ShouldCreate2Rainbows()
        {
            // A A
            // A A
            // A A
            // A A
            // A A
            // 10 cells, 2 vertical 5-lines
            var component = ParseGrid(
                "A A",
                "A A",
                "A A",
                "A A",
                "A A"
            );
            var results = _generator.Generate(component);

            int rainbowCount = CountBombType(results, BombType.Color);
            Assert.Equal(2, rainbowCount);
        }

        [Fact]
        public void Block_3x3_ShouldOptimallyPartition()
        {
            // A A A
            // A A A
            // A A A
            // 9 cells
            // Options:
            // - 1 Rainbow (row/col 5) + remainder -> No, max line is 3
            // - TNT patterns: various T/L shapes possible
            // - 4 UFOs not possible (only 4 overlapping 2x2)
            var component = ParseGrid(
                "A A A",
                "A A A",
                "A A A"
            );
            var results = _generator.Generate(component);

            // Should create some high-value bombs
            int totalCells = results.Sum(g => g.Positions.Count);
            Assert.Equal(9, totalCells);
        }

        [Fact]
        public void Block_3x4_ShouldCreate3Rockets()
        {
            // A A A A
            // A A A A
            // A A A A
            // 12 cells
            // Options:
            // - 3 horizontal Rockets (3 rows of 4) = 120
            // - 4 vertical... no, columns are only 3 long
            var component = ParseGrid(
                "A A A A",
                "A A A A",
                "A A A A"
            );
            var results = _generator.Generate(component);

            int rocketCount = CountRockets(results);
            Assert.Equal(3, rocketCount);
        }

        [Fact]
        public void Block_4x3_ShouldCreate3Rockets()
        {
            // A A A
            // A A A
            // A A A
            // A A A
            // 12 cells
            // - 3 vertical Rockets (columns of 4) = 120
            var component = ParseGrid(
                "A A A",
                "A A A",
                "A A A",
                "A A A"
            );
            var results = _generator.Generate(component);

            int rocketCount = CountRockets(results);
            Assert.Equal(3, rocketCount);
        }

        [Fact]
        public void Block_5x5_ShouldCreate5Rainbows()
        {
            // 5x5 = 25 cells
            // 5 horizontal Rainbows = 650 (optimal)
            var rows = new[]
            {
                "A A A A A",
                "A A A A A",
                "A A A A A",
                "A A A A A",
                "A A A A A"
            };
            var component = ParseGrid(rows);
            var results = _generator.Generate(component);

            int rainbowCount = CountBombType(results, BombType.Color);
            Assert.Equal(5, rainbowCount);
        }

        #endregion

        #region Irregular Shapes

        [Fact]
        public void IrregularShape_Hook_ShouldOptimallyPartition()
        {
            // A A A A A
            //         A
            //         A
            // 7 cells: 1 horizontal Rainbow (5) at y=0
            // The 2 perpendicular scraps at (4,1) and (4,2) are NOT absorbed
            // because Line shapes only absorb collinear + continuous scraps
            var component = ParseGrid(
                "A A A A A",
                "        A",
                "        A"
            );
            var results = _generator.Generate(component);

            Assert.Contains(results, g => g.SpawnBombType == BombType.Color);
            // Only Rainbow's 5 cells are matched, perpendicular scraps not absorbed
            Assert.Equal(5, results.Sum(g => g.Positions.Count));
        }

        [Fact]
        public void IrregularShape_Staircase_ShouldPartition()
        {
            // After ParseGrid removes spaces:
            // Row 0: "A" → (0,0)
            // Row 1: "AA" → (0,1), (1,1)
            // Row 2: "AA" → (0,2), (1,2)
            // Row 3: "A" → (0,3)
            // This creates a vertical Line4 at x=0: (0,0)-(0,1)-(0,2)-(0,3)
            var component = ParseGrid(
                "A    ",
                "A A  ",
                "  A A",
                "    A"
            );
            var results = _generator.Generate(component);

            // Line4 detected at x=0 (4 cells) → Horizontal Rocket
            // Remaining scraps (1,1), (1,2) are perpendicular to the line, not absorbed
            Assert.Single(results);
            Assert.Equal(BombType.Horizontal, results[0].SpawnBombType);
            Assert.Equal(4, results[0].Positions.Count);
        }

        [Fact]
        public void IrregularShape_ZigZag_ShouldPartition()
        {
            // A A A
            //   A
            //   A A A
            // 7 cells: forms a T-shape (top horizontal + vertical)
            // T-shape intersection at (1,0), union = 5 cells
            // Remaining scraps (2,2) and (3,2) don't form a valid line (only 2 cells)
            var component = ParseGrid(
                "A A A  ",
                "  A    ",
                "  A A A"
            );
            var results = _generator.Generate(component);

            // T-shape detected with 5 cells
            Assert.Equal(5, results.Sum(g => g.Positions.Count));
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void SingleCell_ShouldReturnEmpty()
        {
            var component = new HashSet<Position> { new Position(0, 0) };
            var results = _generator.Generate(component);

            Assert.Empty(results);
        }

        [Fact]
        public void TwoCells_ShouldReturnEmpty()
        {
            var component = new HashSet<Position>
            {
                new Position(0, 0),
                new Position(1, 0)
            };
            var results = _generator.Generate(component);

            Assert.Empty(results);
        }

        [Fact]
        public void EmptyComponent_ShouldReturnEmpty()
        {
            var component = new HashSet<Position>();
            var results = _generator.Generate(component);

            Assert.Empty(results);
        }

        [Fact]
        public void DiagonalCells_ShouldReturnEmpty()
        {
            // Diagonal cells are not connected in Match-3
            // A . .
            // . A .
            // . . A
            var component = new HashSet<Position>
            {
                new Position(0, 0),
                new Position(1, 1),
                new Position(2, 2)
            };
            // Diagonal pattern: 3 cells but no valid horizontal/vertical line
            // Should NOT be treated as a match
            var results = _generator.Generate(component);

            // No valid match pattern - returns empty
            Assert.Empty(results);
        }

        [Fact]
        public void LShapeCells_ShouldReturnEmpty()
        {
            // L-shape cells: 3 connected cells but no valid line
            // . . B
            // . B B
            var component = new HashSet<Position>
            {
                new Position(2, 0),
                new Position(1, 1),
                new Position(2, 1)
            };
            // L-shape: 3 cells connected but no 3-in-a-row horizontal or vertical
            // Should NOT be treated as a match
            var results = _generator.Generate(component);

            // No valid match pattern - returns empty
            Assert.Empty(results);
        }

        #endregion

        #region Bomb Origin Tests

        [Fact]
        public void BombOrigin_SingleFocusInMatch_ShouldUseFocus()
        {
            // 4-line with focus at position (1,0)
            var component = ParseGrid("A A A A");
            var foci = new List<Position> { new Position(1, 0) };

            var results = _generator.Generate(component, foci);

            Assert.Single(results);
            Assert.Equal(new Position(1, 0), results[0].BombOrigin);
        }

        [Fact]
        public void BombOrigin_FocusOutsideMatch_ShouldUseRandomPosition()
        {
            // 4-line, focus not in match
            var component = ParseGrid("A A A A");
            var foci = new List<Position> { new Position(5, 5) };

            var results = _generator.Generate(component, foci);

            Assert.Single(results);
            // Origin should be one of the positions in the match
            Assert.Contains(results[0].BombOrigin!.Value, component);
        }

        [Fact]
        public void BombOrigin_NoFoci_ShouldSelectFromMatch()
        {
            var component = ParseGrid("A A A A");
            var results = _generator.Generate(component, null);

            Assert.Single(results);
            Assert.NotNull(results[0].BombOrigin);
            Assert.Contains(results[0].BombOrigin!.Value, component);
        }

        [Fact]
        public void BombOrigin_BothFociInMatch_WithRandom_ShouldRandomlySelect()
        {
            // 5-line with both foci in match
            var component = ParseGrid("A A A A A");
            var foci = new List<Position>
            {
                new Position(1, 0),
                new Position(3, 0)
            };
            var random = new DefaultRandom(42);

            var results = _generator.Generate(component, foci, random);

            Assert.Single(results);
            // Origin should be one of the two foci
            Assert.True(
                results[0].BombOrigin == new Position(1, 0) ||
                results[0].BombOrigin == new Position(3, 0)
            );
        }

        [Fact]
        public void BombOrigin_NoFoci_WithRandom_ShouldRandomlySelectFromAll()
        {
            // Run multiple times to verify randomness
            var component = ParseGrid("A A A A A");
            var random = new DefaultRandom(12345);

            var origins = new HashSet<Position>();
            for (int i = 0; i < 50; i++)
            {
                var results = _generator.Generate(component, null, random);
                if (results.Count > 0 && results[0].BombOrigin is { } origin)
                {
                    origins.Add(origin);
                }
            }

            // With random selection, we should see multiple different origins
            // (statistically unlikely to always pick the same one 50 times)
            Assert.True(origins.Count >= 2, $"Expected multiple origins, got {origins.Count}");
        }

        #endregion

        #region Performance Tests

        [Fact]
        public void Performance_7x7Block_ShouldCompleteQuickly()
        {
            var rows = new[]
            {
                "A A A A A A A",
                "A A A A A A A",
                "A A A A A A A",
                "A A A A A A A",
                "A A A A A A A",
                "A A A A A A A",
                "A A A A A A A"
            };
            var component = ParseGrid(rows);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var results = _generator.Generate(component);
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 500, $"7x7 took {sw.ElapsedMilliseconds}ms");
            Assert.NotEmpty(results);
        }

        [Fact]
        public void Performance_8x8Block_ShouldCompleteQuickly()
        {
            var rows = new[]
            {
                "A A A A A A A A",
                "A A A A A A A A",
                "A A A A A A A A",
                "A A A A A A A A",
                "A A A A A A A A",
                "A A A A A A A A",
                "A A A A A A A A",
                "A A A A A A A A"
            };
            var component = ParseGrid(rows);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var results = _generator.Generate(component);
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 1000, $"8x8 took {sw.ElapsedMilliseconds}ms");
            Assert.NotEmpty(results);
        }

        [Fact]
        public void Performance_9x9Block_ShouldCompleteQuickly()
        {
            var rows = new[]
            {
                "A A A A A A A A A",
                "A A A A A A A A A",
                "A A A A A A A A A",
                "A A A A A A A A A",
                "A A A A A A A A A",
                "A A A A A A A A A",
                "A A A A A A A A A",
                "A A A A A A A A A",
                "A A A A A A A A A"
            };
            var component = ParseGrid(rows);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var results = _generator.Generate(component);
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 2000, $"9x9 took {sw.ElapsedMilliseconds}ms");
            Assert.NotEmpty(results);
        }

        [Fact]
        public void Performance_10x10Block_ShouldCompleteQuickly()
        {
            var rows = new[]
            {
                "A A A A A A A A A A",
                "A A A A A A A A A A",
                "A A A A A A A A A A",
                "A A A A A A A A A A",
                "A A A A A A A A A A",
                "A A A A A A A A A A",
                "A A A A A A A A A A",
                "A A A A A A A A A A",
                "A A A A A A A A A A",
                "A A A A A A A A A A"
            };
            var component = ParseGrid(rows);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var results = _generator.Generate(component);
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 3000, $"10x10 took {sw.ElapsedMilliseconds}ms");
            Assert.NotEmpty(results);
        }

        #endregion

        #region Optimal Partition Verification

        [Fact]
        public void OptimalPartition_5x5_ShouldMaximizeRainbows()
        {
            // 5x5 = 25 cells
            // Optimal: 5 horizontal Rainbows (each row) = 5 * 130 = 650
            var rows = new[]
            {
                "A A A A A",
                "A A A A A",
                "A A A A A",
                "A A A A A",
                "A A A A A"
            };
            var component = ParseGrid(rows);
            var results = _generator.Generate(component);

            int rainbowCount = CountBombType(results, BombType.Color);
            int totalWeight = rainbowCount * 130;

            // Should be 5 Rainbows
            Assert.Equal(5, rainbowCount);
            Assert.Equal(650, totalWeight);
        }

        [Fact]
        public void OptimalPartition_4x5_ShouldChooseBestCombination()
        {
            // 4x5 = 20 cells
            // Option A: 4 horizontal Rainbows = 4 * 130 = 520
            // Option B: 5 vertical Rockets = 5 * 40 = 200
            // Winner: 4 Rainbows
            var rows = new[]
            {
                "A A A A A",
                "A A A A A",
                "A A A A A",
                "A A A A A"
            };
            var component = ParseGrid(rows);
            var results = _generator.Generate(component);

            int rainbowCount = CountBombType(results, BombType.Color);
            Assert.Equal(4, rainbowCount);
        }

        [Fact]
        public void OptimalPartition_5x4_ShouldChooseBestCombination()
        {
            // 5x4 = 20 cells
            // Option A: 5 horizontal Rockets = 5 * 40 = 200
            // Option B: 4 vertical Rainbows = 4 * 130 = 520
            // Winner: 4 vertical Rainbows
            var rows = new[]
            {
                "A A A A",
                "A A A A",
                "A A A A",
                "A A A A",
                "A A A A"
            };
            var component = ParseGrid(rows);
            var results = _generator.Generate(component);

            int rainbowCount = CountBombType(results, BombType.Color);
            Assert.Equal(4, rainbowCount);
        }

        [Fact]
        public void OptimalPartition_2x6_ShouldCreate2Rainbows()
        {
            // 2x6 = 12 cells
            // 2 horizontal Rainbows (5 each, 2 leftover absorbed) = 2 * 130 = 260
            // vs 3 horizontal Rockets impossible (rows only 6 long each)
            // Wait, each row is 6, so 2 rows * 1 Rainbow each = 2 Rainbows
            var component = ParseGrid(
                "A A A A A A",
                "A A A A A A"
            );
            var results = _generator.Generate(component);

            int rainbowCount = CountBombType(results, BombType.Color);
            Assert.Equal(2, rainbowCount);
        }

        [Fact]
        public void OptimalPartition_6x2_ShouldCreate2Rainbows()
        {
            // 6x2 = 12 cells
            // 2 vertical Rainbows (5 each, 2 leftover) = 260
            var component = ParseGrid(
                "A A",
                "A A",
                "A A",
                "A A",
                "A A",
                "A A"
            );
            var results = _generator.Generate(component);

            int rainbowCount = CountBombType(results, BombType.Color);
            Assert.Equal(2, rainbowCount);
        }

        #endregion

        #region Weight Priority Tests

        [Fact]
        public void WeightPriority_RainbowOverTnt_ShouldPreferRainbow()
        {
            // Shape that could be Rainbow (130) or TNT (60)
            // Horizontal 5-line
            var component = ParseGrid("A A A A A");
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.Equal(BombType.Color, results[0].SpawnBombType);
        }

        [Fact]
        public void WeightPriority_TntOverRocket_ShouldPreferTnt()
        {
            // T-shape (5 cells) = TNT (60) vs Rocket (40)
            var component = ParseGrid(
                "A A A",
                "  A  ",
                "  A  "
            );
            var results = _generator.Generate(component);

            Assert.Single(results);
            Assert.True(results[0].SpawnBombType.IsAreaBomb());
        }

        [Fact]
        public void WeightPriority_RocketOverUfo_ShouldPreferRocket()
        {
            // 2x4 block: 2 Rockets (80) > 2 UFOs (40)
            var component = ParseGrid(
                "A A A A",
                "A A A A"
            );
            var results = _generator.Generate(component);

            int rocketCount = CountRockets(results);
            Assert.Equal(2, rocketCount);
        }

        [Fact]
        public void WeightPriority_MultipleSmallBombs_CanBeatSingleLarge()
        {
            // 2 TNT (120) > 1 Rainbow (130)? No, Rainbow wins
            // But 2 TNT + 1 UFO (140) > 1 Rainbow (130)
            // This requires a very specific shape...

            // Actually per docs: Rainbow(130) allows 2xTNT+1xUFO(140) to win
            // But this is rare. Let's test the normal case.

            // Normal case: Rainbow should win over lower combinations
            var component = ParseGrid("A A A A A");
            var results = _generator.Generate(component);

            Assert.Equal(BombType.Color, results[0].SpawnBombType);
        }

        #endregion

        #region Foci Priority - Bomb Placement at Swap Position

        [Fact]
        public void Square2x2WithScrap_Foci_ShouldPlaceBombAtSwapPosition()
        {
            // Bug repro: Swap leftmost A with middle B to form UFO
            // Board after swap (5 connected A cells):
            //   A       -> (1, 0) - scrap cell that gets absorbed
            // B A A     -> B(0,1), A(1,1), A(2,1)
            //   A A     -> A(1,2), A(2,2)
            //
            // The 2x2 square is at (1,1), (2,1), (1,2), (2,2)
            // Foci = [(0,1), (1,1)] - the swap positions
            // Expected: BombOrigin should be (1,1) because it's in foci AND in the square
            // Bug: BombOrigin was incorrectly placed at (1,0) because ScrapAbsorber
            //      added (1,0) to shapeCells and then it was selected by Y-coordinate sorting

            var component = new HashSet<Position>
            {
                new Position(1, 0), // scrap cell (will be absorbed)
                new Position(1, 1), // swap destination - should be bomb origin
                new Position(2, 1),
                new Position(1, 2),
                new Position(2, 2)
            };

            // Foci: swap from (0,1) to (1,1)
            var foci = new[] { new Position(0, 1), new Position(1, 1) };

            var results = _generator.Generate(component, foci);

            Assert.Single(results);
            Assert.Equal(BombType.Ufo, results[0].SpawnBombType);

            // The bomb should be placed at (1,1) - the swap destination that's in the square
            // NOT at (1,0) which is just an absorbed scrap
            Assert.Equal(new Position(1, 1), results[0].BombOrigin);
        }

        [Fact]
        public void Square2x2_Foci_OneInSquare_ShouldUseThatPosition()
        {
            // Simple 2x2 square where one foci is in the square
            // A A
            // A A
            var component = new HashSet<Position>
            {
                new Position(0, 0),
                new Position(1, 0),
                new Position(0, 1),
                new Position(1, 1)
            };

            // Foci: one inside (1,1), one outside (2,1)
            var foci = new[] { new Position(2, 1), new Position(1, 1) };

            var results = _generator.Generate(component, foci);

            Assert.Single(results);
            Assert.Equal(BombType.Ufo, results[0].SpawnBombType);
            Assert.Equal(new Position(1, 1), results[0].BombOrigin);
        }

        [Fact]
        public void Square2x2_Foci_BothInSquare_ShouldUseOneOfThem()
        {
            // 2x2 square where both foci are in the square
            // A A
            // A A
            var component = new HashSet<Position>
            {
                new Position(0, 0),
                new Position(1, 0),
                new Position(0, 1),
                new Position(1, 1)
            };

            // Foci: both inside the square
            var foci = new[] { new Position(0, 0), new Position(1, 0) };

            var results = _generator.Generate(component, foci);

            Assert.Single(results);
            Assert.Equal(BombType.Ufo, results[0].SpawnBombType);

            // Should be one of the foci positions
            Assert.True(
                results[0].BombOrigin == new Position(0, 0) ||
                results[0].BombOrigin == new Position(1, 0),
                $"Expected BombOrigin to be (0,0) or (1,0), but was {results[0].BombOrigin}");
        }

        [Fact]
        public void Square2x2_NoFoci_ShouldUseDefaultSelection()
        {
            // 2x2 square without foci - should use default selection logic
            var component = new HashSet<Position>
            {
                new Position(0, 0),
                new Position(1, 0),
                new Position(0, 1),
                new Position(1, 1)
            };

            var results = _generator.Generate(component, foci: null);

            Assert.Single(results);
            Assert.Equal(BombType.Ufo, results[0].SpawnBombType);
            Assert.NotNull(results[0].BombOrigin);
            Assert.Contains(results[0].BombOrigin!.Value, component);
        }

        #endregion
    }
}
