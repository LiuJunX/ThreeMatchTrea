using System.Collections.Generic;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;

namespace Match3.Editor.Validation;

/// <summary>
/// Validates that objectives are properly configured.
/// </summary>
public class ObjectiveChecker : ILevelChecker
{
    public IEnumerable<ValidationMessage> Check(LevelConfig config)
    {
        if (config.Objectives == null || config.Objectives.Length == 0)
        {
            yield return new ValidationMessage(Severity.Error, "No objectives defined.");
            yield break;
        }

        bool hasActive = false;
        for (int i = 0; i < config.Objectives.Length; i++)
        {
            var obj = config.Objectives[i];
            if (obj.TargetLayer == ObjectiveTargetLayer.None)
                continue;

            hasActive = true;

            if (obj.TargetCount <= 0)
            {
                yield return new ValidationMessage(Severity.Error,
                    $"Objective {i + 1}: target count must be > 0.");
            }
        }

        if (!hasActive)
        {
            yield return new ValidationMessage(Severity.Error,
                "At least one active objective is required.");
        }
    }
}
