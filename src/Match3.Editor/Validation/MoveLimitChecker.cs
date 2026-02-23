using System.Collections.Generic;
using Match3.Core.Config;

namespace Match3.Editor.Validation
{
    /// <summary>
    /// Validates that the move limit is reasonable.
    /// </summary>
    public class MoveLimitChecker : ILevelChecker
    {
        public IEnumerable<ValidationMessage> Check(LevelConfig config)
        {
            if (config.MoveLimit < 1)
            {
                yield return new ValidationMessage(Severity.Error,
                    "Move limit must be at least 1.");
            }
        }
    }
}
