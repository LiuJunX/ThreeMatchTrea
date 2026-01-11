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
            original.Grid[0] = TileType.Red;
            // (1,1) = Blue (Index 3)
            original.Grid[3] = TileType.Blue;

            var result = manipulator.ResizeGrid(original, 3, 3);

            Assert.Equal(3, result.Width);
            Assert.Equal(3, result.Height);
            
            // Check (0,0)
            Assert.Equal(TileType.Red, result.Grid[0]);
            
            // Check (1,1) -> Index 1*3 + 1 = 4
            Assert.Equal(TileType.Blue, result.Grid[4]);
            
            // Check new area is None
            Assert.Equal(TileType.None, result.Grid[2]); // (2,0)
        }

        [Fact]
        public void ResizeGrid_ShouldCropData_WhenShrinking()
        {
            var manipulator = new GridManipulator();
            var original = new LevelConfig(3, 3);
            original.Grid[0] = TileType.Red;
            original.Grid[4] = TileType.Blue; // (1,1)
            original.Grid[8] = TileType.Green; // (2,2)

            var result = manipulator.ResizeGrid(original, 2, 2);

            Assert.Equal(2, result.Width);
            Assert.Equal(2, result.Height);
            
            // (0,0) should be Red
            Assert.Equal(TileType.Red, result.Grid[0]);
            
            // (1,1) should be Blue -> Index 1*2 + 1 = 3
            Assert.Equal(TileType.Blue, result.Grid[3]);
            
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
            manipulator.PaintTile(config, 0, TileType.Green, BombType.None);
            Assert.Equal(TileType.Green, config.Grid[0]);
            Assert.Equal(BombType.None, config.Bombs[0]);

            // Paint Bomb
            manipulator.PaintTile(config, 0, TileType.Red, BombType.Row);
            Assert.Equal(TileType.Red, config.Grid[0]);
            Assert.Equal(BombType.Row, config.Bombs[0]);

            // Paint Rainbow
            manipulator.PaintTile(config, 1, TileType.Rainbow, BombType.Color);
            Assert.Equal(TileType.Rainbow, config.Grid[1]);
            Assert.Equal(BombType.Color, config.Bombs[1]);
        }
    }
}
