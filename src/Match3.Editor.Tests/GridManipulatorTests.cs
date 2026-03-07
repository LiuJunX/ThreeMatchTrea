using Xunit;
using Match3.Editor.Logic;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using System.Linq;

namespace Match3.Editor.Tests
{
    public class GridManipulatorTests
    {
        [Fact]
        public void ResizeGrid_ShouldPreserveData_WhenExpanding()
        {
            var manipulator = new GridManipulator();
            var original = new LevelConfig(2, 2);
            // (0,0) = Red
            original.Grid[0] = ElementType.Item1;
            // (1,1) = Blue (Index 3)
            original.Grid[3] = ElementType.Item3;

            var result = manipulator.ResizeGrid(original, 3, 3);

            Assert.Equal(3, result.Width);
            Assert.Equal(3, result.Height);

            // Check (0,0)
            Assert.Equal(ElementType.Item1, result.Grid[0]);

            // Check (1,1) -> Index 1*3 + 1 = 4
            Assert.Equal(ElementType.Item3, result.Grid[4]);

            // Check new area is None
            Assert.Equal(ElementType.None, result.Grid[2]); // (2,0)
        }

        [Fact]
        public void ResizeGrid_ShouldCropData_WhenShrinking()
        {
            var manipulator = new GridManipulator();
            var original = new LevelConfig(3, 3);
            original.Grid[0] = ElementType.Item1;
            original.Grid[4] = ElementType.Item3; // (1,1)
            original.Grid[8] = ElementType.Item2; // (2,2)

            var result = manipulator.ResizeGrid(original, 2, 2);

            Assert.Equal(2, result.Width);
            Assert.Equal(2, result.Height);

            // (0,0) should be Red
            Assert.Equal(ElementType.Item1, result.Grid[0]);

            // (1,1) should be Blue -> Index 1*2 + 1 = 3
            Assert.Equal(ElementType.Item3, result.Grid[3]);

            // (2,2) is lost
        }

        [Fact]
        public void GenerateRandomLevel_ShouldBeDeterministic_WithSameSeed()
        {
            var manipulator = new GridManipulator();
            var config1 = new LevelConfig(5, 5);
            var config2 = new LevelConfig(5, 5);
            int seed = 12345;

            manipulator.GenerateRandomLevel(config1, seed);
            manipulator.GenerateRandomLevel(config2, seed);

            Assert.True(config1.Grid.SequenceEqual(config2.Grid));
        }

        [Fact]
        public void PaintTile_ShouldUpdateGridAndBombs()
        {
            var manipulator = new GridManipulator();
            var config = new LevelConfig(3, 3);

            // Paint Color
            manipulator.PaintTile(config, 0, ElementType.Item2, BombType.None);
            Assert.Equal(ElementType.Item2, config.Grid[0]);
            Assert.Equal(BombType.None, config.Bombs[0]);

            // Paint Bomb
            manipulator.PaintTile(config, 0, ElementType.Item1, BombType.Horizontal);
            Assert.Equal(ElementType.Item1, config.Grid[0]);
            Assert.Equal(BombType.Horizontal, config.Bombs[0]);

            // Paint Rainbow
            manipulator.PaintTile(config, 1, ElementType.Universal, BombType.Color);
            Assert.Equal(ElementType.Universal, config.Grid[1]);
            Assert.Equal(BombType.Color, config.Bombs[1]);
        }

        [Fact]
        public void PaintCover_ShouldSetTypeAndHealth()
        {
            var manipulator = new GridManipulator();
            var config = new LevelConfig(3, 3);

            manipulator.PaintCover(config, 0, CoverType.Cage);

            Assert.Equal(CoverType.Cage, config.Covers[0]);
            Assert.True(config.CoverHealths[0] > 0);
        }

        [Fact]
        public void ClearCover_ShouldResetTypeAndHealth()
        {
            var manipulator = new GridManipulator();
            var config = new LevelConfig(3, 3);

            // 先放置笼子
            manipulator.PaintCover(config, 0, CoverType.Cage);
            Assert.Equal(CoverType.Cage, config.Covers[0]);
            Assert.True(config.CoverHealths[0] > 0);

            // 清除
            manipulator.ClearCover(config, 0);

            Assert.Equal(CoverType.None, config.Covers[0]);
            Assert.Equal(0, config.CoverHealths[0]);
        }

        [Fact]
        public void PaintGround_ShouldSetTypeAndHealth()
        {
            var manipulator = new GridManipulator();
            var config = new LevelConfig(3, 3);

            manipulator.PaintGround(config, 0, GroundType.Ice);

            Assert.Equal(GroundType.Ice, config.Grounds[0]);
            Assert.True(config.GroundHealths[0] > 0);
        }

        [Fact]
        public void ClearGround_ShouldResetTypeAndHealth()
        {
            var manipulator = new GridManipulator();
            var config = new LevelConfig(3, 3);

            // 先放置冰块
            manipulator.PaintGround(config, 0, GroundType.Ice);
            Assert.Equal(GroundType.Ice, config.Grounds[0]);
            Assert.True(config.GroundHealths[0] > 0);

            // 清除
            manipulator.ClearGround(config, 0);

            Assert.Equal(GroundType.None, config.Grounds[0]);
            Assert.Equal(0, config.GroundHealths[0]);
        }
    }
}
