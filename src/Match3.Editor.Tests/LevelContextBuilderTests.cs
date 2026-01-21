using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Editor.Logic;

namespace Match3.Editor.Tests
{
    /// <summary>
    /// Tests for LevelContextBuilder which constructs AI context from LevelConfig.
    /// </summary>
    public class LevelContextBuilderTests
    {
        [Fact]
        public void Build_WithBasicConfig_ReturnsCorrectDimensions()
        {
            var config = new LevelConfig(8, 10);
            config.MoveLimit = 25;

            var context = LevelContextBuilder.Build(config);

            Assert.Equal(8, context.Width);
            Assert.Equal(10, context.Height);
            Assert.Equal(25, context.MoveLimit);
        }

        [Fact]
        public void Build_WithObjectives_IncludesObjectivesInContext()
        {
            var config = new LevelConfig(8, 8);
            config.Objectives = new[]
            {
                new LevelObjective
                {
                    TargetLayer = ObjectiveTargetLayer.Tile,
                    ElementType = (int)TileType.Red,
                    TargetCount = 30
                }
            };

            var context = LevelContextBuilder.Build(config);

            Assert.NotNull(context.Objectives);
            // LevelConfig always has 4 objectives (fixed size array)
            Assert.Equal(4, context.Objectives.Length);
            // First objective should be set
            Assert.Equal(ObjectiveTargetLayer.Tile, context.Objectives[0].TargetLayer);
            Assert.Equal((int)TileType.Red, context.Objectives[0].ElementType);
            Assert.Equal(30, context.Objectives[0].TargetCount);
        }

        [Fact]
        public void Build_WithWinRate_IncludesWinRateInContext()
        {
            var config = new LevelConfig(8, 8);

            var context = LevelContextBuilder.Build(config, winRate: 0.75f);

            Assert.Equal(0.75f, context.WinRate);
        }

        [Fact]
        public void Build_WithDifficultyText_IncludesDifficultyInContext()
        {
            var config = new LevelConfig(8, 8);

            var context = LevelContextBuilder.Build(config, difficultyText: "Medium (65%)");

            Assert.Equal("Medium (65%)", context.DifficultyText);
        }

        [Fact]
        public void Build_WithTilesInGrid_GeneratesGridSummary()
        {
            var config = new LevelConfig(3, 3);
            // Set some tiles
            config.Grid[0] = TileType.Red;
            config.Grid[1] = TileType.Red;
            config.Grid[2] = TileType.Blue;
            config.Grid[3] = TileType.Blue;
            config.Grid[4] = TileType.Blue;

            var context = LevelContextBuilder.Build(config);

            Assert.NotNull(context.GridSummary);
            Assert.Contains("Red=2", context.GridSummary);
            Assert.Contains("Blue=3", context.GridSummary);
        }

        [Fact]
        public void Build_WithBombsInGrid_IncludesBombsInSummary()
        {
            var config = new LevelConfig(3, 3);
            config.Grid[0] = TileType.Red;
            config.Bombs[0] = BombType.Horizontal;
            config.Grid[1] = TileType.Blue;
            config.Bombs[1] = BombType.Vertical;

            var context = LevelContextBuilder.Build(config);

            Assert.NotNull(context.GridSummary);
            Assert.Contains("Bombs:", context.GridSummary);
            Assert.Contains("Horizontal=1", context.GridSummary);
            Assert.Contains("Vertical=1", context.GridSummary);
        }

        [Fact]
        public void Build_WithCoversInGrid_IncludesCoversInSummary()
        {
            var config = new LevelConfig(3, 3);
            config.Covers[0] = CoverType.Cage;
            config.Covers[1] = CoverType.Cage;
            config.Covers[2] = CoverType.Chain;

            var context = LevelContextBuilder.Build(config);

            Assert.NotNull(context.GridSummary);
            Assert.Contains("Covers:", context.GridSummary);
            Assert.Contains("Cage=2", context.GridSummary);
            Assert.Contains("Chain=1", context.GridSummary);
        }

        [Fact]
        public void Build_WithGroundsInGrid_IncludesGroundsInSummary()
        {
            var config = new LevelConfig(3, 3);
            config.Grounds[0] = GroundType.Ice;
            config.Grounds[1] = GroundType.Ice;
            config.Grounds[2] = GroundType.Ice;

            var context = LevelContextBuilder.Build(config);

            Assert.NotNull(context.GridSummary);
            Assert.Contains("Grounds:", context.GridSummary);
            Assert.Contains("Ice=3", context.GridSummary);
        }

        [Fact]
        public void Build_WithEmptyGrid_ReturnsEmptySummary()
        {
            var config = new LevelConfig(3, 3);
            // Grid is all TileType.None by default

            var context = LevelContextBuilder.Build(config);

            // Summary should be empty or whitespace when no elements
            Assert.True(string.IsNullOrWhiteSpace(context.GridSummary) || context.GridSummary == "");
        }

        [Fact]
        public void Build_WithNullOptionalParams_ReturnsNullValues()
        {
            var config = new LevelConfig(8, 8);

            var context = LevelContextBuilder.Build(config);

            Assert.Null(context.WinRate);
            Assert.Null(context.DifficultyText);
        }

        [Fact]
        public void Build_WithMultipleObjectives_IncludesAllObjectives()
        {
            var config = new LevelConfig(8, 8);
            config.Objectives = new[]
            {
                new LevelObjective
                {
                    TargetLayer = ObjectiveTargetLayer.Tile,
                    ElementType = (int)TileType.Red,
                    TargetCount = 20
                },
                new LevelObjective
                {
                    TargetLayer = ObjectiveTargetLayer.Cover,
                    ElementType = (int)CoverType.Cage,
                    TargetCount = 10
                },
                new LevelObjective
                {
                    TargetLayer = ObjectiveTargetLayer.Ground,
                    ElementType = (int)GroundType.Ice,
                    TargetCount = 15
                }
            };

            var context = LevelContextBuilder.Build(config);

            Assert.NotNull(context.Objectives);
            // LevelConfig always has 4 objectives (fixed size array)
            Assert.Equal(4, context.Objectives.Length);
            // First 3 should be set
            Assert.Equal(ObjectiveTargetLayer.Tile, context.Objectives[0].TargetLayer);
            Assert.Equal(ObjectiveTargetLayer.Cover, context.Objectives[1].TargetLayer);
            Assert.Equal(ObjectiveTargetLayer.Ground, context.Objectives[2].TargetLayer);
        }

        [Fact]
        public void Build_ReturnsNewContextInstance()
        {
            var config = new LevelConfig(8, 8);

            var context1 = LevelContextBuilder.Build(config);
            var context2 = LevelContextBuilder.Build(config);

            Assert.NotSame(context1, context2);
        }
    }
}
