using System.Collections.Generic;
using Match3.Core.Config;
using Match3.Core.Models.Enums;

namespace Match3.Editor.Validation
{
    /// <summary>
    /// Validates that the board has meaningful content.
    /// </summary>
    public class BoardContentChecker : ILevelChecker
    {
        public IEnumerable<ValidationMessage> Check(LevelConfig config)
        {
            if (config.Grid == null || config.Grid.Length == 0)
            {
                yield return new ValidationMessage(Severity.Error, "Board grid is empty.");
                yield break;
            }

            if (config.Grid.Length != config.Width * config.Height)
            {
                yield return new ValidationMessage(Severity.Error,
                    $"Grid length ({config.Grid.Length}) does not match Width×Height ({config.Width}×{config.Height}).");
            }

            bool hasPlayableCell = false;
            foreach (var tile in config.Grid)
            {
                if (tile != ElementType.None && tile != ElementType.Unmatchable)
                {
                    hasPlayableCell = true;
                    break;
                }
            }

            if (!hasPlayableCell)
            {
                yield return new ValidationMessage(Severity.Error, "Board has no playable cells.");
            }
        }
    }
}
