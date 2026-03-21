using System;
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
    /// Each subsequent UFO is aware of the previous targets via PendingAttacks,
    /// preventing multiple UFOs from wasting hits on the same cell.
    /// </summary>
    internal static void LaunchUfoComboProjectiles(
        IProjectileSystem projectileSystem,
        ref GameState state, int tileId1, int tileId2,
        Position p1, Position p2,
        int tick, float simTime, IEventCollector events)
    {
        // Random base angle for 180° fan spread (different each swap)
        float baseAngle = state.Random.Next(0, 360);

        // Build pending attacks incrementally so each UFO avoids previous targets
        Span<PendingAttack> pending = stackalloc PendingAttack[3];
        int pendingCount = 0;

        // UFO 1: from p1 (reuses existing tile visual)
        var target1 = UfoEffect.PickRemoteTarget(in state, p1,
            pendingAttacks: pending[..pendingCount]);
        if (target1.HasValue)
        {
            pending[pendingCount++] = MakePending(target1.Value, in state);
            var proj = new UfoProjectile(
                projectileSystem.GenerateProjectileId(), p1, target1.Value)
            { SourceTileId = tileId1, ComboDivergeAngle = baseAngle };
            projectileSystem.Launch(proj, tick, simTime, events);
        }

        // UFO 2: from p2 (reuses existing tile visual)
        var target2 = UfoEffect.PickRemoteTarget(in state, p2,
            pendingAttacks: pending[..pendingCount]);
        if (target2.HasValue)
        {
            pending[pendingCount++] = MakePending(target2.Value, in state);
            var proj = new UfoProjectile(
                projectileSystem.GenerateProjectileId(), p2, target2.Value)
            { SourceTileId = tileId2, ComboDivergeAngle = baseAngle + 90f };
            projectileSystem.Launch(proj, tick, simTime, events);
        }

        // UFO 3: from p1 (spawns a new tile visual via Choreographer)
        var target3 = UfoEffect.PickRemoteTarget(in state, p1,
            pendingAttacks: pending[..pendingCount]);
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
    /// Create a PendingAttack for a target position.
    /// Determines the hit layer by checking which layer exists at the position.
    /// </summary>
    private static PendingAttack MakePending(Position target, in GameState state)
    {
        byte hitLayer = 2; // default: Tile
        if (state.HasCover(target) && state.GetCover(target).Type != Models.Enums.CoverType.None
            && state.GetCover(target).Type != Models.Enums.CoverType.Bubble)
            hitLayer = 0; // Cover
        else if (state.HasObstacle(target) && state.GetObstacle(target).Type != Models.Enums.ObstacleType.None)
            hitLayer = 1; // Obstacle
        else if (state.HasGround(target) && state.GetGround(target).Type != Models.Enums.GroundType.None
            && state.GetType(target) == Models.Enums.ElementType.None)
            hitLayer = 3; // Ground (only if no tile)

        return new PendingAttack
        {
            GridIndex = (ushort)(target.Y * state.Width + target.X),
            HitLayer = hitLayer
        };
    }

    /// <summary>
    /// Launch a single UFO projectile with an enhanced payload (Rocket row/column or Square 5x5).
    /// Reuses the existing UFO tile visual for the flight animation.
    /// The passenger bomb's tile visual follows the UFO during flight.
    /// </summary>
    /// <param name="projectileSystem">The projectile system used to generate IDs and launch projectiles.</param>
    /// <param name="state">Current game state (passed by ref for target picking).</param>
    /// <param name="ufoTileId">Tile ID of the UFO being launched.</param>
    /// <param name="ufoPos">Grid position the UFO launches from.</param>
    /// <param name="payload">The enhanced payload type (Row, Column, or Area5x5).</param>
    /// <param name="tick">Current simulation tick.</param>
    /// <param name="simTime">Current simulation time in seconds.</param>
    /// <param name="events">Event collector for emitting launch events.</param>
    /// <param name="passengerTileId">Optional tile ID of the bomb being dragged behind the UFO.</param>
    /// <param name="passengerPos">Optional grid position of the passenger bomb.</param>
    internal static void LaunchUfoPayloadProjectile(
        IProjectileSystem projectileSystem,
        ref GameState state, int ufoTileId, Position ufoPos,
        UfoPayload payload,
        int tick, float simTime, IEventCollector events,
        int? passengerTileId = null, Position? passengerPos = null)
    {
        var target = UfoEffect.PickRemoteTarget(in state, ufoPos, payload);
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
