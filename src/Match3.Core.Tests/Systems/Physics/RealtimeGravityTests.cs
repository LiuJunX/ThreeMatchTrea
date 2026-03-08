using System.Numerics;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Physics;
using Match3.Core.Tests.TestFixtures;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Physics;

public class RealtimeGravityTests
{

    [Fact]
    public void Update_ShouldMoveFloatingTileDown()
    {
        // Arrange
        var state = new GameState(1, 10, 5, new StubRandom(0));
        // Clear board
        for(int y=0; y<10; y++) state.SetTile(0, y, new Tile(y, ElementType.None, 0, y));

        // Place a tile at (0, 0) - Top of board
        var tile = new Tile(100, ElementType.Item1, 0, 0);
        state.SetTile(0, 0, tile);

        var physics = new RealtimeGravitySystem(new Match3Config { GravitySpeed = 35.0f }, new StubRandom(0));

        // Act
        // Simulate 0.05s (Reduced dt to prevent logical swap at high speed)
        physics.Update(ref state, 0.05f);

        // Assert - Find tile by ID since it may have moved to a new grid cell
        var newTile = FindTileById(state, 100);

        // It should have moved down (Y increases)
        Assert.True(newTile.Position.Y > 0, $"Tile should have moved down. Pos: {newTile.Position.Y}");
        Assert.True(newTile.IsFalling, "Tile should be in Falling state");
        Assert.True(newTile.Velocity.Y > 0, "Tile should have downward velocity");
    }

    private static Tile FindTileById(GameState state, long id)
    {
        for (int i = 0; i < state.Grid.Length; i++)
        {
            if (state.Grid[i].Id == id) return state.Grid[i];
        }
        return new Tile(); // Should not happen
    }

    [Fact]
    public void Update_ShouldStopAtFloor()
    {
        // Arrange
        var state = new GameState(1, 2, 5, new StubRandom(0));
        // (0,0) = Red, (0,1) = None
        // Tile at (0,0) will fall to (0,1)
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(0, 1, new Tile(2, ElementType.None, 0, 1));
        
        var physics = new RealtimeGravitySystem(new Match3Config { GravitySpeed = 35.0f }, new StubRandom(0));
        
        // Act
        // Simulate enough time to fall 1 unit.
        // s = 0.5 * g * t^2. 1 = 0.5 * 35 * t^2 => t^2 = 1/17.5 = 0.057 => t = 0.24s.
        // Let's run for 0.5s to be sure.
        
        float dt = 0.016f; // 60fps
        for(int i=0; i<30; i++) // ~0.5s
        {
            physics.Update(ref state, dt);
        }
        
        // Assert
        // Tile should be at (0,1)
        var tileAt0 = state.GetTile(0, 0);
        var tileAt1 = state.GetTile(0, 1);
        
        Assert.Equal(ElementType.None, tileAt0.Type); // Should be empty
        Assert.Equal(ElementType.Item1, tileAt1.Type); // Should have moved here
        Assert.Equal(1.0f, tileAt1.Position.Y, 0.001f); // Should be exactly at 1.0
        Assert.False(tileAt1.IsFalling, "Tile should have stopped");
        Assert.Equal(0f, tileAt1.Velocity.Y);
    }

    [Fact]
    public void Update_ShouldSlideDiagonal()
    {
        // Arrange
        // Grid 3x3
        // (1,0) = Red (Falling)
        // (1,1) = Suspended/Obstacle
        // (0,1) = None (Target)
        // (2,1) = Obstacle
        var state = new GameState(3, 3, 5, new StubRandom(0));
        
        // Setup Suspended Tile at (1,1) to force slide
        var obstacle = new Tile(9, ElementType.Item2, 1, 1);
        obstacle.IsSuspended = true; 
        state.SetTile(1, 1, obstacle);
        
        // Target at (0,1) is empty
        state.SetTile(0, 1, new Tile(0, ElementType.None, 0, 1));
        
        // Falling tile at (1,0)
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));

        // Block Right side (2,1) to force Left Slide
        state.SetTile(2, 1, new Tile(8, ElementType.Item3, 2, 1));

        // Block (0,2) so it stops at (0,1)
        state.SetTile(0, 2, new Tile(10, ElementType.Item2, 0, 2));

        var physics = new RealtimeGravitySystem(new Match3Config { GravitySpeed = 35.0f }, new StubRandom(0));

        // Act
        float dt = 0.016f;
        for(int i=0; i<40; i++) // ~0.64s
        {
            physics.Update(ref state, dt);
        }

        // Assert
        // Tile should have moved from (1,0) to (0,1)
        var tileAtTarget = state.GetTile(0, 1);
        
        Assert.Equal(ElementType.Item1, tileAtTarget.Type);
        Assert.Equal(1.0f, tileAtTarget.Position.Y, 0.05f); // Allow small epsilon for sliding snap
        Assert.Equal(0.0f, tileAtTarget.Position.X, 0.05f);
    }

    [Fact]
    public void Update_ShouldFallSmoothlyFromCellToCell()
    {
        // Arrange
        var state = new GameState(1, 2, 5, new StubRandom(0));
        // (0,0) = Red, (0,1) = None
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(0, 1, new Tile(2, ElementType.None, 0, 1));

        var config = new Match3Config { GravitySpeed = 35.0f, MaxFallSpeed = 20.0f };
        var physics = new RealtimeGravitySystem(config, new StubRandom(0));
        
        float dt = 0.016f;
        float previousY = 0f;
        bool startedFalling = false;
        bool reachedBottom = false;

        // Act & Assert Loop
        for (int i = 0; i < 60; i++) // Run for 1 second (plenty of time)
        {
            physics.Update(ref state, dt);
            
            // Check tile position
            // Since the tile might logically move from (0,0) to (0,1) during the process,
            // we need to check both slots to find where the tile is.
            var tile0 = state.GetTile(0, 0);
            var tile1 = state.GetTile(0, 1);
            
            Tile currentTile;
            if (tile0.Type == ElementType.Item1) currentTile = tile0;
            else if (tile1.Type == ElementType.Item1) currentTile = tile1;
            else 
            {
                Assert.Fail("Tile disappeared!");
                return;
            }

            // Verify Smoothness
            if (currentTile.IsFalling)
            {
                startedFalling = true;
                Assert.True(currentTile.Position.Y >= previousY, $"Tile moved backwards at frame {i}: Prev={previousY}, Curr={currentTile.Position.Y}");
                
                // Max movement per frame check (approx check to ensure no teleporting)
                // Max speed 20. dt=0.016. Max delta ~ 0.32.
                // We use 0.35f to be strict but allow slight floating point variance.
                float deltaY = currentTile.Position.Y - previousY;
                Assert.True(deltaY <= 0.35f, $"Tile teleported at frame {i}: Delta={deltaY}");
            }
            else if (startedFalling && currentTile.Position.Y >= 1.0f - 0.001f)
            {
                reachedBottom = true;
                Assert.Equal(1.0f, currentTile.Position.Y, 0.001f);
                Assert.Equal(0f, currentTile.Velocity.Y);
            }

            previousY = currentTile.Position.Y;
            
            if (reachedBottom) break;
        }

        Assert.True(startedFalling, "Tile never started falling");
        Assert.True(reachedBottom, "Tile never reached the bottom (1.0)");
    }

    [Fact]
    public void Update_SameSeed_ProducesDeterministicResults()
    {
        // Arrange - Two identical systems with same seed
        const int seed = 12345;
        var random1 = new DefaultRandom(seed);
        var random2 = new DefaultRandom(seed);

        var config = new Match3Config(8, 8, 6) { GravitySpeed = 35.0f };
        var physics1 = new RealtimeGravitySystem(config, random1);
        var physics2 = new RealtimeGravitySystem(config, random2);

        // Create identical initial states with multiple tiles needing to fall
        var state1 = CreateMultiColumnState(new StubRandom(0));
        var state2 = CreateMultiColumnState(new StubRandom(0));

        // Act - Run both systems for multiple frames
        const float dt = 0.016f;
        for (int i = 0; i < 30; i++)
        {
            physics1.Update(ref state1, dt);
            physics2.Update(ref state2, dt);
        }

        // Assert - Both states should be identical
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                var tile1 = state1.GetTile(x, y);
                var tile2 = state2.GetTile(x, y);

                Assert.Equal(tile1.Type, tile2.Type);
                Assert.Equal(tile1.Position.X, tile2.Position.X, 0.001f);
                Assert.Equal(tile1.Position.Y, tile2.Position.Y, 0.001f);
                Assert.Equal(tile1.IsFalling, tile2.IsFalling);
            }
        }
    }

    private static GameState CreateMultiColumnState(IRandom rng)
    {
        var state = new GameState(8, 8, 6, rng);

        // Clear board
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                state.SetTile(x, y, new Tile(0, ElementType.None, x, y));

        // Place tiles at top that need to fall (creates column competition)
        int id = 1;
        for (int x = 0; x < 8; x++)
        {
            // Place 2 tiles per column at top
            state.SetTile(x, 0, new Tile(id++, ElementType.Item1, x, 0));
            state.SetTile(x, 1, new Tile(id++, ElementType.Item3, x, 1));
        }

        return state;
    }

    #region Cover Blocking Movement Tests

    [Fact]
    public void Update_CageCover_TileDoesNotFall()
    {
        // Arrange: Cage（静态 Cover）阻止棋子参与重力
        var state = new GameState(1, 3, 5, new StubRandom(0));
        for (int y = 0; y < 3; y++)
            state.SetTile(0, y, new Tile(y, ElementType.None, 0, y));

        state.SetTile(0, 0, new Tile(100, ElementType.Item1, 0, 0));
        state.SetCover(new Position(0, 0), new Cover(CoverType.Cage, 1));

        var physics = new RealtimeGravitySystem(
            new Match3Config { GravitySpeed = 35.0f }, new StubRandom(0));

        // Act
        for (int i = 0; i < 30; i++)
            physics.Update(ref state, 0.016f);

        // Assert: tile stays at (0,0) because Cage blocks movement
        var tile = state.GetTile(0, 0);
        Assert.Equal(ElementType.Item1, tile.Type);
        Assert.Equal(100, tile.Id);
    }

    [Fact]
    public void Update_ChainCover_TileDoesNotFall()
    {
        // Arrange: Chain（静态 Cover）也阻止移动
        var state = new GameState(1, 3, 5, new StubRandom(0));
        for (int y = 0; y < 3; y++)
            state.SetTile(0, y, new Tile(y, ElementType.None, 0, y));

        state.SetTile(0, 0, new Tile(100, ElementType.Item1, 0, 0));
        state.SetCover(new Position(0, 0), new Cover(CoverType.Chain, 1));

        var physics = new RealtimeGravitySystem(
            new Match3Config { GravitySpeed = 35.0f }, new StubRandom(0));

        // Act
        for (int i = 0; i < 30; i++)
            physics.Update(ref state, 0.016f);

        // Assert: tile stays at (0,0)
        var tile = state.GetTile(0, 0);
        Assert.Equal(ElementType.Item1, tile.Type);
        Assert.Equal(100, tile.Id);
    }

    [Fact]
    public void Update_BubbleCover_TileFalls()
    {
        // Arrange: Bubble（动态 Cover）不阻止下落
        var state = new GameState(1, 3, 5, new StubRandom(0));
        for (int y = 0; y < 3; y++)
            state.SetTile(0, y, new Tile(y, ElementType.None, 0, y));

        state.SetTile(0, 0, new Tile(100, ElementType.Item1, 0, 0));
        state.SetCover(new Position(0, 0), new Cover(CoverType.Bubble, 1, true));

        var physics = new RealtimeGravitySystem(
            new Match3Config { GravitySpeed = 35.0f }, new StubRandom(0));

        // Act
        for (int i = 0; i < 30; i++)
            physics.Update(ref state, 0.016f);

        // Assert: tile should have fallen to bottom (0,2)
        var tileAtBottom = state.GetTile(0, 2);
        Assert.Equal(ElementType.Item1, tileAtBottom.Type);
        Assert.Equal(100, tileAtBottom.Id);
    }

    [Fact]
    public void Update_CoverRemoved_TileStartsFalling()
    {
        // Arrange: Cage initially blocks, then removed → tile should fall
        var state = new GameState(1, 3, 5, new StubRandom(0));
        for (int y = 0; y < 3; y++)
            state.SetTile(0, y, new Tile(y, ElementType.None, 0, y));

        state.SetTile(0, 0, new Tile(100, ElementType.Item1, 0, 0));
        state.SetCover(new Position(0, 0), new Cover(CoverType.Cage, 1));

        var physics = new RealtimeGravitySystem(
            new Match3Config { GravitySpeed = 35.0f }, new StubRandom(0));

        // Phase 1: with cover — tile shouldn't move
        for (int i = 0; i < 10; i++)
            physics.Update(ref state, 0.016f);
        Assert.Equal(ElementType.Item1, state.GetTile(0, 0).Type);

        // Phase 2: remove cover
        state.SetCover(new Position(0, 0), Cover.Empty);

        // Phase 3: tile should now fall to bottom
        for (int i = 0; i < 30; i++)
            physics.Update(ref state, 0.016f);

        var tileAtBottom = state.GetTile(0, 2);
        Assert.Equal(ElementType.Item1, tileAtBottom.Type);
        Assert.Equal(100, tileAtBottom.Id);
    }

    #endregion
}

