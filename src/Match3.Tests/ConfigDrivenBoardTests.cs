using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;
using Match3.Core;
using Match3.Core.Config;
using Match3.Core.Structs;
using Match3.Core.Logic;
using Match3.Core.Interfaces;
using Match3.Random;

namespace Match3.Tests;

public class ConfigDrivenBoardTests
{
    [Fact]
    public void HorizontalMatch_FromConfig()
    {
        var level = LoadLevel("horizontal_3.json");
        var controller = BuildController(level);
        var finder = new ClassicMatchFinder();
        var state = controller.State;
        var groups = finder.FindMatchGroups(in state);
        var matches = ToSet(groups);
        Assert.Equal(3, matches.Count);
        Assert.Contains(new Position(0, 0), matches);
        Assert.Contains(new Position(1, 0), matches);
        Assert.Contains(new Position(2, 0), matches);
    }

    [Fact]
    public void VerticalMatch_FromConfig()
    {
        var level = LoadLevel("vertical_3.json");
        var controller = BuildController(level);
        var finder = new ClassicMatchFinder();
        var state = controller.State;
        var groups = finder.FindMatchGroups(in state);
        var matches = ToSet(groups);
        Assert.Equal(3, matches.Count);
        Assert.Contains(new Position(0, 0), matches);
        Assert.Contains(new Position(0, 1), matches);
        Assert.Contains(new Position(0, 2), matches);
    }

    [Fact]
    public void GravityFall_FromConfig()
    {
        var level = LoadLevel("gravity_fall.json");
        var controller = BuildController(level);
        var gravity = new StandardGravitySystem(new StandardTileGenerator());
        var state = controller.State;
        gravity.ApplyGravity(ref state);
        Assert.Equal(TileType.Red, state.GetType(0, 4));
        Assert.Equal(TileType.None, state.GetType(0, 2));
    }

    private static HashSet<Position> ToSet(IEnumerable<MatchGroup> groups)
    {
        var s = new HashSet<Position>();
        foreach (var g in groups) foreach (var p in g.Positions) s.Add(p);
        return s;
    }

    private static LevelConfig LoadLevel(string fileName)
    {
        var root = GetSolutionRoot();
        var path = Path.Combine(root, "src", "Match3.Tests", "TestData", "boards", fileName);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<LevelConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
    }

    private static Match3Controller BuildController(LevelConfig level)
    {
        var seedManager = new SeedManager(12345);
        var rng = seedManager.GetRandom(RandomDomain.Main);
        var view = new NullView();
        var logger = new ConsoleGameLogger();
        var config = new Match3Config(level.Width, level.Height, 6);
        return new Match3Controller(
            config,
            rng,
            view,
            new ClassicMatchFinder(),
            new StandardMatchProcessor(),
            new StandardGravitySystem(new StandardTileGenerator(seedManager.GetRandom(RandomDomain.Refill))),
            new PowerUpHandler(),
            new StandardTileGenerator(seedManager.GetRandom(RandomDomain.Refill)),
            logger,
            level
        );
    }

    private static string GetSolutionRoot()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(baseDir, "../../../../../"));
    }

    private class NullView : IGameView
    {
        public void RenderBoard(TileType[,] board) { }
        public void ShowSwap(Position a, Position b, bool success) { }
        public void ShowMatches(IReadOnlyCollection<Position> matched) { }
        public void ShowGravity(IEnumerable<TileMove> moves) { }
        public void ShowRefill(IEnumerable<TileMove> newTiles) { }
    }
}
