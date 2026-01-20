namespace Match3.Core.Events;

/// <summary>
/// Visitor pattern interface for exhaustive event handling.
/// Compiler enforces implementation of all event types, ensuring type safety.
/// </summary>
public interface IEventVisitor
{
    /// <summary>Visit a tile moved event.</summary>
    void Visit(TileMovedEvent evt);

    /// <summary>Visit a tile destroyed event.</summary>
    void Visit(TileDestroyedEvent evt);

    /// <summary>Visit a tile spawned event.</summary>
    void Visit(TileSpawnedEvent evt);

    /// <summary>Visit a tiles swapped event.</summary>
    void Visit(TilesSwappedEvent evt);

    /// <summary>Visit a match detected event.</summary>
    void Visit(MatchDetectedEvent evt);

    /// <summary>Visit a bomb created event.</summary>
    void Visit(BombCreatedEvent evt);

    /// <summary>Visit a bomb activated event.</summary>
    void Visit(BombActivatedEvent evt);

    /// <summary>Visit a bomb combo event.</summary>
    void Visit(BombComboEvent evt);

    /// <summary>Visit a score added event.</summary>
    void Visit(ScoreAddedEvent evt);

    /// <summary>Visit a combo changed event.</summary>
    void Visit(ComboChangedEvent evt);

    /// <summary>Visit a move completed event.</summary>
    void Visit(MoveCompletedEvent evt);

    /// <summary>Visit a projectile launched event.</summary>
    void Visit(ProjectileLaunchedEvent evt);

    /// <summary>Visit a projectile moved event.</summary>
    void Visit(ProjectileMovedEvent evt);

    /// <summary>Visit a projectile retargeted event.</summary>
    void Visit(ProjectileRetargetedEvent evt);

    /// <summary>Visit a projectile impact event.</summary>
    void Visit(ProjectileImpactEvent evt);

    /// <summary>Visit a cover destroyed event.</summary>
    void Visit(CoverDestroyedEvent evt);

    /// <summary>Visit a ground destroyed event.</summary>
    void Visit(GroundDestroyedEvent evt);

    /// <summary>Visit a deadlock detected event.</summary>
    void Visit(DeadlockDetectedEvent evt);

    /// <summary>Visit a board shuffled event.</summary>
    void Visit(BoardShuffledEvent evt);

    /// <summary>Visit an objective progress event.</summary>
    void Visit(ObjectiveProgressEvent evt);

    /// <summary>Visit a level completed event.</summary>
    void Visit(LevelCompletedEvent evt);
}
