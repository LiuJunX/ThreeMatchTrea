using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Swap;

namespace Match3.Core.Simulation;

/// <summary>
/// Handles player input (swap, tap) within simulation.
/// Extracted from SimulationEngine to reduce class size and isolate input concerns.
/// </summary>
internal sealed class SimulationInputHandler
{
    private readonly ISwapOperations _swapOperations;
    private readonly IPowerUpHandler _powerUpHandler;

    public SimulationInputHandler(
        ISwapOperations swapOperations,
        IPowerUpHandler powerUpHandler)
    {
        _swapOperations = swapOperations;
        _powerUpHandler = powerUpHandler;
    }

    /// <summary>
    /// Apply a move between two specified positions.
    /// Validates positions, performs the swap, tracks pending state, and emits events.
    /// </summary>
    /// <param name="from">Source position of the swap.</param>
    /// <param name="to">Destination position of the swap.</param>
    /// <param name="state">Current game state — modified in-place.</param>
    /// <param name="pendingMoveState">Pending move state — set on successful swap.</param>
    /// <param name="lastSwapFrom">Swap origin for bomb generation priority — set on successful swap.</param>
    /// <param name="lastSwapTo">Swap destination for bomb generation priority — set on successful swap.</param>
    /// <param name="currentTick">Current simulation tick for event timestamps.</param>
    /// <param name="elapsedTime">Elapsed simulation time for event timestamps.</param>
    /// <param name="eventCollector">Event collector for TilesSwappedEvent emission.</param>
    /// <returns>True if move was applied, false if positions were invalid or blocked.</returns>
    public bool ApplyMove(
        Position from,
        Position to,
        ref GameState state,
        ref PendingMoveState pendingMoveState,
        ref Position lastSwapFrom,
        ref Position lastSwapTo,
        int currentTick,
        float elapsedTime,
        IEventCollector eventCollector)
    {
        if (!state.IsValid(from) || !state.IsValid(to))
            return false;

        // Covers (ice, chains) block interaction
        if (!state.CanInteract(from) || !state.CanInteract(to))
            return false;

        // Clear selection when a move is applied (swipe bypasses HandleTap)
        state.SelectedPosition = Position.Invalid;

        // Get tile info BEFORE swap
        var tileA = state.GetTile(from.X, from.Y);
        var tileB = state.GetTile(to.X, to.Y);
        var tileAId = tileA.Id;
        var tileBId = tileB.Id;

        // Check if either tile is a bomb or color bomb (before swap)
        bool tileAIsBomb = tileA.Type.IsBomb();
        bool tileBIsBomb = tileB.Type.IsBomb();
        bool tileAIsColorBomb = tileA.Type.IsColorBomb();
        bool tileBIsColorBomb = tileB.Type.IsColorBomb();
        bool hasSpecialMove = tileAIsBomb || tileBIsBomb;

        // Swap tiles in grid using shared operations
        _swapOperations.SwapTiles(ref state, from, to);

        // Check if swap creates a match (check both positions)
        var hadMatch = _swapOperations.HasMatch(in state, from) || _swapOperations.HasMatch(in state, to);

        // If there's a bomb involved, treat as valid move (no revert)
        // Bomb effects will be processed AFTER swap animation completes
        if (hasSpecialMove)
        {
            hadMatch = true;
        }

        // Track pending move for potential revert (or bomb processing)
        pendingMoveState = new PendingMoveState
        {
            From = from,
            To = to,
            TileAId = tileAId,
            TileBId = tileBId,
            HadMatch = hadMatch,
            NeedsValidation = true,
            AnimationTime = 0f,
            // Store bomb swap info for delayed processing
            IsBombSwap = hasSpecialMove,
            TileAIsBomb = tileAIsBomb,
            TileBIsBomb = tileBIsBomb,
            TileAIsColorBomb = tileAIsColorBomb,
            TileBIsColorBomb = tileBIsColorBomb
        };

        // Save swap positions for bomb generation priority
        // Note: 'from' is where player started drag, 'to' is destination
        // After swap, tiles have swapped places, so:
        // - Original tile at 'from' is now at 'to'
        // - Original tile at 'to' is now at 'from'
        // Per bomb-generation.md: bomb should spawn at player's "touched" positions
        lastSwapFrom = from;
        lastSwapTo = to;

        // Emit swap event
        if (eventCollector.IsEnabled)
        {
            eventCollector.Emit(new TilesSwappedEvent
            {
                Tick = currentTick,
                SimulationTime = elapsedTime,
                TileAId = tileAId,
                TileBId = tileBId,
                PositionA = from,
                PositionB = to,
                IsRevert = false
            });
        }

        return true;
    }

    /// <summary>
    /// Handle a tap interaction at the specified position.
    /// Handles bomb activation (single tap), selection, and swap logic.
    /// </summary>
    /// <param name="state">Current game state — modified in-place.</param>
    /// <param name="pendingMoveState">Pending move state — passed through to ApplyMove if swap occurs.</param>
    /// <param name="lastSwapFrom">Swap origin — passed through to ApplyMove if swap occurs.</param>
    /// <param name="lastSwapTo">Swap destination — passed through to ApplyMove if swap occurs.</param>
    /// <param name="currentTick">Current simulation tick for event timestamps.</param>
    /// <param name="elapsedTime">Elapsed simulation time for event timestamps.</param>
    /// <param name="eventCollector">Event collector for event emission.</param>
    /// <param name="bombsActivated">Counter incremented when a bomb is activated via tap.</param>
    public void HandleTap(
        Position p,
        ref GameState state,
        ref PendingMoveState pendingMoveState,
        ref Position lastSwapFrom,
        ref Position lastSwapTo,
        int currentTick,
        float elapsedTime,
        IEventCollector eventCollector,
        ref int bombsActivated)
    {
        if (!state.IsValid(p)) return;

        var tile = state.GetTile(p.X, p.Y);

        // 1. Check for Bomb — single tap activates bomb directly
        if (tile.Type.IsBomb())
        {
            _powerUpHandler.ActivateBomb(ref state, p, currentTick, elapsedTime, eventCollector);
            bombsActivated++;
            return;
        }

        // 2. Handle selection logic
        if (state.SelectedPosition == Position.Invalid)
        {
            // Nothing selected — select this tile (if not blocked by cover)
            if (state.CanInteract(p))
            {
                state.SelectedPosition = p;
            }
        }
        else if (state.SelectedPosition == p)
        {
            // Same tile tapped — deselect
            state.SelectedPosition = Position.Invalid;
        }
        else if (IsNeighbor(state.SelectedPosition, p))
        {
            // Adjacent tile tapped — swap via ApplyMove
            var from = state.SelectedPosition;
            state.SelectedPosition = Position.Invalid;
            ApplyMove(from, p, ref state, ref pendingMoveState,
                ref lastSwapFrom, ref lastSwapTo,
                currentTick, elapsedTime, eventCollector);
            return;
        }
        else
        {
            // Non-adjacent tile tapped — change selection (if not blocked by cover)
            if (state.CanInteract(p))
            {
                state.SelectedPosition = p;
            }
        }
    }

    /// <summary>
    /// Process bomb swap effects after swap animation completes.
    /// Tries combo first (color bomb + normal, bomb + bomb, color bomb + bomb),
    /// then falls back to single bomb activation.
    /// </summary>
    /// <param name="state">Current game state — modified in-place.</param>
    /// <param name="from">Original swap source position.</param>
    /// <param name="to">Original swap destination position.</param>
    /// <param name="tileAIsBomb">Whether the tile originally at 'from' is a bomb.</param>
    /// <param name="tileBIsBomb">Whether the tile originally at 'to' is a bomb.</param>
    /// <param name="tileAIsColorBomb">Whether the tile originally at 'from' is a color bomb.</param>
    /// <param name="tileBIsColorBomb">Whether the tile originally at 'to' is a color bomb.</param>
    /// <param name="currentTick">Current simulation tick for event timestamps.</param>
    /// <param name="elapsedTime">Elapsed simulation time for event timestamps.</param>
    /// <param name="eventCollector">Event collector for event emission.</param>
    /// <param name="bombsActivated">Counter incremented when a bomb is activated.</param>
    public void ProcessBombSwap(
        ref GameState state,
        Position from,
        Position to,
        bool tileAIsBomb,
        bool tileBIsBomb,
        bool tileAIsColorBomb,
        bool tileBIsColorBomb,
        int currentTick,
        float elapsedTime,
        IEventCollector eventCollector,
        ref int bombsActivated)
    {
        // After swap:
        // - Original tile A (from) is now at position 'to'
        // - Original tile B (to) is now at position 'from'

        // Try combo first (handles: color bomb + normal, bomb + bomb, color bomb + bomb)
        _powerUpHandler.ProcessSpecialMove(
            ref state, from, to, currentTick, elapsedTime, eventCollector, out int points);

        if (points > 0)
        {
            // Combo was processed
            bombsActivated++;
            return;
        }

        // If no combo, activate single bomb
        // After swap:
        // - If tileA was bomb, it's now at 'to'
        // - If tileB was bomb, it's now at 'from'
        if (tileAIsBomb && !tileBIsBomb && !tileBIsColorBomb)
        {
            // Single bomb A + normal B: activate bomb at 'to' (where A is now)
            _powerUpHandler.ActivateBomb(ref state, to, currentTick, elapsedTime, eventCollector);
            bombsActivated++;
        }
        else if (tileBIsBomb && !tileAIsBomb && !tileAIsColorBomb)
        {
            // Single bomb B + normal A: activate bomb at 'from' (where B is now)
            _powerUpHandler.ActivateBomb(ref state, from, currentTick, elapsedTime, eventCollector);
            bombsActivated++;
        }
    }

    /// <summary>
    /// Check if two positions are orthogonally adjacent (Manhattan distance == 1).
    /// </summary>
    internal static bool IsNeighbor(Position a, Position b)
    {
        return System.Math.Abs(a.X - b.X) + System.Math.Abs(a.Y - b.Y) == 1;
    }
}
