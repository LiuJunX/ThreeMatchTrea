using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Projectiles;
using Match3.Core.Systems.Projectiles.Targeting;

namespace Match3.Core.Systems.PowerUps.Effects;

public class UfoEffect : IBombEffect
{
    public ElementType Type => ElementType.Ufo;

    public void Apply(in GameState state, Position origin, HashSet<Position> affectedTiles)
    {
        // UFO 起飞时：以自身为中心最小十字消除（上下左右�?格）
        BombComboHelpers.ApplySmallCross(in state, origin, affectedTiles);

        // Remote target handled by ProjectileSystem (deferred destruction with dynamic tracking)
    }

    /// <summary>
    /// Pick the best remote target for the UFO projectile.
    /// Uses smart targeting: layer-stack evaluation with tier-based scoring.
    /// For payload UFOs, evaluates total range value at each candidate drop point.
    /// In-flight deduplication is handled via <paramref name="projectileSystem"/>.
    /// </summary>
    public static Position? PickRemoteTarget(
        in GameState state, Position origin,
        UfoPayload payload = UfoPayload.Default,
        IProjectileSystem? projectileSystem = null)
    {
        return UfoTargetSelector.SelectTarget(
            in state,
            origin,
            UfoTargetConfig.Default,
            projectileSystem,
            payload);
    }
}
