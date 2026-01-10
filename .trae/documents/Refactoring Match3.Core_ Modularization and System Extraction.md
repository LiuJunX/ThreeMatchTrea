Based on the analysis of `Match3.Core` and your request for better file organization, I propose a comprehensive **Architecture & Structure Refactoring Plan**.

### **1. Structural Reorganization (Folder Restructuring)**
The current split between `Logic` and `Systems` is inconsistent. We will unify these and group files by domain.

*   **`Match3.Core/Models/`** (New Domain Layer)
    *   *Move `Structs/*` here.*
    *   *Move root Enums here*: `TileType.cs`, `BombType.cs`, `Direction.cs`, `Position.cs`.
    *   **Sub-folders**: `Models/Grid/` (GameState, Tile), `Models/Gameplay/` (MatchGroup, Move).
*   **`Match3.Core/Systems/`** (Unified Logic Layer)
    *   *Merge `Logic/` content here.*
    *   **Sub-folders**:
        *   `Systems/Gravity/`: `StandardGravitySystem.cs`
        *   `Systems/Matching/`: `ClassicMatchFinder.cs`, `StandardMatchProcessor.cs`
        *   `Systems/Input/`: `StandardInputSystem.cs`, `InteractionSystem.cs` (New)
        *   `Systems/Core/`: `GameLoopSystem.cs` (New), `AnimationSystem.cs` (New)
*   **`Match3.Core/Events/`** (New Communication Layer)
    *   Store event definitions: `TileSwappedEvent`, `MatchesFoundEvent`, etc.
*   **`Match3.Core/Interfaces/`**
    *   Keep as is (Contract Layer).

### **2. Architectural Refactoring (Code Design)**
Decompose the "God Class" `Match3Controller` into specialized systems.

#### **2.1 New System Implementations**
*   **`InteractionSystem`** (`Systems/Input/`):
    *   Absorb input logic from Controller (`OnTap`, `OnSwipe`).
    *   Handle selection state (`SelectedPosition`).
*   **`AnimationSystem`** (`Systems/Core/`):
    *   Absorb visual logic (`AnimateTiles`, `IsVisualAtTarget`).
    *   Isolate `Vector2` math from business logic.
*   **`GameLoopSystem`** (`Systems/Core/`):
    *   Absorb core loop (`ResolveMatches`, `UpdateSwapTasks`).
    *   Manage the "Swap -> Match -> Gravity" cycle purely logically.

#### **2.2 Event-Driven Architecture**
*   Introduce `IGameEvent` and a simple event mechanism.
*   Decouple `Match3.Core` from `IGameView`. The Core publishes events; the View listens.

#### **2.3 Facade Pattern**
*   Rename `Match3Controller` to **`Match3Engine`**.
*   It becomes a clean entry point that initializes and ticks the systems.
*   *Code Example*: `_gameLoop.Update(dt); _animationSystem.Update(dt);`

### **3. Execution Steps**
1.  **File Moves**: Create new folder structure and move existing files (safe refactoring).
2.  **Namespace Updates**: Fix namespaces to match new folders (e.g., `Match3.Core.Logic` -> `Match3.Core.Systems.Matching`).
3.  **Event Definition**: Create the `Events` folder and structs.
4.  **System Extraction**: Iteratively extract code from `Match3Controller` into new systems.
5.  **Engine Assembly**: Reassemble `Match3Engine` to use the new systems.
6.  **Verification**: Run tests and check compilability.

This structure aligns with **Domain-Driven Design (DDD)** principles, keeping Data (`Models`), Contracts (`Interfaces`), and Logic (`Systems`) distinct and organized.
