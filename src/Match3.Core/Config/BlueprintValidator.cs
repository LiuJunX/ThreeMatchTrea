using System;
using System.Collections.Generic;

namespace Match3.Core.Config;

/// <summary>
/// Validates a ProgressionBlueprint for structural correctness.
/// </summary>
public static class BlueprintValidator
{
    /// <summary>
    /// Validate blueprint structure. Returns a list of error messages (empty = valid).
    /// </summary>
    public static List<string> Validate(ProgressionBlueprint blueprint)
    {
        var errors = new List<string>();

        if (blueprint.Phases == null || blueprint.Phases.Length == 0)
        {
            errors.Add("Blueprint must have at least one phase.");
            return errors;
        }

        for (int i = 0; i < blueprint.Phases.Length; i++)
        {
            var phase = blueprint.Phases[i];
            var prefix = $"Phase[{i}] '{phase.Name}'";

            // Range validity
            if (phase.StartLevel > phase.EndLevel)
                errors.Add($"{prefix}: startLevel ({phase.StartLevel}) > endLevel ({phase.EndLevel}).");

            if (phase.StartLevel < 1)
                errors.Add($"{prefix}: startLevel must be >= 1.");

            // No overlap with previous phase
            if (i > 0)
            {
                var prev = blueprint.Phases[i - 1];
                if (phase.StartLevel <= prev.EndLevel)
                    errors.Add($"{prefix}: overlaps with Phase[{i - 1}] '{prev.Name}' (ends at {prev.EndLevel}).");
            }

            // Board constraints
            if (phase.MinBoardWidth > phase.MaxBoardWidth)
                errors.Add($"{prefix}: minBoardWidth > maxBoardWidth.");
            if (phase.MinBoardHeight > phase.MaxBoardHeight)
                errors.Add($"{prefix}: minBoardHeight > maxBoardHeight.");
            if (phase.MinBoardWidth < 3 || phase.MaxBoardWidth > 20)
                errors.Add($"{prefix}: board width out of valid range (3-20).");
            if (phase.MinBoardHeight < 3 || phase.MaxBoardHeight > 20)
                errors.Add($"{prefix}: board height out of valid range (3-20).");

            // Color constraints
            if (phase.MinColors > phase.MaxColors)
                errors.Add($"{prefix}: minColors > maxColors.");
            if (phase.MinColors < 3 || phase.MaxColors > 7)
                errors.Add($"{prefix}: color count out of valid range (3-7).");

            // Difficulty constraints
            if (phase.MinDifficulty > phase.MaxDifficulty)
                errors.Add($"{prefix}: minDifficulty > maxDifficulty.");
            if (phase.MinDifficulty < 0f || phase.MaxDifficulty > 1f)
                errors.Add($"{prefix}: difficulty out of valid range (0.0-1.0).");

            // Win rate constraints
            if (phase.MinWinRate > phase.MaxWinRate)
                errors.Add($"{prefix}: minWinRate > maxWinRate.");
            if (phase.MinWinRate < 0f || phase.MaxWinRate > 1f)
                errors.Add($"{prefix}: win rate out of valid range (0.0-1.0).");

            // Move constraints
            if (phase.MinMoves > phase.MaxMoves)
                errors.Add($"{prefix}: minMoves > maxMoves.");
            if (phase.MinMoves < 1)
                errors.Add($"{prefix}: minMoves must be >= 1.");

            // Rhythm ratios
            if (phase.EasyRatio < 0f || phase.HardRatio < 0f)
                errors.Add($"{prefix}: rhythm ratios must be >= 0.");
            if (phase.EasyRatio + phase.HardRatio > 1f)
                errors.Add($"{prefix}: easyRatio + hardRatio exceeds 1.0.");

            // Shapes
            if (phase.Shapes == null || phase.Shapes.Length == 0)
                errors.Add($"{prefix}: must have at least one allowed shape.");

            // Allowance counts
            ValidateAllowanceCounts(phase, prefix, errors);
        }

        // Check contiguity (no gaps)
        for (int i = 1; i < blueprint.Phases.Length; i++)
        {
            var prev = blueprint.Phases[i - 1];
            var curr = blueprint.Phases[i];
            if (curr.StartLevel != prev.EndLevel + 1)
                errors.Add($"Gap between Phase[{i - 1}] '{prev.Name}' (ends {prev.EndLevel}) and Phase[{i}] '{curr.Name}' (starts {curr.StartLevel}).");
        }

        return errors;
    }

    private static void ValidateAllowanceCounts(PhaseConfig phase, string prefix, List<string> errors)
    {
        if (phase.Obstacles != null)
        {
            foreach (var a in phase.Obstacles)
            {
                if (a.MaxStage < 1) errors.Add($"{prefix}: obstacle {a.Type} maxStage must be >= 1.");
                if (a.MaxCount < 1) errors.Add($"{prefix}: obstacle {a.Type} maxCount must be >= 1.");
            }
        }

        if (phase.Covers != null)
        {
            foreach (var a in phase.Covers)
            {
                if (a.MaxHealth < 1) errors.Add($"{prefix}: cover {a.Type} maxHealth must be >= 1.");
                if (a.MaxCount < 1) errors.Add($"{prefix}: cover {a.Type} maxCount must be >= 1.");
            }
        }

        if (phase.Grounds != null)
        {
            foreach (var a in phase.Grounds)
            {
                if (a.MaxHealth < 1) errors.Add($"{prefix}: ground {a.Type} maxHealth must be >= 1.");
                if (a.MaxCount < 1) errors.Add($"{prefix}: ground {a.Type} maxCount must be >= 1.");
            }
        }
    }

    /// <summary>
    /// Find the phase that contains the given level number. Returns null if not found.
    /// </summary>
    public static PhaseConfig? FindPhase(ProgressionBlueprint blueprint, int levelNumber)
    {
        foreach (var phase in blueprint.Phases)
        {
            if (levelNumber >= phase.StartLevel && levelNumber <= phase.EndLevel)
                return phase;
        }
        return null;
    }
}
