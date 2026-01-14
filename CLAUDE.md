# Project Rules for Claude

This project uses shared coding standards maintained in `.trae/rules/project_rules.md`.

**IMPORTANT**: Before starting any task, read the complete project rules:
- `.trae/rules/project_rules.md` - Full coding standards and conventions

## Quick Reference (Key Constraints)

### Code Style
- 4 spaces indentation, Allman braces, file-scoped namespaces
- Private fields: `_camelCase`, Public members: `PascalCase`
- Interfaces: `I` prefix

### Architecture Red Lines
- `Match3.Core` must NEVER reference `Match3.Web`
- Use `Pools.Rent<T>()` for hot-path objects (no `new T()` in loops)
- Use `Match3.Random` interfaces (no `System.Random`)
- Logic classes must remain stateless

### Workflow
- Run `dotnet test` after changes to core logic
- Confirm before action for ambiguous requirements
- Atomic commits: separate config/logic/docs changes

### Testing Requirements (MUST READ: `docs/testing-guidelines.md`)
- **Input variants**: Test ALL possible input variations (directions, positions, edge cases)
- **Multi-system features**: Add integration tests with REAL systems, not just Stub isolation
- **Async/multi-frame behavior**: Verify intermediate states, not just final results
- **Cross-system stability**: When multiple systems interact, check stability conditions from ALL systems

### Project Structure
- `Match3.Core` - Pure business logic, no UI dependencies
- `Match3.Random` - Unified random entry point
- `Match3.Web` - Application assembly and view layer
- `Match3.Editor` - Cross-platform editor logic
- `docs/` - Documentation (update when changing core components)
