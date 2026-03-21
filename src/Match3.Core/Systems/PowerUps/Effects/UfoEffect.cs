using System;
using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Projectiles;
using Match3.Core.Systems.Projectiles.Targeting;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.PowerUps.Effects;

public class UfoEffect : IBombEffect
{
    public ElementType Type => ElementType.Ufo;

    public void Apply(in GameState state, Position origin, HashSet<Position> affectedTiles)
    {
        // UFO 起飞时：以自身为中心最小十字消除（上下左右各1格）
        BombComboHelpers.ApplySmallCross(in state, origin, affectedTiles);

        // Remote target handled by ProjectileSystem (deferred destruction with dynamic tracking)
    }

    /// <summary>
    /// Pick the best remote target for the UFO projectile (outside the cross area).
    /// Uses smart targeting: layer-stack evaluation with tier-based scoring.
    /// For payload UFOs, evaluates total range value at each candidate drop point.
    /// </summary>
    public static Position? PickRemoteTarget(
        in GameState state, Position origin, UfoPayload payload = UfoPayload.Default)
    {
        // Build exclude area (small cross around origin)
        var excludeArea = Pools.ObtainHashSet<Position>();
        try
        {
            excludeArea.Add(origin);
            if (origin.X > 0) excludeArea.Add(new Position(origin.X - 1, origin.Y));
            if (origin.X < state.Width - 1) excludeArea.Add(new Position(origin.X + 1, origin.Y));
            if (origin.Y > 0) excludeArea.Add(new Position(origin.X, origin.Y - 1));
            if (origin.Y < state.Height - 1) excludeArea.Add(new Position(origin.X, origin.Y + 1));

            return UfoTargetSelector.SelectTarget(
                in state,
                origin,
                ReadOnlySpan<PendingAttack>.Empty,
                UfoTargetConfig.Default,
                excludeArea,
                payload);
        }
        finally
        {
            Pools.Release(excludeArea);
        }
    }
}
