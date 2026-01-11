using System.Collections.Generic;
using Match3.Core.Models.Enums;

namespace Match3.Core.Config;

public class ScenarioConfig
{
    public string Description { get; set; } = "";
    public int Seed { get; set; } = 12345;
    public LevelConfig InitialState { get; set; } = new LevelConfig();
    public List<MoveOperation> Operations { get; set; } = new();
    public LevelConfig ExpectedState { get; set; } = new LevelConfig();
    public List<ScenarioAssertion> Assertions { get; set; } = new();
}

public class ScenarioAssertion
{
    public int X { get; set; }
    public int Y { get; set; }
    public TileType? Type { get; set; }
    public BombType? Bomb { get; set; }
}

public class MoveOperation
{
    public int FromX { get; set; }
    public int FromY { get; set; }
    public int ToX { get; set; }
    public int ToY { get; set; }
    
    public MoveOperation() { }

    public MoveOperation(int x1, int y1, int x2, int y2)
    {
        FromX = x1; FromY = y1;
        ToX = x2; ToY = y2;
    }
}
