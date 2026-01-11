using System.IO;
using System.Linq;

namespace Match3.Core.Scenarios;

public static class ScenarioFileName
{
    public static string SanitizeFileStem(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "scenario";
        }

        var trimmed = name.Trim();
        // Allow only alphanumeric, underscore, hyphen
        var safe = new string(trimmed.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());

        if (string.IsNullOrWhiteSpace(safe))
        {
            return "scenario";
        }

        return safe;
    }
}

