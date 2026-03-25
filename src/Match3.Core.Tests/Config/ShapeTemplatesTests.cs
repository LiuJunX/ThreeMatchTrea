using System.Linq;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Config;

public class ShapeTemplatesTests
{
    private readonly XorShift64 _rng = new(42);

    #region Rectangle

    [Fact]
    public void Rectangle_NoVoidCells()
    {
        var cells = ShapeTemplates.Generate("rectangle", 8, 8, _rng);

        Assert.DoesNotContain(CellKind.Void, cells);
    }

    [Fact]
    public void Rectangle_HasSpawners()
    {
        var cells = ShapeTemplates.Generate("rectangle", 8, 8, _rng);

        Assert.Contains(CellKind.Spawner, cells);
    }

    #endregion

    #region Cross

    [Fact]
    public void Cross_CornersAreVoid()
    {
        var cells = ShapeTemplates.Generate("cross", 9, 9, _rng);

        // Top-left corner (0,0) should be Void
        Assert.Equal(CellKind.Void, cells[0 * 9 + 0]);
        // Center should be playable
        Assert.NotEqual(CellKind.Void, cells[4 * 9 + 4]);
    }

    [Fact]
    public void Cross_SlotCountLessThanRectangle()
    {
        var cross = ShapeTemplates.Generate("cross", 9, 9, _rng);
        var rect = ShapeTemplates.Generate("rectangle", 9, 9, _rng);

        Assert.True(ShapeTemplates.CountSlots(cross) < ShapeTemplates.CountSlots(rect));
    }

    #endregion

    #region Diamond

    [Fact]
    public void Diamond_CornersAreVoid()
    {
        var cells = ShapeTemplates.Generate("diamond", 9, 9, _rng);

        Assert.Equal(CellKind.Void, cells[0 * 9 + 0]); // top-left
        Assert.Equal(CellKind.Void, cells[0 * 9 + 8]); // top-right
    }

    [Fact]
    public void Diamond_CenterIsPlayable()
    {
        var cells = ShapeTemplates.Generate("diamond", 9, 9, _rng);

        Assert.NotEqual(CellKind.Void, cells[4 * 9 + 4]);
    }

    #endregion

    #region L-Shape

    [Fact]
    public void LShape_TopRightIsVoid()
    {
        var cells = ShapeTemplates.Generate("L-shape", 9, 9, _rng);

        // Top-right corner should be Void
        Assert.Equal(CellKind.Void, cells[0 * 9 + 8]);
        // Bottom-left should be playable
        Assert.NotEqual(CellKind.Void, cells[8 * 9 + 0]);
    }

    [Fact]
    public void LShape_BottomRowFullyPlayable()
    {
        var cells = ShapeTemplates.Generate("L-shape", 9, 9, _rng);

        // Bottom row should all be non-Void
        for (int x = 0; x < 9; x++)
            Assert.NotEqual(CellKind.Void, cells[8 * 9 + x]);
    }

    #endregion

    #region Hourglass

    [Fact]
    public void Hourglass_MiddleIsNarrower()
    {
        int w = 9, h = 9;
        var cells = ShapeTemplates.Generate("hourglass", w, h, _rng);

        // Count playable cells in top row vs middle row
        int topSlots = CountRowSlots(cells, w, 0);
        int midSlots = CountRowSlots(cells, w, h / 2);

        Assert.True(midSlots < topSlots,
            $"Middle row ({midSlots} slots) should be narrower than top row ({topSlots} slots)");
    }

    #endregion

    #region Compartment

    [Fact]
    public void Compartment_HasVoidColumn()
    {
        var cells = ShapeTemplates.Generate("compartment", 9, 9, _rng);

        // The split column should have some Void cells
        int splitX = 9 / 2;
        bool hasVoid = false;
        for (int y = 0; y < 9; y++)
            if (cells[y * 9 + splitX] == CellKind.Void) { hasVoid = true; break; }

        Assert.True(hasVoid, "Compartment should have Void cells in the split column");
    }

    [Fact]
    public void Compartment_CorridorExists()
    {
        var cells = ShapeTemplates.Generate("compartment", 9, 9, _rng);

        // The split column should have at least some non-Void cells (corridor)
        int splitX = 9 / 2;
        bool hasCorridor = false;
        for (int y = 0; y < 9; y++)
            if (cells[y * 9 + splitX] != CellKind.Void) { hasCorridor = true; break; }

        Assert.True(hasCorridor, "Compartment should have a corridor");
    }

    #endregion

    #region General Properties

    [Theory]
    [InlineData("rectangle")]
    [InlineData("cross")]
    [InlineData("diamond")]
    [InlineData("L-shape")]
    [InlineData("hourglass")]
    [InlineData("compartment")]
    public void AllShapes_HaveSpawners(string shape)
    {
        var cells = ShapeTemplates.Generate(shape, 9, 9, new XorShift64(123));

        Assert.Contains(CellKind.Spawner, cells);
    }

    [Theory]
    [InlineData("rectangle")]
    [InlineData("cross")]
    [InlineData("diamond")]
    [InlineData("L-shape")]
    [InlineData("hourglass")]
    [InlineData("compartment")]
    public void AllShapes_SpawnersOnTopOfEachColumn(string shape)
    {
        int w = 9, h = 9;
        var cells = ShapeTemplates.Generate(shape, w, h, new XorShift64(123));

        // For each column that has any non-Void cell, the topmost non-Void must be Spawner
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var cell = cells[y * w + x];
                if (cell != CellKind.Void)
                {
                    Assert.Equal(CellKind.Spawner, cell);
                    break;
                }
            }
        }
    }

    [Theory]
    [InlineData("rectangle")]
    [InlineData("cross")]
    [InlineData("diamond")]
    [InlineData("L-shape")]
    [InlineData("hourglass")]
    [InlineData("compartment")]
    public void AllShapes_ReasonableSlotCount(string shape)
    {
        int w = 9, h = 9;
        var cells = ShapeTemplates.Generate(shape, w, h, new XorShift64(123));
        int slots = ShapeTemplates.CountSlots(cells);

        // At least 30% of total area should be playable
        Assert.True(slots >= w * h * 0.3,
            $"Shape '{shape}' has only {slots} slots out of {w * h} total");
    }

    [Theory]
    [InlineData("rectangle", 7, 7)]
    [InlineData("cross", 8, 8)]
    [InlineData("diamond", 10, 10)]
    public void AllShapes_ArraySizeCorrect(string shape, int w, int h)
    {
        var cells = ShapeTemplates.Generate(shape, w, h, new XorShift64(42));

        Assert.Equal(w * h, cells.Length);
    }

    #endregion

    #region Helpers

    private static int CountRowSlots(CellKind[] cells, int w, int row)
    {
        int count = 0;
        for (int x = 0; x < w; x++)
            if (cells[row * w + x] != CellKind.Void) count++;
        return count;
    }

    #endregion
}
