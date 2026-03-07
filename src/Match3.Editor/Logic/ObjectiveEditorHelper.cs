using System;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;

namespace Match3.Editor.Logic
{
    /// <summary>
    /// Helper class for objective editing operations.
    /// Extracted from LevelEditorViewModel to reduce class size.
    /// </summary>
    public static class ObjectiveEditorHelper
    {
        public const int MaxObjectives = 4;

        /// <summary>
        /// Gets the count of active objectives (non-None layer).
        /// </summary>
        public static int GetActiveObjectiveCount(LevelObjective[] objectives)
        {
            int count = 0;
            foreach (var obj in objectives)
            {
                if (obj.TargetLayer != ObjectiveTargetLayer.None) count++;
            }
            return count;
        }

        /// <summary>
        /// Adds a new objective if under the maximum limit.
        /// Returns true if an objective was added.
        /// </summary>
        public static bool TryAddObjective(LevelObjective[] objectives)
        {
            for (int i = 0; i < objectives.Length; i++)
            {
                if (objectives[i].TargetLayer == ObjectiveTargetLayer.None)
                {
                    objectives[i] = new LevelObjective
                    {
                        TargetLayer = ObjectiveTargetLayer.Tile,
                        ElementType = (int)ElementType.Item1,
                        TargetCount = 10
                    };
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Removes an objective at the specified index.
        /// Returns true if the objective was removed.
        /// </summary>
        public static bool TryRemoveObjective(LevelObjective[] objectives, int index)
        {
            if (index < 0 || index >= objectives.Length) return false;

            objectives[index] = new LevelObjective { TargetLayer = ObjectiveTargetLayer.None };
            return true;
        }

        /// <summary>
        /// Updates an objective's target layer and resets element type to first valid value.
        /// Returns true if the layer was set.
        /// </summary>
        public static bool TrySetObjectiveLayer(LevelObjective[] objectives, int index, ObjectiveTargetLayer layer)
        {
            if (index < 0 || index >= objectives.Length) return false;

            var obj = objectives[index];
            obj.TargetLayer = layer;

            obj.ElementType = layer switch
            {
                ObjectiveTargetLayer.Tile => (int)ElementType.Item1,
                ObjectiveTargetLayer.Cover => (int)CoverType.Cage,
                ObjectiveTargetLayer.Ground => (int)GroundType.Ice,
                _ => 0
            };

            objectives[index] = obj;
            return true;
        }

        /// <summary>
        /// Updates an objective's element type.
        /// Returns true if the element type was set.
        /// </summary>
        public static bool TrySetObjectiveElementType(LevelObjective[] objectives, int index, int elementType)
        {
            if (index < 0 || index >= objectives.Length) return false;

            var obj = objectives[index];
            obj.ElementType = elementType;
            objectives[index] = obj;
            return true;
        }

        /// <summary>
        /// Updates an objective's target count.
        /// Returns true if the target count was set.
        /// </summary>
        public static bool TrySetObjectiveTargetCount(LevelObjective[] objectives, int index, int count)
        {
            if (index < 0 || index >= objectives.Length) return false;

            var obj = objectives[index];
            obj.TargetCount = Math.Max(1, count);
            objectives[index] = obj;
            return true;
        }

        /// <summary>
        /// Gets available element types for a given layer.
        /// </summary>
        public static (int Value, string Name)[] GetElementTypesForLayer(ObjectiveTargetLayer layer)
        {
            return layer switch
            {
                ObjectiveTargetLayer.Tile => new[]
                {
                    ((int)ElementType.Item1, "Red"),
                    ((int)ElementType.Item2, "Green"),
                    ((int)ElementType.Item3, "Blue"),
                    ((int)ElementType.Item4, "Yellow"),
                    ((int)ElementType.Item5, "Purple"),
                    ((int)ElementType.Item6, "Orange"),
                },
                ObjectiveTargetLayer.Cover => new[]
                {
                    ((int)CoverType.Cage, "Cage"),
                    ((int)CoverType.Chain, "Chain"),
                    ((int)CoverType.Bubble, "Bubble"),
                },
                ObjectiveTargetLayer.Ground => new[]
                {
                    ((int)GroundType.Ice, "Ice"),
                },
                _ => Array.Empty<(int, string)>()
            };
        }

        /// <summary>
        /// Gets the display name for an element type within a layer.
        /// </summary>
        public static string GetElementTypeName(ObjectiveTargetLayer layer, int elementType)
        {
            return layer switch
            {
                ObjectiveTargetLayer.Tile => ((ElementType)elementType).ToString(),
                ObjectiveTargetLayer.Cover => ((CoverType)elementType).ToString(),
                ObjectiveTargetLayer.Ground => ((GroundType)elementType).ToString(),
                _ => "Unknown"
            };
        }
    }
}
