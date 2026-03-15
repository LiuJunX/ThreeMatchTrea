using Match3.Core.Events;
using Match3.Core.Models.Grid;

namespace Match3.Core.Systems.PowerUps;

/// <summary>
/// Default implementation that delegates to <see cref="IPowerUpHandler"/>.
/// </summary>
public sealed class ChainReactionHandler : IChainReactionHandler
{
    private readonly IPowerUpHandler _powerUpHandler;

    public ChainReactionHandler(IPowerUpHandler powerUpHandler)
    {
        _powerUpHandler = powerUpHandler;
    }

    public void HandleExplosionChain(ref GameState state, Position pos, Tile bombTile,
        int tick, float simTime, IEventCollector events)
    {
        _powerUpHandler.ActivateChainBomb(ref state, pos, bombTile, tick, simTime, events);
    }

    public void HandleImpactChain(ref GameState state, Position pos,
        int tick, float simTime, IEventCollector events)
    {
        _powerUpHandler.ActivateBomb(ref state, pos, tick, simTime, events, isChainReaction: true);
    }
}
