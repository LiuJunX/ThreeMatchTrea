# 1. Adopt ZString for Zero-Allocation String Handling

* **Status**: Accepted
* **Deciders**: Trae AI, User
* **Date**: 2026-01-09

## Context and Problem Statement

The game loop generates significant garbage (GC pressure) due to frequent string allocations in logging and debug output (e.g., `LogInfo($"Pos: {x},{y}")`). This causes frame rate stuttering in long-running sessions. We need a way to handle strings and logging without allocating memory on the heap for every message.

## Decision Drivers

* **Performance**: Must minimize GC allocations in the hot path (60fps).
* **Compatibility**: Must work with .NET Standard 2.1 (Core).
* **Usability**: Should offer an API similar to `StringBuilder` or `string.Format`.

## Considered Options

* **Option 1**: Use `System.Text.StringBuilder`.
* **Option 2**: Use `Span<char>` manually.
* **Option 3**: Use `Cysharp.Text.ZString` library.

## Decision Outcome

Chosen option: **Option 3 (ZString)**.

### Justification
*   `ZString` provides a zero-allocation `StringBuilder` (struct-based) and `Format` methods that write directly to the output buffer.
*   It is widely used in Unity/Gaming scenarios for this exact purpose.
*   Manual `Span<char>` (Option 2) is too complex/verbose for general logging.
*   Standard `StringBuilder` (Option 1) still requires management (Clear/Cache) and eventually allocates the final string unless used very carefully.

### Positive Consequences

*   Zero GC allocation for logging parameters (structs are formatted directly).
*   Reduced pressure on Garbage Collector Gen 0.

### Negative Consequences

*   Introduces a third-party dependency (`ZString`).
*   Developers must learn to use `ZString.Format` instead of `$` interpolation.

## Implementation Details
*   Added `ZString` NuGet package to `Match3.Core`.
*   Refactored `IGameLogger` to use generic templates `LogInfo<T>(string template, T arg)` to leverage ZString internally.

## Validation
*   **Project Rules**: Added explicit prohibition of `$` interpolation in `project_rules.md`.
*   **Code Review**: AI Agents are instructed to reject string allocations in hot paths.
*   **Architecture Tests**: (Planned) Add a NetArchTest rule to detect `StringBuilder` usage in `Match3.Core.Logic`.
