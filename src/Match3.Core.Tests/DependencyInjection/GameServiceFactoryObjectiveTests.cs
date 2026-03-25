using Match3.Core.Config;
using Match3.Core.DependencyInjection;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Xunit;

namespace Match3.Core.Tests.DependencyInjection;

/// <summary>
/// Tests for GameServiceFactory.CreateGameSession — verifying that
/// objectives, MoveLimit, and TargetDifficulty are initialized correctly
/// even when LevelConfig has a null grid (random board generation).
/// </summary>
public class GameServiceFactoryObjectiveTests
{
    private static IGameServiceFactory CreateFactory()
    {
        return new GameServiceFactoryBuilder().UseDefaultServices().Build();
    }

    [Fact]
    public void CreateGameSession_NullGrid_InitializesObjectives()
    {
        var factory = CreateFactory();
        var levelConfig = new LevelConfig
        {
            Width = 8,
            Height = 8,
            MoveLimit = 20,
            Grid = null!, // Random generation
            Objectives = new[]
            {
                new LevelObjective { TargetLayer = ObjectiveTargetLayer.Tile, ElementType = (int)ElementType.Item1, TargetCount = 10 },
                new LevelObjective { TargetLayer = ObjectiveTargetLayer.Tile, ElementType = (int)ElementType.Item3, TargetCount = 8 },
            }
        };

        var session = factory.CreateGameSession(levelConfig);

        var state = session.Engine.State;
        Assert.NotNull(state.ObjectiveProgress);

        // First objective: Red tiles
        Assert.Equal(ObjectiveTargetLayer.Tile, state.ObjectiveProgress[0].TargetLayer);
        Assert.Equal(1, state.ObjectiveProgress[0].ElementType);
        Assert.Equal(10, state.ObjectiveProgress[0].TargetCount);
        Assert.Equal(0, state.ObjectiveProgress[0].CurrentCount);
        Assert.True(state.ObjectiveProgress[0].IsActive);

        // Second objective: Blue tiles
        Assert.Equal(ObjectiveTargetLayer.Tile, state.ObjectiveProgress[1].TargetLayer);
        Assert.Equal(3, state.ObjectiveProgress[1].ElementType);
        Assert.Equal(8, state.ObjectiveProgress[1].TargetCount);
        Assert.True(state.ObjectiveProgress[1].IsActive);

        // Remaining slots inactive
        Assert.False(state.ObjectiveProgress[2].IsActive);
        Assert.False(state.ObjectiveProgress[3].IsActive);
    }

    [Fact]
    public void CreateGameSession_NullGrid_InitializesMoveLimit()
    {
        var factory = CreateFactory();
        var levelConfig = new LevelConfig
        {
            Width = 8,
            Height = 8,
            MoveLimit = 25,
            Grid = null!,
        };

        var session = factory.CreateGameSession(levelConfig);

        Assert.Equal(25, session.Engine.State.MoveLimit);
    }

    [Fact]
    public void CreateGameSession_NullGrid_InitializesTargetDifficulty()
    {
        var factory = CreateFactory();
        var levelConfig = new LevelConfig
        {
            Width = 8,
            Height = 8,
            TargetDifficulty = 0.7f,
            Grid = null!,
        };

        var session = factory.CreateGameSession(levelConfig);

        Assert.Equal(0.7f, session.Engine.State.TargetDifficulty, precision: 2);
    }

    [Fact]
    public void CreateGameSession_NullLevelConfig_NoObjectives()
    {
        var factory = CreateFactory();

        var session = factory.CreateGameSession(levelConfig: null);

        var state = session.Engine.State;
        // All objective slots should be inactive
        for (int i = 0; i < state.ObjectiveProgress.Length; i++)
        {
            Assert.False(state.ObjectiveProgress[i].IsActive);
        }
    }

    [Fact]
    public void CreateGameSession_NullGrid_BoardStillPopulated()
    {
        var factory = CreateFactory();
        var levelConfig = new LevelConfig
        {
            Width = 6,
            Height = 6,
            Grid = null!,
        };

        var session = factory.CreateGameSession(levelConfig);

        var state = session.Engine.State;
        Assert.Equal(6, state.Width);
        Assert.Equal(6, state.Height);

        // Board should have tiles (random generation fills them)
        int tileCount = 0;
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                if (state.GetTile(x, y).Type != ElementType.None)
                    tileCount++;
            }
        }
        Assert.True(tileCount > 0, "Random board should have tiles");
    }

    [Fact]
    public void CreateGameSession_ParsedJsonConfig_ObjectivesWork()
    {
        // End-to-end: JSON → LevelConfig → GameSession with objectives
        const string json = """
        {
            "width": 8,
            "height": 8,
            "moveLimit": 20,
            "targetDifficulty": 0.3,
            "objectives": [
                { "targetLayer": "Tile", "elementType": 1, "targetCount": 10 },
                { "targetLayer": "Tile", "elementType": 3, "targetCount": 10 },
                { "targetLayer": "Tile", "elementType": 4, "targetCount": 8 }
            ],
            "grid": null
        }
        """;

        var levelConfig = ConfigParser.ParseLevelConfig(json);
        var factory = CreateFactory();
        var session = factory.CreateGameSession(levelConfig);

        var state = session.Engine.State;

        // Verify all 3 objectives initialized
        Assert.True(state.ObjectiveProgress[0].IsActive);
        Assert.Equal(1, state.ObjectiveProgress[0].ElementType);
        Assert.Equal(10, state.ObjectiveProgress[0].TargetCount);

        Assert.True(state.ObjectiveProgress[1].IsActive);
        Assert.Equal(3, state.ObjectiveProgress[1].ElementType);
        Assert.Equal(10, state.ObjectiveProgress[1].TargetCount);

        Assert.True(state.ObjectiveProgress[2].IsActive);
        Assert.Equal(4, state.ObjectiveProgress[2].ElementType);
        Assert.Equal(8, state.ObjectiveProgress[2].TargetCount);

        Assert.False(state.ObjectiveProgress[3].IsActive);

        Assert.Equal(20, state.MoveLimit);
    }

    [Fact]
    public void CreateGameSession_NullGrid_WithCells_VoidCellsHaveNoTiles()
    {
        // Shaped level: grid=null (random tiles), cells defines board shape
        var factory = CreateFactory();
        var levelConfig = new LevelConfig(5, 5)
        {
            MoveLimit = 15,
            Grid = null!,
        };
        // Make corners Void (cross shape)
        levelConfig.Cells[0] = CellKind.Void;   // (0,0)
        levelConfig.Cells[4] = CellKind.Void;   // (4,0)
        levelConfig.Cells[20] = CellKind.Void;  // (0,4)
        levelConfig.Cells[24] = CellKind.Void;  // (4,4)

        var session = factory.CreateGameSession(levelConfig);
        var state = session.Engine.State;

        // Void cells should have no tiles
        Assert.Equal(ElementType.None, state.GetTile(0, 0).Type);
        Assert.Equal(ElementType.None, state.GetTile(4, 0).Type);
        Assert.Equal(ElementType.None, state.GetTile(0, 4).Type);
        Assert.Equal(ElementType.None, state.GetTile(4, 4).Type);

        // Slot cells should have random tiles
        Assert.NotEqual(ElementType.None, state.GetTile(1, 0).Type);
        Assert.NotEqual(ElementType.None, state.GetTile(2, 2).Type);
        Assert.NotEqual(ElementType.None, state.GetTile(3, 4).Type);
    }

    [Fact]
    public void CreateGameSession_ParsedJsonWithCells_VoidCellsRespected()
    {
        // End-to-end: JSON with cells array → shaped board
        const string json = """
        {
            "width": 3,
            "height": 3,
            "moveLimit": 10,
            "grid": null,
            "cells": [0, 1, 0, 1, 1, 1, 0, 1, 0]
        }
        """;

        var levelConfig = ConfigParser.ParseLevelConfig(json);
        var factory = CreateFactory();
        var session = factory.CreateGameSession(levelConfig);
        var state = session.Engine.State;

        // Diamond shape: corners are Void
        Assert.Equal(ElementType.None, state.GetTile(0, 0).Type);
        Assert.Equal(ElementType.None, state.GetTile(2, 0).Type);
        Assert.Equal(ElementType.None, state.GetTile(0, 2).Type);
        Assert.Equal(ElementType.None, state.GetTile(2, 2).Type);

        // Center cross has tiles
        Assert.NotEqual(ElementType.None, state.GetTile(1, 0).Type);
        Assert.NotEqual(ElementType.None, state.GetTile(0, 1).Type);
        Assert.NotEqual(ElementType.None, state.GetTile(1, 1).Type);
        Assert.NotEqual(ElementType.None, state.GetTile(2, 1).Type);
        Assert.NotEqual(ElementType.None, state.GetTile(1, 2).Type);
    }
}
