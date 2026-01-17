# ADR 0003: Event Sourcing + Tick-Based Simulation Architecture

## Status
Accepted

## Date
2026-01-16

## Context

The Match3 game requires:
1. **Smooth Animations**: Complex visual effects (UFO flight, cascades) need time-based interpolation
2. **AI High-Speed Simulation**: AI needs to evaluate thousands of moves quickly without rendering overhead
3. **Cross-Platform Core**: Game logic must work on Web, Unity, and other platforms
4. **Deterministic Replay**: Ability to replay games from recorded inputs

The original synchronous architecture had limitations:
- Immediate effect application prevented smooth animations
- No separation between simulation and presentation timing
- AI simulation couldn't be accelerated without affecting game logic

## Decision

We adopt an **Event Sourcing + Tick-Based Simulation** architecture with three layers:

### 1. Core Simulation Layer
- `SimulationEngine` drives game state with fixed time steps (0.016s default)
- All state changes emit `GameEvent` records
- `NullEventCollector` enables zero-overhead AI mode

### 2. Presentation Layer (Pure Player Architecture)
- `Choreographer` (Core) converts events to `RenderCommand[]` with pre-calculated timing
- `Player` (Presentation) executes commands with interpolation, supports seeking/replay
- `VisualState` tracks interpolated positions

### 3. AI Layer
- `AIService` uses `RunUntilStable()` for instant simulation
- `Clone()` enables parallel move evaluation
- `DifficultyAnalysis` provides board assessment

### Key Interfaces

```csharp
public interface IEventCollector
{
    void Emit(GameEvent evt);
    bool IsEnabled { get; }
}

public sealed class SimulationEngine
{
    public TickResult Tick(float deltaTime);
    public SimulationResult RunUntilStable();
    public SimulationEngine Clone(IRandom? newRandom = null);
}
```

## Consequences

### Positive
- **Decoupled Animation**: Presentation layer can interpolate smoothly independent of simulation
- **AI Performance**: ~1000x speedup with event collection disabled
- **Testability**: Events can be captured and verified in tests
- **Replay Support**: Event stream enables deterministic replay
- **Projectile Support**: Continuous physics for flying entities

### Negative
- **Increased Complexity**: Three-layer architecture vs single-layer
- **Memory**: Event objects allocation (mitigated by `IsEnabled` check)
- **Learning Curve**: Developers must understand event flow

### Neutral
- **Backward Compatible**: Existing `Match3Engine` continues to work
- **Incremental Adoption**: Systems can be migrated gradually

## Alternatives Considered

### 1. Immediate Mode
Keep synchronous execution, add animation state machine.
- Rejected: Too coupled, hard to support AI speedup.

### 2. Full ECS
Adopt Entity-Component-System architecture.
- Rejected: Over-engineering for current scope, high migration cost.

### 3. Command Pattern Only
Use commands without events.
- Rejected: Commands lack the granularity needed for smooth animations.

## Related ADRs
- ADR 0001: Adopt ZString
- ADR 0002: Adopt Docs-as-Code

## References
- Martin Fowler: Event Sourcing
- Game Programming Patterns: Update Method
