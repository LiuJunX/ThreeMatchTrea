using Match3.Core.Models.Enums;
using Match3.Editor.ViewModels;
using Match3.Editor.Helpers;

namespace Match3.Editor.Tests
{
    public class LevelEditorViewModelPaletteTests
    {
        [Fact]
        public void TilePaletteTypes_ShouldContainExpectedColorsAndNone()
        {
            var types = LevelEditorViewModel.TilePaletteTypes;

            Assert.Contains(ElementType.Item1, types);
            Assert.Contains(ElementType.Item2, types);
            Assert.Contains(ElementType.Item3, types);
            Assert.Contains(ElementType.Item4, types);
            Assert.Contains(ElementType.Item5, types);
            Assert.Contains(ElementType.Item6, types);
            Assert.Contains(ElementType.Universal, types);
            Assert.Contains(ElementType.None, types);
            Assert.Equal(8, types.Count);
        }

        [Fact]
        public void GetTileBackground_ShouldReturnSolidColorOrGradient()
        {
            var red = EditorStyleHelper.GetTileColor(ElementType.Item1);
            var none = EditorStyleHelper.GetTileColor(ElementType.None);
            var rainbow = EditorStyleHelper.GetTileColor(ElementType.Universal);

            Assert.Equal("#dc3545", red);
            Assert.Equal("#f8f9fa", none);
            Assert.Contains("linear-gradient", rainbow);
        }

        [Fact]
        public void GetTileCheckmarkClass_ShouldUseDarkTextOnLightBackgrounds()
        {
            var noneClass = EditorStyleHelper.GetTileCheckmarkClass(ElementType.None);
            var yellowClass = EditorStyleHelper.GetTileCheckmarkClass(ElementType.Item4);
            var redClass = EditorStyleHelper.GetTileCheckmarkClass(ElementType.Item1);

            Assert.Equal("text-dark", noneClass);
            Assert.Equal("text-dark", yellowClass);
            Assert.Equal("text-white", redClass);
        }
    }
}
