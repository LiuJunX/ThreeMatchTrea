using System.Collections.Generic;
using Match3.Core.Config;

namespace Match3.Editor.Validation;

public interface ILevelChecker
{
    IEnumerable<ValidationMessage> Check(LevelConfig config);
}
