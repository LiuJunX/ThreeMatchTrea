# Match3 Core Architecture

## Overview
The Match3 Core is the heart of the game engine, designed with a **Slot-Based Layered Architecture** and **Event-Sourced Simulation**. It strictly adheres to the principle of separation of concerns, ensuring that game logic is decoupled from the view layer.

### Architecture Layers
```
┌─────────────────────────────────────────────────────────────────┐
│                         AI Agent                                 │
│                    (直接调用 Core)                               │
└────────────────────────────┬────────────────────────────────────┘
                             │
┌────────────────────────────┼────────────────────────────────────┐
│                    Match3.Core                                   │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │                  SimulationEngine                        │    │
│  │  • Tick(dt) → TickResult + Events                       │    │
│  │  • RunUntilStable() → 高速模拟                          │    │
│  │  • Clone() → 并行模拟                                   │    │
│  └─────────────────────────────────────────────────────────┘    │
│                             │                                    │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐        │
│  │ Physics  │  │Projectile│  │ Matching │  │  Refill  │        │
│  │ System   │  │ System   │  │  System  │  │  System  │        │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘        │
│                             │                                    │
│                    GameEvent Stream                              │
└────────────────────────────┬────────────────────────────────────┘
                             │
┌────────────────────────────┼────────────────────────────────────┐
│               Match3.Presentation                                │
│  • Player (指令播放器)                                          │
│  • VisualState (插值位置)                                       │
│  ────────────────────────────────────────────────────────────── │
│               Match3.Core.Choreography                           │
│  • Choreographer (GameEvent → RenderCommand[])                  │
│  • RenderCommand (可序列化渲染指令)                             │
└────────────────────────────┬────────────────────────────────────┘
                             │
┌────────────────────────────┼────────────────────────────────────┐
│                  Match3.Web / Unity                              │
│                    (平台渲染)                                    │
└─────────────────────────────────────────────────────────────────┘
```

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

## 3. Core Systems (Planned & Implemented)
- **MatchFinder**: Scans `Unit` layer for matches based on `IMatchable`.
- **BombGenerationSystem**: Handles complex match analysis, bomb creation, and optimal shape partitioning.
- **GravitySystem**: Moves items in the `Unit` layer (and attached `Dynamic` covers) down to empty spaces.
- **InteractionSystem**: Handles swaps between `Unit` elements, respecting `Cover` constraints.

## 4. Event Sourcing & Simulation

### Event System (`Match3.Core.Events`)
All state changes produce `GameEvent` records for presentation layer consumption.

| Event Type | Description |
| :--- | :--- |
| `TileMovedEvent` | Tile position changed (gravity, swap) |
| `TileDestroyedEvent` | Tile cleared from grid |
| `TileSpawnedEvent` | New tile created (refill) |
| `MatchDetectedEvent` | Match pattern found |
| `ProjectileLaunchedEvent` | Projectile launched |
| `ProjectileImpactEvent` | Projectile hit target |
| `BombActivatedEvent` | Bomb explosion triggered |
| `ScoreAddedEvent` | Score changed |

### Event Visitor Pattern
Events use the **Visitor Pattern** for type-safe dispatch, enforcing compile-time exhaustive handling.

```csharp
// Base event with Accept method
public abstract record GameEvent
{
    public abstract void Accept(IEventVisitor visitor);
}

// Visitor interface (17 event types)
public interface IEventVisitor
{
    void Visit(TileMovedEvent evt);
    void Visit(TileDestroyedEvent evt);
    void Visit(MatchDetectedEvent evt);
    // ... all 17 event types
}

// Usage - replaces switch statements
public class Choreographer : IEventVisitor
{
    public IReadOnlyList<RenderCommand> Choreograph(IReadOnlyList<GameEvent> events)
    {
        foreach (var evt in events) evt.Accept(this);
        return _commands;
    }
    public void Visit(TileMovedEvent evt) { /* create MoveTileCommand */ }
}
```

### Event Collectors
- **`BufferedEventCollector`**: Collects events for presentation (human play)
- **`NullEventCollector`**: Zero-overhead collector for AI simulation

### Simulation Engine (`Match3.Core.Simulation`)
Tick-based simulation with configurable time step.

```csharp
// Human play mode (16ms fixed step, events enabled)
var config = SimulationConfig.ForHumanPlay();

// AI mode (100ms step, events disabled)
var config = SimulationConfig.ForAI();
```

**Key Methods**:
- `Tick(deltaTime)` → Execute single simulation step
- `RunUntilStable()` → High-speed simulation until stable
- `Clone()` → Create parallel simulation branch for AI

## 5. Projectile System (`Match3.Core.Systems.Projectiles`)
Continuous physics for flying entities (UFO, missiles).

### Projectile Phases
| Phase | Description |
| :--- | :--- |
| `Takeoff` | Vertical rise animation (0.3s) |
| `Flight` | Moving towards target (12 units/s) |
| `Impact` | Effect application |

### Targeting Modes
- **FixedCell**: Target fixed grid position
- **Dynamic**: Re-evaluate best target each tick
- **TrackTile**: Track specific tile by ID

## 6. Presentation Layer (Pure Player Architecture)
Decoupled animation system driven by render commands.

### Architecture Flow
```
Core → GameEvent → Core.Choreographer → RenderCommand[] → Player → VisualState
```

### Components
- **`Choreographer`** (Core层): Converts `GameEvent` → `RenderCommand[]` with pre-calculated timing
- **`Player`** (Presentation层): Executes commands, interpolates animations, updates `VisualState`
- **`VisualState`**: Interpolated visual positions, scales, effects
- **`RenderCommand`**: Serializable render instructions (MoveTile, DestroyTile, SpawnTile, etc.)

### Benefits
- **Minimal Porting Cost**: Only need to port Player to new platforms
- **Precise Replay**: RenderCommand sequences are serializable
- **Maximum Testability**: Assert on command streams
- **AI-Friendly**: Choreographer in Core layer for AI coding

## 7. AI Service (`Match3.Core.AI`)
High-speed simulation for move evaluation and difficulty analysis.

### Key Interfaces
```csharp
public interface IAIService
{
    IReadOnlyList<Move> GetValidMoves(in GameState state);
    MovePreview PreviewMove(in GameState state, Move move);
    Move? GetBestMove(in GameState state);
    DifficultyAnalysis AnalyzeDifficulty(in GameState state);
}
```

### Strategies
- **GreedyStrategy**: Maximize immediate score
- **BombPriorityStrategy**: Prioritize bomb creation/activation

### Difficulty Analysis
- Valid move count
- Score potential (average/max)
- Cascade depth
- Board health indicators

## 8. Key Design Decisions
- **OOP over Pure DOD**: We use classes (`Match3Cell`) and interfaces to handle the complexity of multi-layered interactions, favoring maintainability and flexibility over raw struct-array performance for this specific component.
- **Unified Damage**: Clearing a block, breaking ice, or spreading jelly are all treated as `TakeDamage` events.
- **Event Sourcing**: All state changes produce events, enabling replay, AI analysis, and decoupled presentation.
- **Tick-Based Simulation**: Fixed time step (16ms default) enables deterministic simulation and time-based animations.
- **Zero-Overhead AI**: `NullEventCollector` allows AI to run simulations without event allocation overhead.

## 9. Dependency Injection (`Match3.Core.DependencyInjection`)

### Overview
The DI system abstracts away the manual assembly of 13+ game systems, providing a clean factory-based API.

```
DependencyInjection/
├── IGameServiceFactory.cs       # Factory interface
├── GameServiceFactory.cs        # Factory implementation
├── GameServiceBuilder.cs        # Fluent configuration builder
├── GameServiceConfiguration.cs  # Immutable configuration record
└── GameSession.cs               # Session encapsulation
```

### GameServiceBuilder (Fluent API)
```csharp
var factory = new GameServiceBuilder()
    .WithPhysics((cfg, rng) => new CustomPhysics(cfg, rng))
    .WithMatchFinder(bombGen => new CustomMatchFinder(bombGen))
    .Build();
```

### GameSession
Encapsulates a complete game session with all dependencies:

```csharp
public sealed class GameSession : IDisposable
{
    public SimulationEngine Engine { get; }
    public IEventCollector EventCollector { get; }
    public SeedManager SeedManager { get; }
    public GameServiceConfiguration Configuration { get; }
}
```

### Usage
```csharp
// Simple usage with defaults
var factory = new GameServiceBuilder().Build();
var session = factory.CreateGameSession(levelConfig);

// Custom configuration
var config = new GameServiceConfiguration
{
    Width = 8,
    Height = 8,
    RngSeed = 12345,
    EnableEventCollection = true
};
var session = factory.CreateGameSession(config, levelConfig);
```

## 10. Command & Replay System

### Overview
The Command pattern enables game recording and deterministic replay.

```
Commands/
├── IGameCommand.cs        # Command interface
├── SwapCommand.cs         # Tile swap command
├── TapCommand.cs          # Power-up tap command
└── CommandHistory.cs      # Thread-safe command recording

Replay/
├── GameStateSnapshot.cs   # Serializable state snapshot
├── GameRecording.cs       # Complete game recording
└── ReplayController.cs    # Playback controller
```

### Command Interface
```csharp
public interface IGameCommand
{
    Guid Id { get; }
    long IssuedAtTick { get; }
    bool Execute(SimulationEngine engine);
    bool CanExecute(in GameState state);
}
```

### ReplayController
```csharp
var controller = new ReplayController(recording, factory);
controller.PlaybackSpeed = 2.0f;  // 2x speed
controller.Play();
controller.Seek(0.5f);            // Jump to 50%
controller.Tick(deltaTime);       // Update each frame
```

### Determinism Guarantees
- **Fixed Random Seed**: `GameRecording.RandomSeed`
- **Domain-Isolated RNG**: `SeedManager` provides separate streams
- **Command Timing**: `IssuedAtTick` records precise timing
- **State Snapshot**: `GameStateSnapshot` captures full board state

See: `docs/03-design/features/replay-system.md`
