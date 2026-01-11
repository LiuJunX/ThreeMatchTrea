using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Match3.Tests;

public class CodingStandardsTests
{
    [Fact]
    public void NoDirectSystemRandomOrGuidUsageOutsideRandomModule()
    {
        var root = GetRepoRoot();
        var targets = new[]
        {
            Path.Combine(root, "src", "Match3.Core"),
            Path.Combine(root, "src", "Match3.Web")
        };

        var banned = new[]
        {
            "new System.Random",
            "System.Random(",
            "Guid.NewGuid(",
        };

        var violations = new List<string>();
        foreach (var dir in targets)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                foreach (var pat in banned)
                {
                    if (text.Contains(pat, StringComparison.Ordinal))
                    {
                        violations.Add($"{file} -> {pat}");
                    }
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    private static string GetRepoRoot()
    {
        var baseDir = AppContext.BaseDirectory;
        var p = Directory.GetParent(baseDir);
        while (p != null && !Directory.EnumerateFiles(p.FullName, "*.sln").Any())
        {
            p = p.Parent;
        }
        return p?.FullName ?? Path.GetFullPath(Path.Combine(baseDir, "../../../../"));
    }
}
