using Match3.Core.Models.Grid;
using Match3.Core.Models.Enums;

namespace Match3.Core.Models.Input;

/// <summary>
/// Base class for all input intents that can be queued into the engine.
/// This decouples the Core from platform-specific input systems.
/// </summary>
public abstract record InputIntent;

/// <summary>
/// Represents a tap/click on a specific grid position.
/// </summary>
public record TapIntent(Position Position) : InputIntent;

/// <summary>
/// Represents a swipe gesture from a specific position in a direction.
/// </summary>
public record SwipeIntent(Position From, Direction Direction) : InputIntent;

// Future:
// public record AutoPlayIntent(bool Enabled) : InputIntent;
// public record DebugCommandIntent(string Command, string[] Args) : InputIntent;
