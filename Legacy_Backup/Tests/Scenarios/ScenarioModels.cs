using System.Collections.Generic;

namespace Match3.Tests.Scenarios
{
    public class ScenarioMetadata
    {
        public string? CreatedUtc { get; set; }
        public string? UpdatedUtc { get; set; }
        public string Author { get; set; } = "";
        public string Version { get; set; } = "1";
        public List<string> Tags { get; set; } = new();
    }

    public class TestScenario
    {
        public string Name { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public int Seed { get; set; } = 42;
        public string[] Layout { get; set; } = System.Array.Empty<string>();
        public List<ScenarioMove> Moves { get; set; } = new();
        public List<ScenarioExpectation> Expectations { get; set; } = new();
    }

    public class ScenarioMove
    {
        public string From { get; set; } = "";
        public string To { get; set; } = "";
    }

    public class ScenarioExpectation
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string? Type { get; set; }
        public string? Bomb { get; set; }
    }
}
