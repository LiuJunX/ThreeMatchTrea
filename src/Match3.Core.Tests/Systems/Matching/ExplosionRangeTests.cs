using System;
using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.PowerUps.Effects;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Matching;

public class ExplosionRangeTests
{
    [Fact]
    public void SquareBomb_ShouldExplode5x5Area()
    {
        // Arrange
        var effect = new SquareBombEffect();

        // Create a 10x10 grid
        var state = new GameState(10, 10, 5, StubRandom.WithFixedValue(0));
        for (int i = 0; i < 100; i++)
        {
            int x = i % 10;
            int y = i / 10;
            state.SetTile(x, y, new Tile(i, ElementType.Item1, x, y));
        }

        // Act
        // Center at (5, 5)
        int cx = 5, cy = 5;
        var origin = new Position(cx, cy);
        var affectedTiles = new HashSet<Position>();
        
        effect.Apply(in state, origin, affectedTiles);

        // Assert
        // 5x5 area = 25 tiles
        Assert.Equal(25, affectedTiles.Count);
        
        // Check boundaries
        bool allWithinRange = true;
        foreach (var p in affectedTiles)
        {
            int dx = p.X - cx;
            int dy = p.Y - cy;
            if (Math.Abs(dx) > 2 || Math.Abs(dy) > 2)
            {
                allWithinRange = false;
                break;
            }
        }
        Assert.True(allWithinRange, "Some positions are outside the 5x5 range");
    }
}
