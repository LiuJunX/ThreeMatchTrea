using System.Collections.Generic;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.PowerUps.ColorBomb;
using Match3.Core.Systems.Scoring;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.PowerUps;

/// <summary>
/// Pure routing and scoring for bomb swap interactions.
/// Detects combo type, emits events, and delegates execution to <see cref="BombResolution"/>.
/// </summary>
public sealed class BombDispatcher
{
    private readonly IScoreSystem _scoreSystem;
    private readonly BombComboHandler _comboHandler;
    private readonly BombResolution _resolution;
    private readonly IColorBombSessionManager? _colorBombSessionManager;

    internal BombDispatcher(
        IScoreSystem scoreSystem,
        BombComboHandler comboHandler,
        BombResolution resolution,
        IColorBombSessionManager? colorBombSessionManager)
    {
        _scoreSystem = scoreSystem;
        _comboHandler = comboHandler;
        _resolution = resolution;
        _colorBombSessionManager = colorBombSessionManager;
    }

    public void ProcessBombSwap(ref GameState state, Position p1, Position p2, out int points)
    {
        ProcessBombSwap(ref state, p1, p2, 0, 0f, NullEventCollector.Instance, out points);
    }

    /// <summary>
    /// Processes a bomb swap between two positions.
    /// </summary>
    /// <remarks>
    /// <para><strong>Combo detection priority:</strong></para>
    /// <list type="number">
    /// <item>ColorBomb + other bomb (non-ColorBomb) — routed to session-based beam flow</item>
    /// <item>ColorBomb + normal tile — routed to session-based flow with target color</item>
    /// <item><see cref="BombComboHandler"/> — handles all remaining combos including ColorBomb + ColorBomb</item>
    /// </list>
    /// </remarks>
    public void ProcessBombSwap(
        ref GameState state,
        Position p1,
        Position p2,
        int tick,
        float simTime,
        IEventCollector events,
        out int points)
    {
        points = 0;
        var t1 = state.GetTile(p1.X, p1.Y);
        var t2 = state.GetTile(p2.X, p2.Y);

        // Calculate score before modifying state (tiles might be cleared)
        points = _scoreSystem.CalculateSpecialMoveScore(t1.Type, t2.Type);

        // ColorBomb + other bomb combo → route to session-based flow
        if (_colorBombSessionManager != null && IsColorBombWithOtherBomb(t1.Type, t2.Type))
        {
            var colorBombPos = t1.Type.IsColorBomb() ? p1 : p2;
            var colorBombTile = t1.Type.IsColorBomb() ? t1 : t2;
            var otherBombType = t1.Type.IsColorBomb() ? t2.Type : t1.Type;

            if (events.IsEnabled)
            {
                events.Emit(new BombComboEvent
                {
                    Tick = tick,
                    SimulationTime = simTime,
                    TileIdA = t1.Id,
                    TileIdB = t2.Id,
                    BombTypeA = t1.Type,
                    BombTypeB = t2.Type,
                    PositionA = p1,
                    PositionB = p2
                });
            }

            _resolution.ConsumeBomb(ref state, p1, tick, simTime, events);
            _resolution.ConsumeBomb(ref state, p2, tick, simTime, events);

            _colorBombSessionManager.CreateComboSession(
                ref state, colorBombPos, colorBombTile.Id, otherBombType, tick, simTime, events);

            return;
        }

        // ColorBomb + normal tile → route to session-based flow with specified target color
        if (_colorBombSessionManager != null && BombComboHelpers.IsColorBombWithNormalTile(t1, t2))
        {
            var colorBombPos = t1.Type.IsColorBomb() ? p1 : p2;
            var colorBombTile = t1.Type.IsColorBomb() ? t1 : t2;
            var normalTile = t1.Type.IsColorBomb() ? t2 : t1;

            _resolution.ConsumeBomb(ref state, colorBombPos, tick, simTime, events);

            _colorBombSessionManager.CreateSession(
                ref state, colorBombPos, colorBombTile.Id, normalTile.Type, tick, simTime, events);

            return;
        }

        // Use BombComboHandler to process non-ColorBomb combos (and ColorBomb+ColorBomb)
        var affected = Pools.ObtainHashSet<Position>();
        try
        {
            if (_comboHandler.TryApplyCombo(ref state, p1, p2, affected, out var comboResult))
            {
                if (events.IsEnabled)
                {
                    events.Emit(new BombComboEvent
                    {
                        Tick = tick,
                        SimulationTime = simTime,
                        TileIdA = t1.Id,
                        TileIdB = t2.Id,
                        BombTypeA = t1.Type,
                        BombTypeB = t2.Type,
                        PositionA = p1,
                        PositionB = p2,
                        AffectedPositions = new List<Position>(affected)
                    });
                }

                _resolution.ConsumeBomb(ref state, p1, tick, simTime, events);
                _resolution.ConsumeBomb(ref state, p2, tick, simTime, events);

                _resolution.ExecuteComboResult(
                    ref state, p1, p2, t1, t2, affected, comboResult, tick, simTime, events);

                return;
            }
        }
        finally
        {
            Pools.Release(affected);
        }

        // If no special move happened, reset points
        points = 0;
    }

    /// <summary>
    /// Check if one tile is a ColorBomb and the other is a non-ColorBomb bomb.
    /// ColorBomb+ColorBomb and ColorBomb+normal are NOT matched here.
    /// </summary>
    private static bool IsColorBombWithOtherBomb(ElementType a, ElementType b)
    {
        if (a == ElementType.ColorBomb && b.IsBomb() && b != ElementType.ColorBomb) return true;
        if (b == ElementType.ColorBomb && a.IsBomb() && a != ElementType.ColorBomb) return true;
        return false;
    }
}
