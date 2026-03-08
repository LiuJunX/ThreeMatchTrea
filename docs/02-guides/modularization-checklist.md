# Modularization Checklist

Use this checklist when implementing any new feature in Match3Explore.

## 1. Planning Phase
- [ ] **Identify Domain**: Does this feature belong to Physics, Scoring, Input, or AI?
- [ ] **Define Interface**: Create a `I{Feature}System.cs` in `Match3.Core/Interfaces`.
    - [ ] Add XML documentation to all methods.
    - [ ] Ensure inputs depend on `GameState` (by ref/in) or POCOs.
- [ ] **Define Dependencies**: What other systems does this feature need? (e.g., RNG, Logger).

## 2. Implementation Phase
- [ ] **Create System Class**: Create `{Feature}System.cs` in `Match3.Core/Systems`.
- [ ] **Implement Interface**: Implement the logic.
- [ ] **Statelessness Check**: Ensure the class has no state fields (except `readonly` dependencies).
- [ ] **Dependency Injection**: Add dependencies to the constructor.

## 3. Integration Phase
- [ ] **Register in Engine**:
    - [ ] Add field `private readonly I{Feature}System _system;` to `Match3Engine`.
    - [ ] Add parameter to `Match3Engine` constructor.
    - [ ] Assign parameter to field.
- [ ] **Register in Factory**:
    - [ ] Instantiate the system in `GameServiceFactory`.
    - [ ] Pass it to the `Match3Engine` constructor.

## 4. Verification Phase
- [ ] **Unit Tests**: Create `Match3.Tests/Systems/{Feature}SystemTests.cs`.
- [ ] **Integration Test**: Run `Match3EngineTests` to ensure no regressions.
- [ ] **Build**: Run `dotnet build` to verify DI chains.

## Example: Adding a "Combo System"

1.  **Interface**:
    ```csharp
    public interface IComboSystem {
        void RegisterMatch(ref GameState state, int count);
        int GetMultiplier(in GameState state);
    }
    ```
2.  **Implementation**:
    ```csharp
    public class StandardComboSystem : IComboSystem {
        public void RegisterMatch(ref GameState state, int count) {
            state.ComboCounter++; // Assuming ComboCounter exists in GameState
        }
        // ...
    }
    ```
3.  **Integration**: Update `Match3Engine` to call `_comboSystem.RegisterMatch(...)` after matches.
