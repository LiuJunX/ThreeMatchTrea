# ThreeMatchTrea Architecture

This document describes the high-level architecture, design philosophy, and coding standards of the ThreeMatchTrea project.

## 1. Core Design Philosophy: AI-First & Data-Oriented

The architecture of `Match3.Core` has been strictly refactored to follow a **Data-Oriented Design (DOD)** approach, inspired by ECS (Entity Component System) principles. This is to ensure:
1.  **High Performance**: Minimal memory allocation and cache-friendly data layout.
2.  **Determinism**: Guaranteed reproducibility given a seed.
3.  **AI Readiness**: Easy integration with Reinforcement Learning (RL) and Monte Carlo Tree Search (MCTS) algorithms.

### Data-Logic Separation

We strictly enforce the separation of **State** (Data) and **Logic** (Behavior).

*   **State (`GameState`)**: A pure `struct` containing only data (arrays, integers, scores). It has **zero** logic methods. It is allocated on the stack or as a compact array, making it extremely cheap to clone (Snapshot).
*   **Logic (`GameRules`)**: A `static class` containing pure functions. These functions take `ref GameState` as input and modify it. They are stateless and thread-safe.

**Violation of this principle (e.g., adding logic to GameState or storing state in GameRules) is strictly prohibited.**

## 2. Architecture Layers

The solution is divided into strict layers to maintain separation of concerns:

*   **Match3.Web (UI)**: 
    *   Blazor Server application.
    *   View-only responsibilities.
    *   **No game rules** should exist here.
    *   Visualizes the `GameState` provided by the Core.

*   **Match3.Core (Domain)**:
    *   **Structs**: Pure data models (`Tile`, `GameState`, `Position`).
    *   **Interfaces**: Abstract behaviors (`IMatchFinder`, `IGameView`).
    *   **Logic**: Pure logic implementations (`ClassicMatchFinder`, `StandardGravitySystem`).
    *   **AI**: RL environment wrappers (`Match3Environment`).

*   **Match3.ConfigTool (Tools)**:
    *   Console application for generating binary configuration files from external sources (e.g., Feishu/Lark).

*   **Match3.Tests**:
    *   Unit and Scenario tests ensuring correctness.

## 3. Core Components

### GameState (Struct)
The heart of the system.
- **`Tile[] Grid`**: Flattened 1D array representing the 2D board for better memory locality.
- **`long Score`**: Current game score.
- **`long MoveCount`**: Number of moves made.
- **`IRandom Random`**: Deterministic random source.

### Match3Controller
The bridge between Data and UI.
- Maintains a `GameState` instance.
- Orchestrates the game loop (Swap -> Match -> Gravity -> Refill).
- Notifies `IGameView` (UI) of changes.

### Match3Environment (AI Wrapper)
Implements a standard RL interface (`Reset`, `Step`, `GetState`).
- Wraps `GameState` and Logic into an object-oriented API for external AI frameworks (Python/ML.NET).

## 4. Configuration System

The system allows game designers to edit configuration data in **Feishu (Lark) Spreadsheets**. A build tool converts this to an optimized **Binary Format**.

### Workflow
1.  **Design**: Edit data in Feishu.
2.  **Build**: Run `Match3.ConfigTool` to fetch JSON and compile to `config.bin`.
3.  **Runtime**: Game loads `config.bin` at startup via `ConfigManager`.

### Components
*   **Match3.ConfigTool**: Fetches and serializes data.
*   **ConfigManager** (`Match3.Core`): Loads binary data into efficient structs (`ItemConfig`).

## 5. Coding Standards & Best Practices

### Code Style
*   **Format**: 4 spaces indent, CRLF, Allman braces (start on new line).
*   **Naming**: 
    *   `_camelCase` for private fields.
    *   `PascalCase` for public members/classes.
    *   `IInterface` prefix for interfaces.
*   **Namespaces**: Use file-scoped namespaces (e.g., `namespace Match3.Core;`).

### Critical Rules
1.  **Single Responsibility**: Split classes > 300 lines.
2.  **Randomness**: **MUST** use `Match3.Core.Interfaces.IRandom`. NEVER use `System.Random` or `Guid` directly.
3.  **Performance**: Pass `GameState` by `ref` or `in` to avoid struct copying.
4.  **State Management**: Use explicit State classes where possible.
5.  **CSS Isolation**: Use `.razor.css` files. No `<style>` tags in razor.

### Future Development Guidelines
1.  **Never add state to Logic classes.**
2.  **Never add logic to State structs.**
3.  **Always use `ref`** when passing `GameState` unless a snapshot is explicitly needed.
