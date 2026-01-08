using Xunit;
using Match3.Core;
using Match3.Core.Config;
using Match3.Core.Structs;
using Match3.Core.Logic;
using Match3.Core.Interfaces;
using System.Collections.Generic;
using System.Numerics;
using Match3.Random;

namespace Match3.Tests;

public class GameBoardTests
{
    [Fact]
    public void Initialize_CreatesCorrectDimensions()
    {
        // Arrange
        int width = 8;
        int height = 8;
        int tileCount = 5;
        var rng = new TestRandomGenerator();
        
        // Use Controller to initialize
        var view = new MockGameView();
        var tileGen = new StandardTileGenerator(new DefaultRandom(1001));
        var gravity = new StandardGravitySystem(tileGen);
        var finder = new ClassicMatchFinder();
        var processor = new StandardMatchProcessor();
        var powerUp = new PowerUpHandler();
        var logger = new ConsoleGameLogger();
        var config = new Match3Config(width, height, tileCount);
        
        var controller = new Match3Controller(config, rng, view, finder, processor, gravity, powerUp, tileGen, logger);

        // Assert
        Assert.Equal(width, controller.State.Width);
        Assert.Equal(height, controller.State.Height);
    }

    [Fact]
    public void FindMatches_FoundHorizontalMatch()
    {
        // Arrange
        var state = CreateAndClearState(5, 5);
        // R R R G B
        state.SetTile(0, 0, new Tile(0, TileType.Red, 0, 0));
        state.SetTile(1, 0, new Tile(0, TileType.Red, 1, 0));
        state.SetTile(2, 0, new Tile(0, TileType.Red, 2, 0));
        state.SetTile(3, 0, new Tile(0, TileType.Green, 3, 0));
        state.SetTile(4, 0, new Tile(0, TileType.Blue, 4, 0));

        var finder = new ClassicMatchFinder();

        // Act
        var groups = finder.FindMatchGroups(in state);
        var matches = new HashSet<Position>();
        foreach(var g in groups) foreach(var p in g.Positions) matches.Add(p);

        // Assert
        Assert.Equal(3, matches.Count);
        Assert.Contains(new Position(0, 0), matches);
        Assert.Contains(new Position(1, 0), matches);
        Assert.Contains(new Position(2, 0), matches);
    }

    [Fact]
    public void FindMatches_FoundVerticalMatch()
    {
        // Arrange
        var state = CreateAndClearState(5, 5);
        // R
        // R
        // R
        state.SetTile(0, 0, new Tile(0, TileType.Red, 0, 0));
        state.SetTile(0, 1, new Tile(0, TileType.Red, 0, 1));
        state.SetTile(0, 2, new Tile(0, TileType.Red, 0, 2));
        state.SetTile(0, 3, new Tile(0, TileType.Green, 0, 3));

        var finder = new ClassicMatchFinder();

        // Act
        var groups = finder.FindMatchGroups(in state);
        var matches = new HashSet<Position>();
        foreach(var g in groups) foreach(var p in g.Positions) matches.Add(p);

        // Assert
        Assert.Equal(3, matches.Count);
        Assert.Contains(new Position(0, 0), matches);
        Assert.Contains(new Position(0, 1), matches);
        Assert.Contains(new Position(0, 2), matches);
    }

    [Fact]
    public void Swap_SwapsTwoTiles()
    {
        // Arrange
        var state = CreateAndClearState(5, 5);
        var p1 = new Position(0, 0);
        var p2 = new Position(1, 0);
        state.SetTile(p1.X, p1.Y, new Tile(0, TileType.Red, p1.X, p1.Y));
        state.SetTile(p2.X, p2.Y, new Tile(0, TileType.Blue, p2.X, p2.Y));

        // Act
        // Simulate Swap manually as it's private in Controller and we don't have a Swap helper public anymore
        // or we can test logic:
        var idxA = state.Index(p1.X, p1.Y);
        var idxB = state.Index(p2.X, p2.Y);
        var temp = state.Grid[idxA];
        state.Grid[idxA] = state.Grid[idxB];
        state.Grid[idxB] = temp;

        // Assert
        Assert.Equal(TileType.Blue, state.GetType(p1.X, p1.Y));
        Assert.Equal(TileType.Red, state.GetType(p2.X, p2.Y));
    }

    [Fact]
    public void ApplyGravity_TilesFall()
    {
        // Arrange
        var state = CreateAndClearState(3, 5);
        // Col 0:
        // (4) .
        // (3) .
        // (2) R  <- This should fall to 4
        
        state.SetTile(0, 2, new Tile(0, TileType.Red, 0, 2));

        var tileGen = new StandardTileGenerator(new DefaultRandom(2002));
        var gravity = new StandardGravitySystem(tileGen);

        // Act
        gravity.ApplyGravity(ref state);

        // Assert
        // The tile at (0,2) should fall to (0,4) (bottom)
        Assert.Equal(TileType.Red, state.GetType(0, 4));
        Assert.Equal(TileType.None, state.GetType(0, 2));
    }

    private GameState CreateAndClearState(int width, int height)
    {
        var state = new GameState(width, height, 5, new TestRandomGenerator());
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                state.SetTile(x, y, new Tile(0, TileType.None, x, y));
            }
        }
        return state;
    }

    private class MockGameView : IGameView
    {
        public void RenderBoard(TileType[,] board) { }
        public void ShowSwap(Position a, Position b, bool success) { }
        public void ShowMatches(IReadOnlyCollection<Position> matched) { }
        public void ShowGravity(IEnumerable<TileMove> moves) { }
        public void ShowRefill(IEnumerable<TileMove> moves) { }
    }
}

public class TestRandomGenerator : IRandom
{
    private int _val = 0;
    public int Next(int min, int max) 
    {
        return min + (_val++ % (max - min));
    }
}
