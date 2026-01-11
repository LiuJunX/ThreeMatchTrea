using System;
using System.Collections.Generic;

namespace Match3.Core.Scenarios;

public sealed class ScenarioMetadata
{
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public string Author { get; set; } = "";
    public string Version { get; set; } = "1";
    public List<string> Tags { get; set; } = new();
}

