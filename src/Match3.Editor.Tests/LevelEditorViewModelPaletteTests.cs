using Match3.Core.Models.Enums;
using Match3.Editor.ViewModels;

namespace Match3.Editor.Tests
{
    public class LevelEditorViewModelPaletteTests
    {
        [Fact]
        public void TilePaletteTypes_ShouldContainExpectedColorsAndNone()
        {
            var types = LevelEditorViewModel.TilePaletteTypes;

            Assert.Contains(TileType.Red, types);
            Assert.Contains(TileType.Green, types);
            Assert.Contains(TileType.Blue, types);
            Assert.Contains(TileType.Yellow, types);
            Assert.Contains(TileType.Purple, types);
            Assert.Contains(TileType.Orange, types);
            Assert.Contains(TileType.Rainbow, types);
            Assert.Contains(TileType.None, types);
            Assert.Equal(8, types.Count);
        }

        [Fact]
        public void GetTileBackground_ShouldReturnSolidColorOrGradient()
        {
            var red = LevelEditorViewModel.GetTileBackground(TileType.Red);
            var none = LevelEditorViewModel.GetTileBackground(TileType.None);
            var rainbow = LevelEditorViewModel.GetTileBackground(TileType.Rainbow);

            Assert.Equal("#dc3545", red);
            Assert.Equal("#f8f9fa", none);
            Assert.Contains("linear-gradient", rainbow);
        }

        [Fact]
        public void GetTileCheckmarkClass_ShouldUseDarkTextOnLightBackgrounds()
        {
            var noneClass = LevelEditorViewModel.GetTileCheckmarkClass(TileType.None);
            var yellowClass = LevelEditorViewModel.GetTileCheckmarkClass(TileType.Yellow);
            var redClass = LevelEditorViewModel.GetTileCheckmarkClass(TileType.Red);

            Assert.Equal("text-dark", noneClass);
            Assert.Equal("text-dark", yellowClass);
            Assert.Equal("text-white", redClass);
        }
    }
}

