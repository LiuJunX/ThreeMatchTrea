using Match3.Core.Events;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.PowerUps.Effects;
using Match3.Core.Systems.Projectiles;
using Match3.Core.Systems.Projectiles.Targeting;

namespace Match3.Core.Systems.PowerUps;

/// <summary>
/// Static helper for launching UFO projectiles during bomb combos.
/// Extracted from PowerUpHandler to reduce class complexity.
/// </summary>
internal static class UfoLaunchHelper
{
    /// <summary>
    /// Launch 3 UFO projectiles for UFO+UFO combo.
    /// Each UFO is launched before the next selects its target, so
    /// <see cref="IProjectileSystem.CountInFlightTargetsAt"/> naturally
    /// prevents multiple UFOs from wasting hits on the same cell.
    /// </summary>
    internal static void LaunchUfoComboProjectiles(
        IProjectileSystem projectileSystem,
        ref GameState state, int tileId1, int tileId2,
        Position p1, Position p2,
        int tick, float simTime, IEventCollector events)
    {
        // Random base angle for 180° fan spread (different each swap)
        float baseAngle = state.Random.Next(0, 360);

        // UFO 1: from p1 (reuses existing tile visual)
        var target1 = UfoEffect.PickRemoteTarget(in state, p1,
            projectileSystem: projectileSystem);
        if (target1.HasValue)
        {
            var proj = new UfoProjectile(
                projectileSystem.GenerateProjectileId(), p1, target1.Value)
            { SourceTileId = tileId1, ComboDivergeAngle = baseAngle };
            projectileSystem.Launch(proj, tick, simTime, events);
        }

        // UFO 2: from p2 (reuses existing tile visual)
        var target2 = UfoEffect.PickRemoteTarget(in state, p2,
            projectileSystem: projectileSystem);
        if (target2.HasValue)
        {
            var proj = new UfoProjectile(
                projectileSystem.GenerateProjectileId(), p2, target2.Value)
            { SourceTileId = tileId2, ComboDivergeAngle = baseAngle + 90f };
            projectileSystem.Launch(proj, tick, simTime, events);
        }

        // UFO 3: from p1 (spawns a new tile visual via Choreographer)
        var target3 = UfoEffect.PickRemoteTarget(in state, p1,
            projectileSystem: projectileSystem);
        if (target3.HasValue)
        {
            int syntheticTileId = state.NextTileId++;
            var proj = new UfoProjectile(
                projectileSystem.GenerateProjectileId(), p1, target3.Value)
            { SourceTileId = syntheticTileId, SpawnVisual = true, ComboDivergeAngle = baseAngle + 180f };
            projectileSystem.Launch(proj, tick, simTime, events);
        }
    }

    /// <summary>
    /// Launch a single UFO projectile with an enhanced payload (Rocket row/column or Square 5x5).
    /// Reuses the existing UFO tile visual for the flight animation.
    /// The passenger bomb's tile visual follows the UFO during flight.
    /// </summary>
    internal static void LaunchUfoPayloadProjectile(
        IProjectileSystem projectileSystem,
        ref GameState state, int ufoTileId, Position ufoPos,
        UfoPayload payload,
        int tick, float simTime, IEventCollector events,
        int? passengerTileId = null, Position? passengerPos = null)
    {
        var target = UfoEffect.PickRemoteTarget(in state, ufoPos, payload, projectileSystem);
        if (target.HasValue)
        {
            var proj = new UfoProjectile(
                projectileSystem.GenerateProjectileId(), ufoPos, target.Value)
            {
                SourceTileId = ufoTileId,
                Payload = payload,
                PassengerTileId = passengerTileId,
                PassengerOrigin = passengerPos
            };
            projectileSystem.Launch(proj, tick, simTime, events);
        }
    }
}
