using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Layers;
using Match3.Core.Systems.Objectives;
using Match3.Core.Systems.Obstacles;
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

    /// <summary>Layer system reference — exposed for StandardMatchProcessor creation.</summary>
    public ICoverSystem? CoverSystem { get; init; }

    /// <summary>Explosion-path ObstacleSystem (with lockScheduler).</summary>
    public IObstacleSystem? ObstacleSystem { get; init; }

    private readonly ILevelObjectiveSystem? _objectiveSystem;

    public SimulationContext(
        ICellEliminator cellEliminator,
        BombEffectRegistry bombEffectRegistry,
        LockScheduler lockScheduler)
    {
        CellEliminator = cellEliminator;
        BombEffectRegistry = bombEffectRegistry;
        LockScheduler = lockScheduler;
    }

    private SimulationContext(
        ICellEliminator cellEliminator,
        BombEffectRegistry bombEffectRegistry,
        LockScheduler lockScheduler,
        ILevelObjectiveSystem? objectiveSystem)
        : this(cellEliminator, bombEffectRegistry, lockScheduler)
    {
        _objectiveSystem = objectiveSystem;
    }

    /// <summary>
    /// Factory for analysis services — creates all subsystems from scratch.
    /// Future system additions only need to update <see cref="Build"/>.
    /// </summary>
    public static SimulationContext Create(ILevelObjectiveSystem? objectiveSystem = null) =>
        Build(new LockScheduler(), objectiveSystem);

    /// <summary>
    /// Creates a context with a cloned LockScheduler — used by SimulationEngine.Clone().
    /// </summary>
    public static SimulationContext CloneFrom(LockScheduler source, ILevelObjectiveSystem? objectiveSystem = null) =>
        Build(source.Clone(), objectiveSystem);

    /// <summary>
    /// Creates a coherent clone for parallel simulation.
    /// CoverSystem/GroundSystem/CellEliminator/ObstacleSystem are new instances;
    /// LockScheduler is cloned; BombEffectRegistry is stateless and shared.
    /// </summary>
    public SimulationContext Clone(ILevelObjectiveSystem? objectiveSystem = null) =>
        CloneFrom(LockScheduler, objectiveSystem);

    /// <summary>
    /// Creates systems for match processing.
    /// CellEliminator has no objectiveSystem (avoids double-counting with EmitTileDestroyedEvents).
    /// ObstacleSystem has no lockScheduler (match path doesn't participate in engine lock lifecycle).
    /// </summary>
    public (ICellEliminator Eliminator, IObstacleSystem? Obstacle, ICoverSystem? Cover) CreateMatchSystems()
    {
        var cover = CoverSystem ?? new CoverSystem();
        var ground = new GroundSystem(_objectiveSystem);
        // No lockScheduler — only the engine's lockScheduler should manage lock lifecycle
        var obstacle = new ObstacleSystem(_objectiveSystem);
        var elim = new CellEliminator(cover, ground, null, obstacle);
        return (elim, obstacle, cover);
    }

    /// <summary>
    /// Shared system creation logic. Both Create() and CloneFrom() funnel through here.
    /// When adding a new system, update only this method.
    /// </summary>
    private static SimulationContext Build(LockScheduler lockScheduler, ILevelObjectiveSystem? objectiveSystem)
    {
        var cover = new CoverSystem(objectiveSystem);
        var ground = new GroundSystem(objectiveSystem);
        var obstacle = new ObstacleSystem(objectiveSystem, lockScheduler);
        var elim = new CellEliminator(cover, ground, objectiveSystem, obstacle);
        return new SimulationContext(elim, BombEffectRegistry.CreateDefault(), lockScheduler, objectiveSystem)
        {
            CoverSystem = cover,
            ObstacleSystem = obstacle
        };
    }
}
