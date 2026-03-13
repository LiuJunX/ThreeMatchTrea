using System;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.View;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.Input;

public class StandardInputSystem : IInputSystem
{
    public event Action<Position>? TapDetected;
    public event Action<Position, Direction>? SwipeDetected;

    private double _cellSize = 1.0; // Default, set via Configure
    private const double HoldDurationSeconds = 2.0;
    private const double DiagonalDeadZoneDegrees = 10.0;

    // State
    private double? _dragStartX;
    private double? _dragStartY;
    private int _dragSourceX = -1;
    private int _dragSourceY = -1;
    private DateTime _pointerDownTime;

    public void Configure(double cellSize)
    {
        _cellSize = cellSize;
    }

    public void OnPointerDown(int gridX, int gridY, double screenX, double screenY)
    {
        _dragStartX = screenX;
        _dragStartY = screenY;
        _dragSourceX = gridX;
        _dragSourceY = gridY;
        _pointerDownTime = DateTime.UtcNow;
    }

    public void OnPointerMove(double screenX, double screenY)
    {
        // Optional: Track current drag for visual feedback
    }

    public void OnPointerUp(double screenX, double screenY)
    {
        if (_dragStartX == null || _dragStartY == null || _dragSourceX == -1) return;

        var deltaX = screenX - _dragStartX.Value;
        var deltaY = screenY - _dragStartY.Value;
        var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        var duration = (DateTime.UtcNow - _pointerDownTime).TotalSeconds;

        // 1. Hold to Cancel
        if (duration > HoldDurationSeconds)
        {
            ResetDrag();
            return;
        }

        // 2. Dynamic Threshold
        var threshold = _cellSize * 0.5;

        if (distance < threshold)
        {
            // Tap
            TapDetected?.Invoke(new Position(_dragSourceX, _dragSourceY));
        }
        else
        {
            // Swipe
            // 3. Diagonal Cancel Check
            if (IsDiagonalCancel(deltaX, deltaY))
            {
                ResetDrag();
                return;
            }

            var direction = GetSwipeDirection(deltaX, deltaY);
            SwipeDetected?.Invoke(new Position(_dragSourceX, _dragSourceY), direction);
        }

        ResetDrag();
    }

    private void ResetDrag()
    {
        _dragStartX = null;
        _dragStartY = null;
        _dragSourceX = -1;
        _dragSourceY = -1;
    }

    private bool IsDiagonalCancel(double dx, double dy)
    {
        var radians = Math.Atan2(dy, dx);
        var degrees = radians * (180 / Math.PI);
        if (degrees < 0) degrees += 360;

        double tolerance = DiagonalDeadZoneDegrees;

        bool Check(double target) => degrees >= (target - tolerance) && degrees <= (target + tolerance);

        return Check(45) || Check(135) || Check(225) || Check(315);
    }
    
    private Direction GetSwipeDirection(double dx, double dy)
    {
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            return dx > 0 ? Direction.Right : Direction.Left;
        }
        else
        {
            return dy > 0 ? Direction.Down : Direction.Up;
        }
    }

    public bool IsValidPosition(in GameState state, Position p)
    {
        return state.IsValid(p);
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
