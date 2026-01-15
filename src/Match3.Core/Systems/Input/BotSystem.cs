using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching;

namespace Match3.Core.Systems.Input;

public class BotSystem : IBotSystem
{
    private static readonly Direction[] Directions = { Direction.Up, Direction.Down, Direction.Left, Direction.Right };
    private readonly IMatchFinder _matchFinder;

    public BotSystem(IMatchFinder matchFinder)
    {
        _matchFinder = matchFinder;
    }

    public bool TryGetRandomMove(ref GameState state, IInteractionSystem interactionSystem, out Move move)
    {
        move = default;
        // Simple random move logic for AutoPlay
        // Try random positions and directions
        int attempts = 20;
        var w = state.Width;
        var h = state.Height;
        
        for (int i = 0; i < attempts; i++)
        {
            int x = state.Random.Next(0, w);
            int y = state.Random.Next(0, h);
            var p = new Position(x, y);
            
            // Try 4 directions
            foreach (var d in Directions)
            {
                // Simulate swipe
                if (interactionSystem.TryHandleSwipe(ref state, p, d, true, out var candidate))
                {
                     if (candidate.HasValue)
                     {
                          // Check if this move creates a match
                          SwapTiles(ref state, candidate.Value.From, candidate.Value.To);
                          bool hasMatch = _matchFinder.HasMatchAt(in state, candidate.Value.From) || 
                                          _matchFinder.HasMatchAt(in state, candidate.Value.To);
                          SwapTiles(ref state, candidate.Value.From, candidate.Value.To); // Swap back

                          if (hasMatch)
                          {
                              move = candidate.Value;
                              return true;
                          }
                     }
                }
            }
        }
        return false;
    }
    
    private void SwapTiles(ref GameState state, Position a, Position b)
    {
        var idxA = a.Y * state.Width + a.X;
        var idxB = b.Y * state.Width + b.X;
        var tA = state.Grid[idxA];
        var tB = state.Grid[idxB];

        // Swap positions
        var tempPos = tA.Position;
        tA.Position = tB.Position;
        tB.Position = tempPos;

        // Swap in grid
        state.Grid[idxA] = tB;
        state.Grid[idxB] = tA;
    }
}
