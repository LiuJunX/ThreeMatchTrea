using System;

namespace Match3.Core.Attributes;

/// <summary>
/// Marks an enum field with AI-friendly index and display name for type mapping.
/// Used to map between AI prompt indices (0, 1, 2...) and actual enum values.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class AIMappingAttribute : Attribute
{
    /// <summary>
    /// The zero-based index used in AI prompts.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// The display name shown in AI prompts.
    /// </summary>
    public string DisplayName { get; }

    public AIMappingAttribute(int index, string displayName)
    {
        Index = index;
        DisplayName = displayName;
    }
}
