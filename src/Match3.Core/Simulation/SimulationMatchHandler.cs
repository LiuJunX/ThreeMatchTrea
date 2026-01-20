using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Models.Gameplay;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Objectives;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Simulation;

/// <summary>
/// Handles match detection and processing within simulation.
/// Extracted from SimulationEngine to reduce class size.
/// </summary>
internal sealed class SimulationMatchHandler
{
    private readonly IMatchFinder _matchFinder;
    private readonly IMatchProcessor _matchProcessor;
    private readonly ILevelObjectiveSystem? _objectiveSystem;

    public SimulationMatchHandler(
        IMatchFinder matchFinder,
        IMatchProcessor matchProcessor,
        ILevelObjectiveSystem? objectiveSystem = null)
    {
        _matchFinder = matchFinder;
        _matchProcessor = matchProcessor;
        _objectiveSystem = objectiveSystem;
    }

    /// <summary>
    /// Check if there are pending matches in the current state.
    /// </summary>
    public bool HasPendingMatches(in GameState state)
    {
        return _matchFinder.HasMatches(in state);
    }

    /// <summary>
    /// Process stable matches and emit events.
    /// </summary>
    /// <param name="foci">Optional positions to prioritize for bomb spawn (e.g., swap positions).</param>
    /// <returns>Number of match groups processed.</returns>
    public int ProcessStableMatches(
        ref GameState state,
        long currentTick,
        float elapsedTime,
        IEventCollector eventCollector,
        IEnumerable<Position>? foci = null)
    {
        var allMatches = _matchFinder.FindMatchGroups(state, foci);
        if (allMatches.Count == 0) return 0;

        var stableGroups = Pools.ObtainList<MatchGroup>();
        int processed = 0;

        try
        {
            foreach (var group in allMatches)
            {
                if (IsGroupStable(ref state, group))
                {
                    stableGroups.Add(group);
                    EmitMatchDetectedEvent(group, currentTick, elapsedTime, eventCollector);
                }
            }

            if (stableGroups.Count > 0)
            {
                EmitTileDestroyedEvents(ref state, stableGroups, currentTick, elapsedTime, eventCollector);
                processed = stableGroups.Count;
                _matchProcessor.ProcessMatches(ref state, stableGroups);
            }
        }
        finally
        {
            Pools.Release(stableGroups);
        }

        return processed;
    }

    /// <summary>
    /// Process tiles affected by projectile impacts.
    /// </summary>
    public void ProcessProjectileImpacts(
        ref GameState state,
        HashSet<Position> affectedPositions,
        long currentTick,
        float elapsedTime,
        IEventCollector eventCollector)
    {
        foreach (var pos in affectedPositions)
        {
            if (!state.IsValid(pos)) continue;

            var tile = state.GetTile(pos.X, pos.Y);
            if (tile.Type == TileType.None) continue;

            if (eventCollector.IsEnabled)
            {
                eventCollector.Emit(new TileDestroyedEvent
                {
                    Tick = currentTick,
                    SimulationTime = elapsedTime,
                    TileId = tile.Id,
                    GridPosition = pos,
                    Type = tile.Type,
                    Bomb = tile.Bomb,
                    Reason = DestroyReason.Projectile
                });
            }

            // Track objective progress
            _objectiveSystem?.OnTileDestroyed(ref state, tile.Type, currentTick, elapsedTime, eventCollector);

            state.SetTile(pos.X, pos.Y, new Tile());
        }
    }

    private bool IsGroupStable(ref GameState state, MatchGroup group)
    {
        foreach (var p in group.Positions)
        {
            var tile = state.GetTile(p.X, p.Y);
            if (tile.IsFalling) return false;
        }
        return true;
    }

    private void EmitMatchDetectedEvent(
        MatchGroup group,
        long currentTick,
        float elapsedTime,
        IEventCollector eventCollector)
    {
        if (!eventCollector.IsEnabled) return;

        var positions = new List<Position>(group.Positions);
        eventCollector.Emit(new MatchDetectedEvent
        {
            Tick = currentTick,
            SimulationTime = elapsedTime,
            Type = group.Type,
            Positions = positions,
            Shape = DetermineMatchShape(group),
            TileCount = group.Positions.Count
        });
    }

    private void EmitTileDestroyedEvents(
        ref GameState state,
        List<MatchGroup> stableGroups,
        long currentTick,
        float elapsedTime,
        IEventCollector eventCollector)
    {
        foreach (var group in stableGroups)
        {
            foreach (var pos in group.Positions)
            {
                // Skip position that will spawn a bomb
                if (group.BombOrigin.HasValue && group.BombOrigin.Value == pos)
                    continue;

                var tile = state.GetTile(pos.X, pos.Y);
                if (tile.Type == TileType.None) continue;

                if (eventCollector.IsEnabled)
                {
                    eventCollector.Emit(new TileDestroyedEvent
                    {
                        Tick = currentTick,
                        SimulationTime = elapsedTime,
                        TileId = tile.Id,
                        GridPosition = pos,
                        Type = tile.Type,
                        Bomb = tile.Bomb,
                        Reason = DestroyReason.Match
                    });
                }

                // Track objective progress
                _objectiveSystem?.OnTileDestroyed(ref state, tile.Type, currentTick, elapsedTime, eventCollector);
            }
        }
    }

    private static MatchShape DetermineMatchShape(MatchGroup group)
    {
        if (group.Shape != default)
            return group.Shape;

        return group.Positions.Count switch
        {
            <= 3 => MatchShape.Simple3,
            4 => MatchShape.Line4Horizontal,
            _ => MatchShape.Line5
        };
    }
}
