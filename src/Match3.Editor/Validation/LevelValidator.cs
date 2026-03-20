using System.Collections.Generic;
using System.Linq;
using Match3.Core.Config;

namespace Match3.Editor.Validation;

/// <summary>
/// Orchestrates all level checkers.
/// Add new checkers to the constructor to extend validation.
/// </summary>
public class LevelValidator
{
    private readonly List<ILevelChecker> _checkers;

    public LevelValidator()
    {
        _checkers = new List<ILevelChecker>
        {
            new BoardContentChecker(),
            new ObjectiveChecker(),
            new ObjectiveConsistencyChecker(),
            new MoveLimitChecker()
        };
    }

    public ValidationResult Validate(LevelConfig config)
    {
        var messages = _checkers.SelectMany(c => c.Check(config)).ToList();
        return new ValidationResult(messages);
    }
}
