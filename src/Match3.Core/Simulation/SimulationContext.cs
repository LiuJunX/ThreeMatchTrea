using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Objectives;
using Match3.Core.Systems.PowerUps;

namespace Match3.Core.Simulation;

/// <summary>
/// Shared infrastructure for bomb-related subsystems.
/// Eliminates the triplicate of (CellEliminator, BombEffectRegistry, LockScheduler)
/// across PowerUpHandler, ExplosionSystem, and ColorBombSessionManager.
/// Clone() produces a coherent copy for parallel simulation (AI / DryRun).
/// </summary>
public sealed class SimulationContext
{
    public ICellEliminator CellEliminator { get; }
    public BombEffectRegistry BombEffectRegistry { get; }
    public LockScheduler LockScheduler { get; }

    public SimulationContext(
        ICellEliminator cellEliminator,
        BombEffectRegistry bombEffectRegistry,
        LockScheduler lockScheduler)
    {
        CellEliminator = cellEliminator;
        BombEffectRegistry = bombEffectRegistry;
        LockScheduler = lockScheduler;
    }

    /// <summary>
    /// Creates a coherent clone for parallel simulation.
    /// CoverSystem/GroundSystem/CellEliminator are new instances;
    /// LockScheduler is cloned; BombEffectRegistry is stateless and shared.
    /// </summary>
    public SimulationContext Clone(ILevelObjectiveSystem? objectiveSystem = null)
    {
        var clonedLocks = LockScheduler.Clone();
        var cover = new CoverSystem(objectiveSystem);
        var ground = new GroundSystem(objectiveSystem);
        var clonedElim = new CellEliminator(cover, ground, objectiveSystem);
        return new SimulationContext(clonedElim, BombEffectRegistry, clonedLocks);
    }
}
