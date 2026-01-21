using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Match3.Core.Attributes;
using Match3.Core.Models.Enums;

namespace Match3.Editor.Logic
{
    /// <summary>
    /// Registry for AI type mappings. Provides conversion between AI prompt indices
    /// and actual enum values, with lazy caching for performance.
    /// </summary>
    public static class AITypeRegistry
    {
        private static readonly Dictionary<Type, List<AIMappingEntry>> Cache = new Dictionary<Type, List<AIMappingEntry>>();
        private static readonly object CacheLock = new object();

        /// <summary>
        /// Represents a single AI mapping entry.
        /// </summary>
        public readonly struct AIMappingEntry
        {
            public int AIIndex { get; }
            public int EnumValue { get; }
            public string DisplayName { get; }

            public AIMappingEntry(int aiIndex, int enumValue, string displayName)
            {
                AIIndex = aiIndex;
                EnumValue = enumValue;
                DisplayName = displayName;
            }
        }

        /// <summary>
        /// Converts an AI prompt index to the actual enum value.
        /// </summary>
        /// <typeparam name="T">The enum type.</typeparam>
        /// <param name="aiIndex">The zero-based AI index.</param>
        /// <returns>The enum value, or default if not found.</returns>
        public static int FromAIIndex<T>(int aiIndex) where T : Enum
        {
            var entries = GetEntries<T>();
            var entry = entries.FirstOrDefault(e => e.AIIndex == aiIndex);
            return entry.EnumValue;
        }

        /// <summary>
        /// Converts an actual enum value to its AI prompt index.
        /// </summary>
        /// <typeparam name="T">The enum type.</typeparam>
        /// <param name="value">The enum value.</param>
        /// <returns>The AI index, or -1 if not found.</returns>
        public static int ToAIIndex<T>(int value) where T : Enum
        {
            var entries = GetEntries<T>();
            var entry = entries.FirstOrDefault(e => e.EnumValue == value);
            return entry.AIIndex >= 0 ? entry.AIIndex : -1;
        }

        /// <summary>
        /// Generates a prompt description string for an enum type.
        /// Example: "0=Red, 1=Green, 2=Blue"
        /// </summary>
        /// <typeparam name="T">The enum type.</typeparam>
        /// <returns>A formatted description string.</returns>
        public static string GetPromptDescription<T>() where T : Enum
        {
            var entries = GetEntries<T>();
            if (entries.Count == 0)
                return "";

            return string.Join(", ", entries.OrderBy(e => e.AIIndex).Select(e => $"{e.AIIndex}={e.DisplayName}"));
        }

        /// <summary>
        /// Gets all mapping entries for an enum type.
        /// </summary>
        /// <typeparam name="T">The enum type.</typeparam>
        /// <returns>A read-only list of mapping entries.</returns>
        public static IReadOnlyList<AIMappingEntry> GetEntries<T>() where T : Enum
        {
            return GetEntries(typeof(T));
        }

        /// <summary>
        /// Gets all mapping entries for an enum type.
        /// </summary>
        /// <param name="enumType">The enum type.</param>
        /// <returns>A list of mapping entries.</returns>
        public static IReadOnlyList<AIMappingEntry> GetEntries(Type enumType)
        {
            lock (CacheLock)
            {
                if (Cache.TryGetValue(enumType, out var cached))
                    return cached;

                var entries = BuildEntries(enumType);
                Cache[enumType] = entries;
                return entries;
            }
        }

        /// <summary>
        /// Converts an AI prompt index to enum value based on the objective layer.
        /// </summary>
        /// <param name="layer">The target layer.</param>
        /// <param name="aiIndex">The zero-based AI index.</param>
        /// <returns>The actual enum value.</returns>
        public static int FromAIIndex(ObjectiveTargetLayer layer, int aiIndex)
        {
            switch (layer)
            {
                case ObjectiveTargetLayer.Tile:
                    return FromAIIndex<TileType>(aiIndex);
                case ObjectiveTargetLayer.Cover:
                    return FromAIIndex<CoverType>(aiIndex);
                case ObjectiveTargetLayer.Ground:
                    return FromAIIndex<GroundType>(aiIndex);
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Generates a prompt description for a specific layer.
        /// </summary>
        /// <param name="layer">The target layer.</param>
        /// <returns>A formatted description string.</returns>
        public static string GetPromptDescription(ObjectiveTargetLayer layer)
        {
            switch (layer)
            {
                case ObjectiveTargetLayer.Tile:
                    return GetPromptDescription<TileType>();
                case ObjectiveTargetLayer.Cover:
                    return GetPromptDescription<CoverType>();
                case ObjectiveTargetLayer.Ground:
                    return GetPromptDescription<GroundType>();
                default:
                    return "";
            }
        }

        /// <summary>
        /// Gets element types for a given layer (for UI dropdowns).
        /// Returns tuples of (actual enum value, display name).
        /// </summary>
        /// <param name="layer">The target layer.</param>
        /// <returns>List of (value, name) tuples ordered by AI index.</returns>
        public static IReadOnlyList<(int Value, string Name)> GetElementTypesForLayer(ObjectiveTargetLayer layer)
        {
            IReadOnlyList<AIMappingEntry> entries;
            switch (layer)
            {
                case ObjectiveTargetLayer.Tile:
                    entries = GetEntries<TileType>();
                    break;
                case ObjectiveTargetLayer.Cover:
                    entries = GetEntries<CoverType>();
                    break;
                case ObjectiveTargetLayer.Ground:
                    entries = GetEntries<GroundType>();
                    break;
                default:
                    entries = Array.Empty<AIMappingEntry>();
                    break;
            }

            return entries
                .OrderBy(e => e.AIIndex)
                .Select(e => (e.EnumValue, e.DisplayName))
                .ToArray();
        }

        private static List<AIMappingEntry> BuildEntries(Type enumType)
        {
            var entries = new List<AIMappingEntry>();

            foreach (var field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var attr = field.GetCustomAttribute<AIMappingAttribute>();
                if (attr != null)
                {
                    var value = Convert.ToInt32(field.GetValue(null));
                    entries.Add(new AIMappingEntry(attr.Index, value, attr.DisplayName));
                }
            }

            return entries;
        }
    }
}
