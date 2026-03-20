using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Events;

/// <summary>
/// Event emitted when a generator obstacle (e.g., Mailbox) activates and spawns a product tile.
/// </summary>
public sealed record GeneratorActivatedEvent : GameEvent
{
    /// <summary>Grid position of the generator obstacle.</summary>
    public Position GridPosition { get; init; }

    /// <summary>Type of the generator obstacle (e.g., Mailbox).</summary>
    public ObstacleType ObstacleType { get; init; }

    /// <summary>Type of the spawned product (e.g., Envelope).</summary>
    public ElementType ProductType { get; init; }

    /// <summary>Grid position where the product was spawned.</summary>
    public Position ProductPosition { get; init; }

    /// <inheritdoc />
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}
