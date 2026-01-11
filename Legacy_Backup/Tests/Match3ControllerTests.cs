using System.Collections.Generic;
using Match3.Core;
using Match3.Core.Config;
using Match3.Core.Interfaces;
using Xunit;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Gravity;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.Utility;
using System.Linq;
using Match3.Random;

using Match3.Core.Systems;

namespace Match3.Tests;

public class Match3ControllerTests
{
    [Fact]
    public void TrySwap_ValidSwap_ResolvesMatches()
    {
        // Arrange
        var rng = new TestRandomGenerator(); 
        var view = new MockGameView();
        var logger = new ConsoleGameLogger();
        var config = new Match3Config(4, 4, 5);
        var scoreSystem = new StandardScoreSystem();
        var inputSystem = new StandardInputSystem();

        var tileGen = new StandardTileGenerator(new DefaultRandom(3002));
        var controller = new Match3Engine(
            config,
            rng,
            view,
            logger,
            inputSystem,
            new ClassicMatchFinder(),
            new StandardMatchProcessor(scoreSystem),
            new StandardGravitySystem(new StandardTileGenerator(new DefaultRandom(3001))),
            new PowerUpHandler(scoreSystem),
            scoreSystem,
            tileGen);

        // Setup a stable board (Checkerboard of Blue/Purple) to prevent gravity/matches
        for(int y=0; y<4; y++) 
        {
            for(int x=0; x<4; x++) 
            {
                var type = ((x + y) % 2 == 0) ? TileType.Blue : TileType.Purple;
                controller.DebugSetTile(new Position(x, y), type);
            }
        }
        
        // Setup a specific match scenario on Row 0
        // R R G R  (Swap G<->R at x=2,3 to make R R R R)
        // Ensure tiles below are present so they don't fall.
        controller.DebugSetTile(new Position(0, 0), TileType.Red);
        controller.DebugSetTile(new Position(1, 0), TileType.Red);
        controller.DebugSetTile(new Position(2, 0), TileType.Green);
        controller.DebugSetTile(new Position(3, 0), TileType.Red);
        
        // Act
        // Swap (2,0) <-> (3,0)
        controller.OnTap(new Position(2, 0));
        controller.OnTap(new Position(3, 0));

        // Assert
        Assert.Equal("Swapping...", controller.StatusMessage);
        
        // Optimistic Swap Start
        Assert.True(view.SwapSuccess.HasValue);
        Assert.True(view.SwapSuccess.Value);

        // Pump update loop
        int maxSteps = 100;
        while(!controller.IsIdle && maxSteps-- > 0)
        {
            controller.Update(0.016f);
        }

        // Verify matches were detected
        Assert.NotEmpty(view.AllMatches);
        var matchSet = view.AllMatches[0];
        
        // 0,0 1,0 2,0 should match. They should be at Row 0 because board was full.
        Assert.Contains(new Position(0, 0), matchSet);
        Assert.Contains(new Position(1, 0), matchSet);
        Assert.Contains(new Position(2, 0), matchSet);
    }

    [Fact]
    public void TrySwap_InvalidSwap_Reverts()
    {
        // Arrange
        var rng = new TestRandomGenerator();
        var view = new MockGameView();
        var logger = new ConsoleGameLogger();
        var config = new Match3Config(4, 4, 5);
        var scoreSystem = new StandardScoreSystem();
        var inputSystem = new StandardInputSystem();

        var tileGen = new StandardTileGenerator(new DefaultRandom(3004));
        var controller = new Match3Engine(
            config,
            rng,
            view,
            logger,
            inputSystem,
            new ClassicMatchFinder(),
            new StandardMatchProcessor(scoreSystem),
            new StandardGravitySystem(new StandardTileGenerator(new DefaultRandom(3003))),
            new PowerUpHandler(scoreSystem),
            scoreSystem,
            tileGen);

        // Stable board
        for(int y=0; y<4; y++) 
        {
            for(int x=0; x<4; x++) 
            {
                var type = ((x + y) % 2 == 0) ? TileType.Blue : TileType.Purple;
                controller.DebugSetTile(new Position(x, y), type);
            }
        }

        // R G at (0,0) and (1,0)
        controller.DebugSetTile(new Position(0, 0), TileType.Red);
        controller.DebugSetTile(new Position(1, 0), TileType.Green);
        
        // Act
        // Swap (0,0) <-> (1,0) -> G R. No match.
        controller.OnTap(new Position(0, 0));
        controller.OnTap(new Position(1, 0));

        // Assert
        Assert.Equal("Swapping...", controller.StatusMessage);
        Assert.True(view.SwapSuccess.HasValue);
        Assert.True(view.SwapSuccess.Value);

        // Reset View
        view.Reset();

        // Pump update
        int maxSteps = 100;
        while(maxSteps-- > 0)
        {
            controller.Update(0.016f);
            if(controller.IsIdle) break;
        }
        
        // Assert Revert Signal
        Assert.True(view.SwapSuccess.HasValue, "View should be notified of swap revert");
        Assert.False(view.SwapSuccess.Value, "Swap should be reported as failed");
        
        // Verify state is reverted
        Assert.Equal(TileType.Red, controller.State.GetTile(0, 0).Type);
        Assert.Equal(TileType.Green, controller.State.GetTile(1, 0).Type);
    }

    [Fact]
    public void Match_Triggers_GravityAndRefill()
    {
        // Arrange
        var rng = new TestRandomGenerator(); 
        var view = new MockGameView();
        var logger = new ConsoleGameLogger();
        var config = new Match3Config(4, 4, 5);
        var scoreSystem = new StandardScoreSystem();
        var inputSystem = new StandardInputSystem();

        var tileGen = new StandardTileGenerator(new DefaultRandom(3006));
        var controller = new Match3Engine(
            config,
            rng,
            view,
            logger,
            inputSystem,
            new ClassicMatchFinder(),
            new StandardMatchProcessor(scoreSystem),
            new StandardGravitySystem(new StandardTileGenerator(new DefaultRandom(3005))),
            new PowerUpHandler(scoreSystem),
            scoreSystem,
            tileGen);

        // Setup board
        // B B B B (0)
        // B B B B (1)
        // R R G R (2)
        // B B B B (3)
        for(int y=0; y<4; y++) 
        {
            for(int x=0; x<4; x++) 
            {
                controller.DebugSetTile(new Position(x, y), TileType.Blue);
            }
        }
        
        // Set up match at row 2
        controller.DebugSetTile(new Position(0, 2), TileType.Red);
        controller.DebugSetTile(new Position(1, 2), TileType.Red);
        controller.DebugSetTile(new Position(2, 2), TileType.Green);
        controller.DebugSetTile(new Position(3, 2), TileType.Red);
        
        // Act: Swap to make R R R R
        controller.OnTap(new Position(2, 2));
        controller.OnTap(new Position(3, 2));
        
        // Pump updates
        int maxSteps = 200;
        while(!controller.IsIdle && maxSteps-- > 0)
        {
            controller.Update(0.016f);
        }
        
        // Assert
        Assert.NotEmpty(view.AllMatches);
        Assert.NotEmpty(view.AllGravity); // Tiles from row 0,1 should fall to 2
        Assert.NotEmpty(view.AllRefill); // New tiles should spawn at top
        
        // Verify board is full
        for(int y=0; y<4; y++)
        {
            for(int x=0; x<4; x++)
            {
                var t = controller.State.GetTile(x, y);
                Assert.NotEqual(TileType.None, t.Type);
            }
        }
    }
}

public class MockGameView : IGameView
{
    public bool? SwapSuccess { get; private set; }
    public List<List<Position>> AllMatches { get; } = new();
    public List<List<TileMove>> AllGravity { get; } = new();
    public List<List<TileMove>> AllRefill { get; } = new();

    public void Reset()
    {
        SwapSuccess = null;
        AllMatches.Clear();
        AllGravity.Clear();
        AllRefill.Clear();
    }

    public void RenderBoard(TileType[,] board) { }
    
    public void ShowSwap(Position a, Position b, bool success) 
    {
        SwapSuccess = success;
    }
    
    public void ShowMatches(IReadOnlyCollection<Position> matched) 
    {
        AllMatches.Add(new List<Position>(matched));
    }

    public void ShowMatches(HashSet<Position> positions)
    {
        AllMatches.Add(new List<Position>(positions));
    }
    
    public void ShowGravity(IEnumerable<TileMove> moves) 
    {
        AllGravity.Add(moves.ToList());
    }
    public void ShowRefill(IEnumerable<TileMove> newTiles) 
    {
        AllRefill.Add(newTiles.ToList());
    }
}
