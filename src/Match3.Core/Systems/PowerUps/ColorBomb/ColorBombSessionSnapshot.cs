using System.Collections.Generic;
using Match3.Core.Models.Enums;

namespace Match3.Core.Systems.PowerUps.ColorBomb;

/// <summary>
/// Lightweight snapshot of <see cref="ColorBombSessionManager"/> state
/// for ring buffer save/restore. Deep-copies all session data so the
/// snapshot is independent of the live manager.
/// </summary>
public sealed class ColorBombSessionSnapshot
{
    public readonly List<ColorBombSession> Sessions = new();
    public readonly HashSet<ElementType> ReservedColors = new();
    public int NextSessionId;
}
