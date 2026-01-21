using Xunit;
using Match3.Editor.Logic;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;

namespace Match3.Editor.Tests
{
    public class ObjectiveEditorHelperTests
    {
        [Fact]
        public void GetActiveObjectiveCount_ShouldReturnZero_WhenAllObjectivesAreNone()
        {
            var objectives = new LevelObjective[4];
            for (int i = 0; i < objectives.Length; i++)
            {
                objectives[i] = new LevelObjective { TargetLayer = ObjectiveTargetLayer.None };
            }

            var count = ObjectiveEditorHelper.GetActiveObjectiveCount(objectives);

            Assert.Equal(0, count);
        }

        [Fact]
        public void GetActiveObjectiveCount_ShouldReturnCorrectCount_WhenSomeObjectivesAreActive()
        {
            var objectives = new LevelObjective[4];
            objectives[0] = new LevelObjective { TargetLayer = ObjectiveTargetLayer.Tile };
            objectives[1] = new LevelObjective { TargetLayer = ObjectiveTargetLayer.Cover };
            objectives[2] = new LevelObjective { TargetLayer = ObjectiveTargetLayer.None };
            objectives[3] = new LevelObjective { TargetLayer = ObjectiveTargetLayer.None };

            var count = ObjectiveEditorHelper.GetActiveObjectiveCount(objectives);

            Assert.Equal(2, count);
        }

        [Fact]
        public void TryAddObjective_ShouldAddObjective_WhenSlotAvailable()
        {
            var objectives = new LevelObjective[4];
            for (int i = 0; i < objectives.Length; i++)
            {
                objectives[i] = new LevelObjective { TargetLayer = ObjectiveTargetLayer.None };
            }

            var result = ObjectiveEditorHelper.TryAddObjective(objectives);

            Assert.True(result);
            Assert.Equal(ObjectiveTargetLayer.Tile, objectives[0].TargetLayer);
            Assert.Equal((int)TileType.Red, objectives[0].ElementType);
            Assert.Equal(10, objectives[0].TargetCount);
        }

        [Fact]
        public void TryAddObjective_ShouldReturnFalse_WhenAllSlotsFilled()
        {
            var objectives = new LevelObjective[4];
            for (int i = 0; i < objectives.Length; i++)
            {
                objectives[i] = new LevelObjective { TargetLayer = ObjectiveTargetLayer.Tile };
            }

            var result = ObjectiveEditorHelper.TryAddObjective(objectives);

            Assert.False(result);
        }

        [Fact]
        public void TryRemoveObjective_ShouldRemoveObjective_WhenIndexValid()
        {
            var objectives = new LevelObjective[4];
            objectives[0] = new LevelObjective { TargetLayer = ObjectiveTargetLayer.Tile };

            var result = ObjectiveEditorHelper.TryRemoveObjective(objectives, 0);

            Assert.True(result);
            Assert.Equal(ObjectiveTargetLayer.None, objectives[0].TargetLayer);
        }

        [Fact]
        public void TryRemoveObjective_ShouldReturnFalse_WhenIndexInvalid()
        {
            var objectives = new LevelObjective[4];

            Assert.False(ObjectiveEditorHelper.TryRemoveObjective(objectives, -1));
            Assert.False(ObjectiveEditorHelper.TryRemoveObjective(objectives, 4));
        }

        [Fact]
        public void TrySetObjectiveLayer_ShouldUpdateLayerAndResetElementType()
        {
            var objectives = new LevelObjective[4];
            objectives[0] = new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)TileType.Blue
            };

            var result = ObjectiveEditorHelper.TrySetObjectiveLayer(objectives, 0, ObjectiveTargetLayer.Cover);

            Assert.True(result);
            Assert.Equal(ObjectiveTargetLayer.Cover, objectives[0].TargetLayer);
            Assert.Equal((int)CoverType.Cage, objectives[0].ElementType);
        }

        [Fact]
        public void TrySetObjectiveLayer_ShouldSetGroundDefaultElementType()
        {
            var objectives = new LevelObjective[4];
            objectives[0] = new LevelObjective { TargetLayer = ObjectiveTargetLayer.Tile };

            ObjectiveEditorHelper.TrySetObjectiveLayer(objectives, 0, ObjectiveTargetLayer.Ground);

            Assert.Equal((int)GroundType.Ice, objectives[0].ElementType);
        }

        [Fact]
        public void TrySetObjectiveElementType_ShouldUpdateElementType()
        {
            var objectives = new LevelObjective[4];
            objectives[0] = new LevelObjective
            {
                TargetLayer = ObjectiveTargetLayer.Tile,
                ElementType = (int)TileType.Red
            };

            var result = ObjectiveEditorHelper.TrySetObjectiveElementType(objectives, 0, (int)TileType.Green);

            Assert.True(result);
            Assert.Equal((int)TileType.Green, objectives[0].ElementType);
        }

        [Fact]
        public void TrySetObjectiveTargetCount_ShouldUpdateTargetCount()
        {
            var objectives = new LevelObjective[4];
            objectives[0] = new LevelObjective { TargetCount = 10 };

            var result = ObjectiveEditorHelper.TrySetObjectiveTargetCount(objectives, 0, 25);

            Assert.True(result);
            Assert.Equal(25, objectives[0].TargetCount);
        }

        [Fact]
        public void TrySetObjectiveTargetCount_ShouldEnforceMinimumOfOne()
        {
            var objectives = new LevelObjective[4];
            objectives[0] = new LevelObjective { TargetCount = 10 };

            ObjectiveEditorHelper.TrySetObjectiveTargetCount(objectives, 0, 0);

            Assert.Equal(1, objectives[0].TargetCount);
        }

        [Fact]
        public void GetElementTypesForLayer_ShouldReturnTileTypes_ForTileLayer()
        {
            var types = ObjectiveEditorHelper.GetElementTypesForLayer(ObjectiveTargetLayer.Tile);

            Assert.Equal(6, types.Length);
            Assert.Contains(types, t => t.Value == (int)TileType.Red && t.Name == "Red");
            Assert.Contains(types, t => t.Value == (int)TileType.Blue && t.Name == "Blue");
        }

        [Fact]
        public void GetElementTypesForLayer_ShouldReturnCoverTypes_ForCoverLayer()
        {
            var types = ObjectiveEditorHelper.GetElementTypesForLayer(ObjectiveTargetLayer.Cover);

            Assert.Equal(3, types.Length);
            Assert.Contains(types, t => t.Value == (int)CoverType.Cage && t.Name == "Cage");
        }

        [Fact]
        public void GetElementTypesForLayer_ShouldReturnGroundTypes_ForGroundLayer()
        {
            var types = ObjectiveEditorHelper.GetElementTypesForLayer(ObjectiveTargetLayer.Ground);

            Assert.Single(types);
            Assert.Equal((int)GroundType.Ice, types[0].Value);
        }

        [Fact]
        public void GetElementTypeName_ShouldReturnCorrectName()
        {
            Assert.Equal("Red", ObjectiveEditorHelper.GetElementTypeName(ObjectiveTargetLayer.Tile, (int)TileType.Red));
            Assert.Equal("Cage", ObjectiveEditorHelper.GetElementTypeName(ObjectiveTargetLayer.Cover, (int)CoverType.Cage));
            Assert.Equal("Ice", ObjectiveEditorHelper.GetElementTypeName(ObjectiveTargetLayer.Ground, (int)GroundType.Ice));
            Assert.Equal("Unknown", ObjectiveEditorHelper.GetElementTypeName(ObjectiveTargetLayer.None, 0));
        }
    }
}
