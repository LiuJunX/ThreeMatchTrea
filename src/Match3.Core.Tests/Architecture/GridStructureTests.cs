using Xunit;
using Match3.Core.Models.Grid;
using Match3.Core.Models.Enums;
using Match3.Core.Primitives;
using Match3.Core.Structs.Handles;

namespace Match3.Core.Tests.Architecture
{
    /// <summary>
    /// Verifies the structural integrity of the ECS-Lite Grid System.
    /// </summary>
    public class GridStructureTests
    {
        [Fact]
        public void Grid_Should_Initialize_With_Handles()
        {
            // Arrange
            var grid = new Match3Grid(5, 5);

            // Act
            var tile = grid.GetTile(2, 2);
            var invalidTile = grid.GetTile(10, 10);

            // Assert
            Assert.True(tile.IsValid);
            Assert.False(invalidTile.IsValid);
            Assert.Equal(2, tile.X);
            Assert.Equal(2, tile.Y);
            Assert.Equal(TileType.None, tile.Topology); // Default is 0 (None) unless initialized
        }

        [Fact]
        public void Unit_Creation_Should_Persist_Data()
        {
            // Arrange
            var grid = new Match3Grid(3, 3);
            
            // Act
            // Create a Red Unit (Type 1, Color 1) at (0,0)
            var unitHandle = grid.CreateUnit(0, 0, 1, 1);

            // Assert
            Assert.True(unitHandle.IsValid);
            Assert.Equal(1, unitHandle.Type);
            Assert.Equal(1, unitHandle.Color);
            
            // Verify Tile Linkage
            var tile = grid.GetTile(0, 0);
            Assert.True(tile.HasUnit);
            Assert.Equal(unitHandle.Id, tile.Unit.Id);
            Assert.Equal(1, tile.Unit.Color);
        }

        [Fact]
        public void Data_Modification_Via_Handle_Should_Reflect_In_Storage()
        {
            // Arrange
            var grid = new Match3Grid(3, 3);
            var unit = grid.CreateUnit(1, 1, 1);
            unit.Health = 10;

            // Act
            unit.TakeDamage(3);

            // Assert
            Assert.Equal(7, unit.Health);
            
            // Re-fetch to ensure data persistence
            var fetchedUnit = grid.GetTile(1, 1).Unit;
            Assert.Equal(7, fetchedUnit.Health);
        }

        [Fact]
        public void Component_Independence_Should_Be_Maintained()
        {
            // Arrange
            var grid = new Match3Grid(3, 3);
            var u1 = grid.CreateUnit(0, 0, 1, 10); // Red
            var u2 = grid.CreateUnit(0, 1, 1, 20); // Blue

            // Act
            u1.Color = 99;

            // Assert
            Assert.Equal(99, u1.Color);
            Assert.Equal(20, u2.Color); // u2 should not change
        }
        
        [Fact]
        public void Tile_Topology_And_Walkability()
        {
            // Arrange
            var grid = new Match3Grid(3, 3);
            var tile = grid.GetTile(0, 0);
            
            // Act
            tile.Topology = TileType.Normal;
            
            // Assert
            Assert.True(tile.IsWalkable());
            
            tile.Topology = TileType.Wall;
            Assert.False(tile.IsWalkable());
        }

        [Fact]
        public void Unit_ID_Should_Be_Recycled()
        {
            // Arrange
            var grid = new Match3Grid(3, 3);
            
            // 1. Create Unit A (ID should be 1)
            var u1 = grid.CreateUnit(0, 0, 1);
            int id1 = u1.Id;
            
            // 2. Destroy Unit A
            grid.DestroyUnitAt(0, 0);
            
            // Verify it's gone from tile
            Assert.False(grid.GetTile(0, 0).HasUnit);
            
            // 3. Create Unit B
            var u2 = grid.CreateUnit(1, 1, 2);
            
            // Assert: ID should be reused (Stack LIFO behavior)
            Assert.Equal(id1, u2.Id);
        }

        [Fact]
        public void Generation_Check_Should_Invalidate_Old_Handles()
        {
            // Arrange
            var grid = new Match3Grid(3, 3);
            
            // 1. Create Unit A
            var u1 = grid.CreateUnit(0, 0, 1);
            int id1 = u1.Id;
            
            // Verify u1 is valid
            Assert.True(u1.IsValid);
            
            // 2. Destroy Unit A
            grid.DestroyUnitAt(0, 0);
            
            // Verify u1 is now invalid (Dangling Handle Protection)
            Assert.False(u1.IsValid, "Old handle should be invalid after destruction due to generation mismatch");
            
            // 3. Create Unit B (reuses ID)
            var u2 = grid.CreateUnit(1, 1, 2);
            Assert.Equal(id1, u2.Id);
            
            // Verify u1 is STILL invalid (even though ID exists)
            Assert.False(u1.IsValid, "Old handle should remain invalid even if ID is reused");
            Assert.True(u2.IsValid);
        }
    }
}
