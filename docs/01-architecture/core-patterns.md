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

## Related Documents
*   Code Style: `docs/02-guides/coding-standards.md`
*   Testing Guidelines: `docs/testing-guidelines.md`
