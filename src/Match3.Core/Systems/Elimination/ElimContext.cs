using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;

namespace Match3.Core.Systems.Elimination;

/// <summary>
/// Context for an elimination attempt. Carries the source and optional match color.
/// Implicitly convertible from <see cref="ElimSource"/> so existing callers need no changes.
/// </summary>
public readonly struct ElimContext
{
    /// <summary>What caused this elimination.</summary>
    public ElimSource Source { get; }

    /// <summary>
    /// Color of the matched tiles. Only meaningful when <see cref="Source"/> is
    /// <see cref="ElimSource.Match"/> or <see cref="ElimSource.ColorBomb"/>.
    /// Reserved for future obstacle types that need color info on direct hits.
    /// </summary>
    public ElementType MatchColor { get; }

    public ElimContext(ElimSource source, ElementType matchColor = ElementType.None)
    {
        Source = source;
        MatchColor = matchColor;
    }

    /// <summary>
    /// Implicit conversion from <see cref="ElimSource"/>.
    /// Allows existing call sites to pass a bare enum value without changes.
    /// </summary>
    public static implicit operator ElimContext(ElimSource source) => new(source);
}
