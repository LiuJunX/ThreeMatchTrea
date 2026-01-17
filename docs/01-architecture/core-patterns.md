<!-- SOURCE_OF_TRUTH: 架构约束、性能规范 -->
<!-- 其他文档应引用此文件，不应复制内容 -->

# Match3 Core Patterns & Services

本文件是架构约束和性能规范的**真源文档**。

All logic implementation must adhere to these patterns to ensure performance and portability.

## 1. Object Pooling (Memory Management)
*   **Why**: To avoid GC spikes during the game loop (60fps).
*   **What**: `MatchGroup`, `TileMove`, `Command` objects must be pooled.
*   **How**:
    *   **Rent**: `Pools.Rent<T>()`
    *   **Return**: `Pools.Return(obj)`
    *   **Forbidden**: `new T()` in hot paths (Update/Process loops).
*   **Utility**: `Match3.Core.Utility`
    *   `GenericObjectPool<T>`: Thread-safe, standard implementation.
    *   Supported Types: `List<T>`, `HashSet<T>`, `Queue<T>`.

**Best Practices**:
1.  **Prefer Pooling**: Use `Pools.ObtainList<T>()` instead of `new` for temporary collections in hot paths.
2.  **Guaranteed Release**: Always use `try...finally` blocks to ensure resources are released back to the pool via `Pools.Release()`.

## 2. Logging
*   **Interface**: `IGameLogger`
*   **Usage**: Inject `IGameLogger` into constructors.
*   **Zero-Allocation**: Use generic overloads `LogInfo<T>(template, arg)` instead of string interpolation `$"..."`.
*   **Forbidden**: `Console.WriteLine`, `Debug.Log`, and `$` string interpolation in hot paths.

## 3. String Handling (Zero-Allocation)
*   **Library**: `ZString` (Cysharp.Text)
*   **Pattern**: Use `ZString.Concat` or `ZString.Format` when you absolutely must manipulate strings in Logic.
*   **Constraint**: Avoid `string` allocations in `Update()`, `ProcessMatches()`, or `ApplyGravity()`.

## 4. Randomness
*   **Interface**: `IRandomService` (from Match3.Random)
*   **Usage**: All RNG must go through this service to ensure determinism for replays/testing.
*   **Forbidden**: `System.Random`.
*   **Rule**: **MUST** use `Match3.Core.Interfaces.IRandom`. NEVER use `System.Random` or `Guid` directly.

## 5. Performance Guidelines
1.  **Single Responsibility**: Split classes > 300 lines.
2.  **Pass by Ref**: Pass `GameState` by `ref` or `in` to avoid struct copying.
3.  **No State in Logic**: Never add state to Logic classes.
4.  **No Logic in State**: Never add logic to State structs.

## 6. Modular Architecture (Mandatory)
To ensure long-term maintainability and AI-collaboration efficiency, all new features must follow the **System-Interface** pattern.

### The Rule of "Systems"
*   **Definition**: A "System" is a stateless logic class that implements a specific `Interface` (e.g., `ScoreSystem : IScoreSystem`).
*   **Responsibility**: Encapsulate a single domain domain (Input, Scoring, Physics, AI).
*   **Integration**:
    *   `Match3Controller` MUST NOT contain business logic. It only coordinates Systems.
    *   All Systems must be injected via constructor (Dependency Injection).
    *   Systems must communicate via method calls or event bus, never by sharing mutable state objects (except `ref GameState`).

### Implementation Checklist
1.  **Define Interface**: Create `I{Feature}System` in `Match3.Core/Systems/{Domain}/`.
2.  **Implement System**: Create `{Feature}System` in same directory as interface.
3.  **Register**: Inject via constructor to `Match3Engine` or relevant coordinator.
4.  **Test**: Create specific unit tests for the System in isolation.

## 7. Architecture Red Lines

### Layer Dependencies
*   **No UI in Core**: `Match3.Core` must NEVER reference `Match3.Web` or `UnityEngine`.
*   **Inner/Outer Rule**: Inner layers (Core/Editor) MUST NOT depend on outer layers (Web/Unity/UI/IO).
*   **Editor Restrictions**: `Match3.Editor` MUST NOT reference `System.IO`, `System.Console`, `Microsoft.AspNetCore`.

### State Management
*   **Stateless Logic**: Logic classes must remain stateless. State belongs in Structs.
*   **No Logic in State**: State structs must not contain business logic.
*   **View Stateless**: UI/View layers must be stateless projections of ViewModel.

### Side Effects
*   All side effects (File IO, Alerts, Clipboard, Time) MUST be accessed via interfaces:
    *   `IFileSystemService` for file operations
    *   `IPlatformService` for platform-specific features

## 8. Event Sourcing Pattern

### Event Collection
*   **Interface**: `IEventCollector`
*   **Human Play**: Use `BufferedEventCollector` to capture events for presentation
*   **AI Mode**: Use `NullEventCollector.Instance` (zero allocation)

```csharp
// Emit events only when collector is enabled
if (events.IsEnabled)
{
    events.Emit(new TileMovedEvent { ... });
}
```

### Event Types
All events inherit from `GameEvent` which provides:
*   `Tick`: Simulation tick when event occurred
*   `SimulationTime`: Elapsed simulation time in seconds

**Best Practices**:
1.  **Check IsEnabled**: Always check `events.IsEnabled` before creating event objects
2.  **Immutable Events**: Use `record` types with `init` setters
3.  **Position Types**: Use `Vector2` for continuous positions, `Position` for grid positions

## 9. Simulation Engine Pattern

### Tick-Based Updates
*   **Fixed Time Step**: Use `SimulationConfig.FixedDeltaTime` (default 0.016s)
*   **Deterministic**: Same inputs produce same outputs across platforms

```csharp
// Single tick execution
var result = engine.Tick();

// Run until stable (AI mode)
var result = engine.RunUntilStable();
```

### Cloning for AI
*   Use `engine.Clone()` to create parallel simulation branches
*   Cloned engines use `NullEventCollector` by default
*   Provide deterministic `IRandom` for reproducible results

## 10. Projectile System Pattern

### Projectile Lifecycle
1.  **Launch**: `projectileSystem.Launch(projectile, tick, simTime, events)`
2.  **Update**: Called each tick via `projectileSystem.Update(...)`
3.  **Impact**: When `Update()` returns affected positions, process impacts

### Custom Projectiles
Extend `Projectile` base class:
```csharp
public class MyProjectile : Projectile
{
    public override bool Update(ref GameState state, float dt, ...) { ... }
    public override HashSet<Position> ApplyEffect(ref GameState state) { ... }
}
```

## 11. Factory Pattern (Dependency Injection)

### GameServiceFactory
Abstracts away the manual assembly of 13+ game systems.

```csharp
// Builder pattern for configuration
var factory = new GameServiceBuilder()
    .WithPhysics((cfg, rng) => new CustomPhysics(cfg, rng))
    .WithMatchFinder(bombGen => new CustomMatchFinder(bombGen))
    .Build();

// Factory creates complete sessions
var session = factory.CreateGameSession(config, levelConfig);
```

### Configurable Systems
| System | Factory Signature |
| :--- | :--- |
| Physics | `Func<Match3Config, IRandom, IPhysicsSimulation>` |
| MatchFinder | `Func<IBombGenerator, IMatchFinder>` |
| MatchProcessor | `Func<IScoreSystem, BombEffectRegistry, IMatchProcessor>` |
| Refill | `Func<ISpawnModel, IRefillSystem>` |
| EventCollector | `Func<bool, IEventCollector>` |

### Benefits
*   **Testability**: Inject mocks for isolated testing
*   **Flexibility**: Swap implementations without code changes
*   **Encapsulation**: Hide complex wiring from consumers

## 12. Visitor Pattern (Event Dispatch)

### Purpose
Compile-time exhaustive handling of all event types, replacing error-prone switch statements.

### Structure
```csharp
// 1. Base class with Accept method
public abstract record GameEvent
{
    public abstract void Accept(IEventVisitor visitor);
}

// 2. Concrete events implement Accept
public sealed record TileMovedEvent : GameEvent
{
    public override void Accept(IEventVisitor visitor) => visitor.Visit(this);
}

// 3. Visitor interface enforces exhaustive handling
public interface IEventVisitor
{
    void Visit(TileMovedEvent evt);
    void Visit(TileDestroyedEvent evt);
    // ... 17 total Visit methods
}

// 4. Consumers implement the interface
public class EventInterpreter : IEventVisitor
{
    public void Visit(TileMovedEvent evt) { /* create animation */ }
    public void Visit(TileDestroyedEvent evt) { /* create explosion */ }
}
```

### Benefits
*   **Type Safety**: Compiler error if new event type not handled
*   **No Casting**: Each Visit method receives correctly typed event
*   **Extensibility**: Add new visitors without modifying events

## 13. Command Pattern (Replay System)

### Purpose
Encapsulate player actions as objects for recording and replay.

### Structure
```csharp
public interface IGameCommand
{
    Guid Id { get; }
    long IssuedAtTick { get; }
    bool Execute(SimulationEngine engine);
    bool CanExecute(in GameState state);
}

public sealed record SwapCommand : IGameCommand
{
    public Position From { get; init; }
    public Position To { get; init; }

    public bool Execute(SimulationEngine engine)
        => engine.ApplyMove(From, To);
}
```

### Recording & Playback
```csharp
// Recording
var history = new CommandHistory();
history.Record(new SwapCommand { From = a, To = b, IssuedAtTick = tick });

// Playback
var recording = new GameRecording
{
    InitialState = GameStateSnapshot.FromState(state),
    RandomSeed = seed,
    Commands = history.GetCommands()
};

var controller = new ReplayController(recording, factory);
controller.Play();
```

### Benefits
*   **Determinism**: Same commands + seed = identical replay
*   **Debugging**: Reproduce bugs by replaying command sequence
*   **Analytics**: Analyze player behavior patterns

See: `docs/03-design/features/replay-system.md`

## Related Documents
*   Code Style: `docs/02-guides/coding-standards.md`
*   Testing Guidelines: `docs/testing-guidelines.md`
*   Architecture Overview: `docs/01-architecture/overview.md`
*   Replay System: `docs/03-design/features/replay-system.md`
