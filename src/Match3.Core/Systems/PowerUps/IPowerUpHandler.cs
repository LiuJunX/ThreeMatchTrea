using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.PowerUps;

public interface IPowerUpHandler
{
    void ProcessSpecialMove(ref GameState state, Position p1, Position p2, out int points);

    void ProcessSpecialMove(
        ref GameState state,
        Position p1,
        Position p2,
        long tick,
        float simTime,
        IEventCollector events,
        out int points);

    void ActivateBomb(ref GameState state, Position p);

    void ActivateBomb(ref GameState state, Position p, long tick, float simTime, IEventCollector events);
}
