# ADR 001: Slot-Based Grid Architecture

## Status
Accepted

## Context
The initial implementation of the Match3 engine used a "Struct-based Grid" approach where `Tile` was a simple data structure. While performance-friendly, this design proved insufficient for complex gameplay requirements such as:
- Multi-layered interactions (e.g., Ice over a Gem).
- Items occupying multiple cells (2x2 obstacles).
- Separate logic for "Ground" elements (Jelly) vs "Unit" elements (Gems).
- Future extensibility for new mechanics without modifying the core struct.

## Decision
We have transitioned to a **Slot-Based Layered Architecture** using a `Match3Cell` class as the fundamental container.

### Key Changes
1.  **From Struct to Class**: `Match3Cell` is a reference type containing references to elements in different layers.
2.  **Explicit Layers**: Each cell has 5 dedicated slots:
    - **Topology**: Immutable properties (Wall, Spawner).
    - **Ground**: Floor elements (Jelly).
    - **Unit**: Movable items (Gems, Bombs).
    - **Cover**: Obstacles (Ice, Cages).
    - **Aux**: Temporary effects.
3.  **Interface-Driven**: All elements implement `IGridElement` and specific layer interfaces (`IUnitElement`, `ICoverElement`).
4.  **Unified Damage Model**: All destroyable elements implement `IDamageable`, simplifying the matching and destruction logic.

## Consequences

### Positive
- **Flexibility**: New layers or item types can be added without changing the grid structure.
- **Clarity**: "What is in this cell?" is now explicitly answered by checking specific slots, rather than parsing a complex state enum.
- **Logic Separation**: Gravity affects `Unit` layer; Matching affects `Unit` layer; Explosions affect all layers via `IDamageable`.

### Negative
- **Memory Overhead**: Moving from a flat struct array to an array of objects increases memory usage and GC pressure. However, for a typical Match3 board (e.g., 9x9 to 12x12), this overhead is negligible compared to the architectural benefits.
- **Pointer Chasing**: Accessing an item now requires an extra indirection (`Grid -> Cell -> Item`), which is slightly slower than direct struct access, but acceptable for this genre.
