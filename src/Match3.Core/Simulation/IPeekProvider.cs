using Match3.Core.Models.Grid;

namespace Match3.Core.Simulation;

/// <summary>
/// Provides read-only access to pre-computed future game states.
/// Physics systems use this to peek ahead without knowing the ring buffer implementation.
/// </summary>
public interface IPeekProvider
{
    /// <summary>
    /// Try to read a future game state at the given tick offset from current.
    /// </summary>
    /// <param name="tickOffset">Number of ticks ahead (1 = next tick, 2 = two ticks ahead, etc.).</param>
    /// <param name="state">The future state if available.</param>
    /// <returns>True if the offset is within the pre-computed range.</returns>
    bool TryPeekState(int tickOffset, out GameState state);

    /// <summary>
    /// Maximum valid tick offset for <see cref="TryPeekState"/>.
    /// Returns 0 if no peek is available.
    /// </summary>
    int MaxPeekOffset { get; }
}
