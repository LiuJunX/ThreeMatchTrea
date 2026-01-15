using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Matching.Generation.Rules;
using Xunit;

namespace Match3.Core.Tests.Systems.Matching;

/// <summary>
/// Tests for ShapeRule implementations: LineRule, SquareRule, IntersectionRule.
/// </summary>
public class ShapeRuleTests
{
    #region Helper Methods

    private static HashSet<Position> CreateLine(int startX, int y, int length)
    {
        var positions = new HashSet<Position>();
        for (int i = 0; i < length; i++)
        {
            positions.Add(new Position(startX + i, y));
        }
        return positions;
    }

    private static HashSet<Position> CreateVerticalLine(int x, int startY, int length)
    {
        var positions = new HashSet<Position>();
        for (int i = 0; i < length; i++)
        {
            positions.Add(new Position(x, startY + i));
        }
        return positions;
    }

    private static HashSet<Position> CreateSquare2x2(int x, int y)
    {
        return new HashSet<Position>
        {
            new Position(x, y), new Position(x + 1, y),
            new Position(x, y + 1), new Position(x + 1, y + 1)
        };
    }

    private static ShapeFeature CreateFeature(
        HashSet<Position> component,
        List<HashSet<Position>>? hLines = null,
        List<HashSet<Position>>? vLines = null)
    {
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;

        foreach (var p in component)
        {
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }

        return new ShapeFeature
        {
            MinX = minX,
            MaxX = maxX,
            MinY = minY,
            MaxY = maxY,
            HLines = hLines ?? new List<HashSet<Position>>(),
            VLines = vLines ?? new List<HashSet<Position>>()
        };
    }

    #endregion

    #region LineRule Tests

    public class LineRuleTests
    {
        private readonly LineRule _rule = new();

        [Fact]
        public void Detect_Line3_ShouldNotDetect()
        {
            var line = CreateLine(0, 0, 3);
            var feature = CreateFeature(line, new List<HashSet<Position>> { line });
            var results = new List<DetectedShape>();

            _rule.Detect(line, feature, results);

            Assert.Empty(results);
        }

        [Fact]
        public void Detect_HorizontalLine4_ShouldDetectRocket()
        {
            var line = CreateLine(0, 0, 4);
            var feature = CreateFeature(line, new List<HashSet<Position>> { line });
            var results = new List<DetectedShape>();

            _rule.Detect(line, feature, results);

            Assert.Single(results);
            Assert.Equal(BombType.Vertical, results[0].Type); // Horizontal match -> Vertical rocket
            Assert.Equal(MatchShape.Line4Horizontal, results[0].Shape);
            Assert.Equal(4, results[0].Cells!.Count);
        }

        [Fact]
        public void Detect_VerticalLine4_ShouldDetectRocket()
        {
            var line = CreateVerticalLine(0, 0, 4);
            var feature = CreateFeature(line, vLines: new List<HashSet<Position>> { line });
            var results = new List<DetectedShape>();

            _rule.Detect(line, feature, results);

            Assert.Single(results);
            Assert.Equal(BombType.Horizontal, results[0].Type); // Vertical match -> Horizontal rocket
            Assert.Equal(MatchShape.Line4Vertical, results[0].Shape);
            Assert.Equal(4, results[0].Cells!.Count);
        }

        [Fact]
        public void Detect_HorizontalLine5_ShouldDetectRainbow()
        {
            var line = CreateLine(0, 0, 5);
            var feature = CreateFeature(line, new List<HashSet<Position>> { line });
            var results = new List<DetectedShape>();

            _rule.Detect(line, feature, results);

            // Should have Rainbow candidates
            Assert.Contains(results, r => r.Type == BombType.Color && r.Shape == MatchShape.Line5);
        }

        [Fact]
        public void Detect_VerticalLine5_ShouldDetectRainbow()
        {
            var line = CreateVerticalLine(0, 0, 5);
            var feature = CreateFeature(line, vLines: new List<HashSet<Position>> { line });
            var results = new List<DetectedShape>();

            _rule.Detect(line, feature, results);

            Assert.Contains(results, r => r.Type == BombType.Color && r.Shape == MatchShape.Line5);
        }

        [Fact]
        public void Detect_Line6_ShouldGenerateSlidingWindowCandidates()
        {
            var line = CreateLine(0, 0, 6);
            var feature = CreateFeature(line, new List<HashSet<Position>> { line });
            var results = new List<DetectedShape>();

            _rule.Detect(line, feature, results);

            // Line of 6 should generate:
            // - 2 Rainbow candidates (sliding window: positions 0-4, 1-5)
            // - 2 Rocket candidates (edge only: positions 0-3, 2-5)
            var rainbowCount = 0;
            var rocketCount = 0;
            foreach (var r in results)
            {
                if (r.Type == BombType.Color) rainbowCount++;
                else if (r.Type == BombType.Vertical) rocketCount++;
            }

            Assert.Equal(2, rainbowCount);
            Assert.Equal(2, rocketCount); // Only edge rockets
        }

        [Fact]
        public void Detect_RocketWeight_ShouldBe40()
        {
            var line = CreateLine(0, 0, 4);
            var feature = CreateFeature(line, new List<HashSet<Position>> { line });
            var results = new List<DetectedShape>();

            _rule.Detect(line, feature, results);

            Assert.Single(results);
            Assert.Equal(40, results[0].Weight);
        }

        [Fact]
        public void Detect_RainbowWeight_ShouldBe130()
        {
            var line = CreateLine(0, 0, 5);
            var feature = CreateFeature(line, new List<HashSet<Position>> { line });
            var results = new List<DetectedShape>();

            _rule.Detect(line, feature, results);

            var rainbow = results.Find(r => r.Type == BombType.Color);
            Assert.NotNull(rainbow);
            Assert.Equal(130, rainbow.Weight);
        }
    }

    #endregion

    #region SquareRule Tests

    public class SquareRuleTests
    {
        private readonly SquareRule _rule = new();

        [Fact]
        public void Detect_2x2Square_ShouldDetectUfo()
        {
            var square = CreateSquare2x2(0, 0);
            var feature = CreateFeature(square);
            var results = new List<DetectedShape>();

            _rule.Detect(square, feature, results);

            Assert.Single(results);
            Assert.Equal(BombType.Ufo, results[0].Type);
            Assert.Equal(MatchShape.Square, results[0].Shape);
            Assert.Equal(4, results[0].Cells!.Count);
        }

        [Fact]
        public void Detect_UfoWeight_ShouldBe20()
        {
            var square = CreateSquare2x2(0, 0);
            var feature = CreateFeature(square);
            var results = new List<DetectedShape>();

            _rule.Detect(square, feature, results);

            Assert.Single(results);
            Assert.Equal(20, results[0].Weight);
        }

        [Fact]
        public void Detect_2x2InLine4_ShouldSkip()
        {
            // 2x4 rectangle: 2x2 squares should be skipped because both rows are part of line 4
            var component = new HashSet<Position>();
            for (int x = 0; x < 4; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    component.Add(new Position(x, y));
                }
            }

            var feature = CreateFeature(component);
            var results = new List<DetectedShape>();

            _rule.Detect(component, feature, results);

            // All 2x2 squares should be skipped since both rows form 4+ lines
            Assert.Empty(results);
        }

        [Fact]
        public void Detect_Isolated2x2_ShouldDetect()
        {
            // Pure 2x2 not part of any 4+ line
            var square = CreateSquare2x2(0, 0);
            var feature = CreateFeature(square);
            var results = new List<DetectedShape>();

            _rule.Detect(square, feature, results);

            Assert.Single(results);
            Assert.Equal(BombType.Ufo, results[0].Type);
        }

        [Fact]
        public void Detect_Multiple2x2_ShouldDetectAll()
        {
            // Two separate 2x2 squares
            var component = new HashSet<Position>();
            // First square at (0, 0)
            component.Add(new Position(0, 0));
            component.Add(new Position(1, 0));
            component.Add(new Position(0, 1));
            component.Add(new Position(1, 1));
            // Second square at (5, 0) - not adjacent
            component.Add(new Position(5, 0));
            component.Add(new Position(6, 0));
            component.Add(new Position(5, 1));
            component.Add(new Position(6, 1));

            var feature = CreateFeature(component);
            var results = new List<DetectedShape>();

            _rule.Detect(component, feature, results);

            Assert.Equal(2, results.Count);
            Assert.All(results, r => Assert.Equal(BombType.Ufo, r.Type));
        }

        [Fact]
        public void Detect_3x3Block_ShouldDetectFour2x2()
        {
            // 3x3 block contains four overlapping 2x2 squares
            var component = new HashSet<Position>();
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    component.Add(new Position(x, y));
                }
            }

            var feature = CreateFeature(component);
            var results = new List<DetectedShape>();

            _rule.Detect(component, feature, results);

            // (0,0), (1,0), (0,1), (1,1)
            Assert.Equal(4, results.Count);
        }
    }

    #endregion

    #region IntersectionRule Tests

    public class IntersectionRuleTests
    {
        private readonly IntersectionRule _rule = new();

        [Fact]
        public void Detect_TShape_ShouldDetectTNT()
        {
            // T-shape: 3 horizontal + 3 vertical with 1 overlap = 5 unique cells
            var component = new HashSet<Position>
            {
                // Horizontal: (0,1), (1,1), (2,1)
                new Position(0, 1), new Position(1, 1), new Position(2, 1),
                // Vertical: (1,0), (1,1), (1,2) - (1,1) is overlap
                new Position(1, 0), new Position(1, 2)
            };

            var hLine = new HashSet<Position>
            {
                new Position(0, 1), new Position(1, 1), new Position(2, 1)
            };
            var vLine = new HashSet<Position>
            {
                new Position(1, 0), new Position(1, 1), new Position(1, 2)
            };

            var feature = CreateFeature(
                component,
                new List<HashSet<Position>> { hLine },
                new List<HashSet<Position>> { vLine });
            var results = new List<DetectedShape>();

            _rule.Detect(component, feature, results);

            Assert.Single(results);
            Assert.Equal(BombType.Square5x5, results[0].Type);
            Assert.Equal(MatchShape.Cross, results[0].Shape);
        }

        [Fact]
        public void Detect_LShape_ShouldDetectTNT()
        {
            // L-shape: 3 horizontal + 3 vertical with corner overlap = 5 unique cells
            var hLine = new HashSet<Position>
            {
                new Position(0, 0), new Position(1, 0), new Position(2, 0)
            };
            var vLine = new HashSet<Position>
            {
                new Position(0, 0), new Position(0, 1), new Position(0, 2)
            };

            var component = new HashSet<Position>(hLine);
            foreach (var p in vLine) component.Add(p);

            var feature = CreateFeature(
                component,
                new List<HashSet<Position>> { hLine },
                new List<HashSet<Position>> { vLine });
            var results = new List<DetectedShape>();

            _rule.Detect(component, feature, results);

            Assert.Single(results);
            Assert.Equal(BombType.Square5x5, results[0].Type);
        }

        [Fact]
        public void Detect_TNTWeight_ShouldBe60()
        {
            var hLine = new HashSet<Position>
            {
                new Position(0, 1), new Position(1, 1), new Position(2, 1)
            };
            var vLine = new HashSet<Position>
            {
                new Position(1, 0), new Position(1, 1), new Position(1, 2)
            };

            var component = new HashSet<Position>(hLine);
            foreach (var p in vLine) component.Add(p);

            var feature = CreateFeature(
                component,
                new List<HashSet<Position>> { hLine },
                new List<HashSet<Position>> { vLine });
            var results = new List<DetectedShape>();

            _rule.Detect(component, feature, results);

            Assert.Single(results);
            Assert.Equal(60, results[0].Weight);
        }

        [Fact]
        public void Detect_NoIntersection_ShouldNotDetect()
        {
            // Two parallel lines that don't intersect
            var hLine = new HashSet<Position>
            {
                new Position(0, 0), new Position(1, 0), new Position(2, 0)
            };
            var vLine = new HashSet<Position>
            {
                new Position(5, 0), new Position(5, 1), new Position(5, 2)
            };

            var component = new HashSet<Position>(hLine);
            foreach (var p in vLine) component.Add(p);

            var feature = CreateFeature(
                component,
                new List<HashSet<Position>> { hLine },
                new List<HashSet<Position>> { vLine });
            var results = new List<DetectedShape>();

            _rule.Detect(component, feature, results);

            Assert.Empty(results);
        }

        [Fact]
        public void Detect_SmallIntersection_ShouldNotDetect()
        {
            // Intersection with union < 5 should not detect
            // 2 horizontal + 2 vertical with overlap = 3 unique cells
            var hLine = new HashSet<Position>
            {
                new Position(0, 0), new Position(1, 0)
            };
            var vLine = new HashSet<Position>
            {
                new Position(0, 0), new Position(0, 1)
            };

            var component = new HashSet<Position>(hLine);
            foreach (var p in vLine) component.Add(p);

            // Note: Features should have lines of 3+, but testing edge case
            var feature = CreateFeature(
                component,
                new List<HashSet<Position>> { hLine },
                new List<HashSet<Position>> { vLine });
            var results = new List<DetectedShape>();

            _rule.Detect(component, feature, results);

            // Union count = 2 + 2 - 1 = 3, which is < 5
            Assert.Empty(results);
        }

        [Fact]
        public void Detect_LargeIntersection_ShouldDetect()
        {
            // 4 horizontal + 4 vertical with overlap = 7 unique cells
            var hLine = new HashSet<Position>
            {
                new Position(0, 2), new Position(1, 2), new Position(2, 2), new Position(3, 2)
            };
            var vLine = new HashSet<Position>
            {
                new Position(2, 0), new Position(2, 1), new Position(2, 2), new Position(2, 3)
            };

            var component = new HashSet<Position>(hLine);
            foreach (var p in vLine) component.Add(p);

            var feature = CreateFeature(
                component,
                new List<HashSet<Position>> { hLine },
                new List<HashSet<Position>> { vLine });
            var results = new List<DetectedShape>();

            _rule.Detect(component, feature, results);

            Assert.Single(results);
            Assert.Equal(7, results[0].Cells!.Count);
        }
    }

    #endregion

    #region ShapeDetector Integration Tests

    public class ShapeDetectorTests
    {
        private readonly ShapeDetector _detector = new();

        [Fact]
        public void DetectAll_SimpleMatch3_ShouldReturnEmpty()
        {
            var component = CreateLine(0, 0, 3);
            var results = new List<DetectedShape>();

            _detector.DetectAll(component, results);

            // 3-match doesn't generate any special shapes
            Assert.Empty(results);
        }

        [Fact]
        public void DetectAll_Line4_ShouldDetectRocket()
        {
            var component = CreateLine(0, 0, 4);
            var results = new List<DetectedShape>();

            _detector.DetectAll(component, results);

            Assert.Contains(results, r => r.Type == BombType.Vertical);
        }

        [Fact]
        public void DetectAll_2x2Square_ShouldDetectUfo()
        {
            var component = CreateSquare2x2(0, 0);
            var results = new List<DetectedShape>();

            _detector.DetectAll(component, results);

            Assert.Contains(results, r => r.Type == BombType.Ufo);
        }

        [Fact]
        public void DetectAll_TShape_ShouldDetectTNT()
        {
            var component = new HashSet<Position>
            {
                // T-shape
                new Position(0, 1), new Position(1, 1), new Position(2, 1),
                new Position(1, 0), new Position(1, 2)
            };
            var results = new List<DetectedShape>();

            _detector.DetectAll(component, results);

            Assert.Contains(results, r => r.Type == BombType.Square5x5);
        }

        [Fact]
        public void DetectAll_NullComponent_ShouldNotThrow()
        {
            var results = new List<DetectedShape>();
            _detector.DetectAll(null!, results);
            Assert.Empty(results);
        }

        [Fact]
        public void DetectAll_EmptyComponent_ShouldNotThrow()
        {
            var results = new List<DetectedShape>();
            _detector.DetectAll(new HashSet<Position>(), results);
            Assert.Empty(results);
        }

        [Fact]
        public void DetectAll_SmallComponent_ShouldReturnEmpty()
        {
            var component = new HashSet<Position>
            {
                new Position(0, 0),
                new Position(1, 0)
            };
            var results = new List<DetectedShape>();

            _detector.DetectAll(component, results);

            Assert.Empty(results);
        }
    }

    #endregion
}
