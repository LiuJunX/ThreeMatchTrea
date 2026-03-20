using System.Collections.Generic;
using System.Linq;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;

namespace Match3.Editor.Validation;

/// <summary>
/// Validates that objectives are achievable given the board content.
/// Catches design-level issues like referencing layers with no content.
/// </summary>
public class ObjectiveConsistencyChecker : ILevelChecker
{
    public IEnumerable<ValidationMessage> Check(LevelConfig config)
    {
        if (config.Objectives == null) yield break;

        for (int i = 0; i < config.Objectives.Length; i++)
        {
            var obj = config.Objectives[i];
            if (obj.TargetLayer == ObjectiveTargetLayer.None)
                continue;

            foreach (var msg in CheckObjective(config, obj, i))
                yield return msg;
        }
    }

    private IEnumerable<ValidationMessage> CheckObjective(
        LevelConfig config, LevelObjective obj, int index)
    {
        int objNum = index + 1;

        switch (obj.TargetLayer)
        {
            case ObjectiveTargetLayer.Ground:
                foreach (var msg in CheckGroundObjective(config, obj, objNum))
                    yield return msg;
                break;

            case ObjectiveTargetLayer.Cover:
                foreach (var msg in CheckCoverObjective(config, obj, objNum))
                    yield return msg;
                break;

            case ObjectiveTargetLayer.Obstacle:
                foreach (var msg in CheckObstacleObjective(config, obj, objNum))
                    yield return msg;
                break;

            case ObjectiveTargetLayer.Tile:
                foreach (var msg in CheckTileObjective(config, obj, objNum))
                    yield return msg;
                break;
        }
    }

    private IEnumerable<ValidationMessage> CheckGroundObjective(
        LevelConfig config, LevelObjective obj, int objNum)
    {
        var targetType = (GroundType)obj.ElementType;

        if (config.Grounds == null || config.Grounds.Length == 0)
        {
            yield return new ValidationMessage(Severity.Error,
                $"Objective {objNum}: targets Ground/{targetType} but no Ground layer exists.");
            yield break;
        }

        int placed = config.Grounds.Count(g => g == targetType);
        if (placed == 0)
        {
            yield return new ValidationMessage(Severity.Error,
                $"Objective {objNum}: targets Ground/{targetType} but none placed on board.");
        }
        else if (obj.TargetCount > placed)
        {
            yield return new ValidationMessage(Severity.Warning,
                $"Objective {objNum}: targets {obj.TargetCount} Ground/{targetType} but only {placed} placed.");
        }
    }

    private IEnumerable<ValidationMessage> CheckCoverObjective(
        LevelConfig config, LevelObjective obj, int objNum)
    {
        var targetType = (CoverType)obj.ElementType;

        if (config.Covers == null || config.Covers.Length == 0)
        {
            yield return new ValidationMessage(Severity.Error,
                $"Objective {objNum}: targets Cover/{targetType} but no Cover layer exists.");
            yield break;
        }

        int placed = config.Covers.Count(c => c == targetType);
        if (placed == 0)
        {
            yield return new ValidationMessage(Severity.Error,
                $"Objective {objNum}: targets Cover/{targetType} but none placed on board.");
        }
        else if (obj.TargetCount > placed)
        {
            yield return new ValidationMessage(Severity.Warning,
                $"Objective {objNum}: targets {obj.TargetCount} Cover/{targetType} but only {placed} placed.");
        }
    }

    private IEnumerable<ValidationMessage> CheckObstacleObjective(
        LevelConfig config, LevelObjective obj, int objNum)
    {
        var targetType = (ObstacleType)obj.ElementType;

        if (config.Obstacles == null || config.Obstacles.Length == 0)
        {
            yield return new ValidationMessage(Severity.Error,
                $"Objective {objNum}: targets Obstacle/{targetType} but no Obstacle layer exists.");
            yield break;
        }

        int placed = config.Obstacles.Count(o => o == targetType);
        if (placed == 0)
        {
            yield return new ValidationMessage(Severity.Error,
                $"Objective {objNum}: targets Obstacle/{targetType} but none placed on board.");
        }
        else if (obj.TargetCount > placed)
        {
            // Warning not error: some obstacles are generators (MagicHat, Mailbox)
            yield return new ValidationMessage(Severity.Warning,
                $"Objective {objNum}: targets {obj.TargetCount} Obstacle/{targetType} but only {placed} placed.");
        }
    }

    private IEnumerable<ValidationMessage> CheckTileObjective(
        LevelConfig config, LevelObjective obj, int objNum)
    {
        var targetType = (ElementType)obj.ElementType;

        // Collectibles (Bird, Pearl, Plate, Envelope) are spawned by generators, not pre-placed
        if (targetType.IsCollectible())
            yield break;

        // Colors are spawned by the refill system, always available
        if (targetType.IsColor())
            yield break;

        // Bombs are generated from matches, always possible
        if (targetType.IsBomb())
            yield break;
    }
}
