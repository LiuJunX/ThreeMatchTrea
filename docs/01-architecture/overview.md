# Match3 Core Architecture

## Overview
The Match3 Core is the heart of the game engine, designed with a **Slot-Based Layered Architecture**. It strictly adheres to the principle of separation of concerns, ensuring that game logic is decoupled from the view layer.

## 1. Grid System
The game board is represented by `Match3Grid`, which manages a 2D array of `Match3Cell` objects. Unlike traditional single-layer grids, each cell is a **multi-layered container**.

### The 5-Layer Slot Model
Each `Match3Cell` contains specific slots for different types of elements. This enforces the "One Item Per Layer" rule physically.

| Layer | Property | Interface | Description |
| :--- | :--- | :--- | :--- |
| **1. Topology** | `Topology` | `TileType` (Enum) | Physical properties of the cell (Wall, Spawner, Hole, Sink). Stored as a BitMask. |
| **2. Ground** | `Ground` | `IGroundElement` | Elements sitting on the "floor" (e.g., Jelly, Carpet). They do not move with gravity. |
| **3. Unit** | `Unit` | `IUnitElement` | The main playable items (e.g., Color Blocks, Bombs). Subject to gravity and matching. |
| **4. Cover** | `Cover` | `ICoverElement` | Obstacles covering the unit (e.g., Ice, Cages). Can be Static (fixed) or Dynamic (moves with unit). |
| **5. Aux** | `Aux` | `IAuxElement` | Reserved for future expansions (e.g., temporary status effects, markers). |

### Coordinate System
- **Vector2Int**: A fundamental primitive used for grid coordinates `(x, y)`.
- **Origin**: `(0, 0)` is typically the bottom-left or top-left (implementation dependent, currently logical).

## 2. Element Interfaces
All items on the grid implement specific interfaces to define their behavior.

### Core Interfaces
- **`IGridElement`**: The base contract. Defines `Size` (default 1x1).
- **`IDamageable`**: The Unified Damage Model.
    - `int Health { get; }`
    - `int MaxHealth { get; }`
    - `bool TakeDamage(int amount)`

### Specific Interfaces
- **`IMatchable`**: For items that participate in color matching (e.g., `UnitNormalItem`).
- **`IUnitElement`**: Marker for items in the Unit layer.
- **`ICoverElement`**: Defines `AttachmentMode` (Static/Dynamic).

## 3. Core Systems (Planned)
- **MatchFinder**: Scans `Unit` layer for matches based on `IMatchable`.
- **GravitySystem**: Moves items in the `Unit` layer (and attached `Dynamic` covers) down to empty spaces.
- **InteractionSystem**: Handles swaps between `Unit` elements, respecting `Cover` constraints.

## 4. Key Design Decisions
- **OOP over Pure DOD**: We use classes (`Match3Cell`) and interfaces to handle the complexity of multi-layered interactions, favoring maintainability and flexibility over raw struct-array performance for this specific component.
- **Unified Damage**: Clearing a block, breaking ice, or spreading jelly are all treated as `TakeDamage` events.
