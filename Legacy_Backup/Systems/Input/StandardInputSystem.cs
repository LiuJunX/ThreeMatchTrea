using Match3.Core.Interfaces;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Input;

public class StandardInputSystem : IInputSystem
{
    public bool IsValidPosition(in GameState state, Position p)
    {
        return p.X >= 0 && p.X < state.Width && p.Y >= 0 && p.Y < state.Height;
    }

    public Position GetSwipeTarget(Position from, Direction direction)
    {
        return direction switch
        {
            Direction.Up => new Position(from.X, from.Y - 1),
            Direction.Down => new Position(from.X, from.Y + 1),
            Direction.Left => new Position(from.X - 1, from.Y),
            Direction.Right => new Position(from.X + 1, from.Y),
            _ => from
        };
    }
}
