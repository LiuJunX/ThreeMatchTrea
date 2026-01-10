Yes, **updating the project rules to explicitly adopt DDD (Domain-Driven Design) and Package-by-Feature** is highly recommended.

**Why update the rules?**
1.  **Codify the New Architecture**: You have already approved the refactoring to Package-by-Feature. The rules should reflect the code's reality to prevent future regressions (e.g., someone adding a new interface back into a generic `Interfaces` folder).
2.  **Guide Future AI/Developers**: Explicit rules ensure that any new feature (e.g., "Ice Layers") is implemented with the correct structure from day one.
3.  **Replace Outdated Rules**: Current **Rule 10** enforces the old "Interface in one folder, Implementation in another" pattern. This contradicts our new direction and must be updated.

**Plan to Update Project Rules:**

1.  **Modify Rule 10 (Mandatory Modularization)**:
    *   **Rename**: "Mandatory Modularization (DDD & Package-by-Feature)".
    *   **New Guideline**:
        *   "Features MUST be self-contained in `Match3.Core/Systems/{FeatureName}`."
        *   "Interfaces (`I{Name}System`) MUST reside next to their implementation, NOT in a global folder."
        *   "Dependencies between features MUST be explicit via interface injection."

2.  **Add DDD Principles to Rule 3 (Design Patterns)**:
    *   Add "High Cohesion": Code that changes together stays together.
    *   Add "Ubiquitous Language": Use domain terms (`Gravity`, `Matching`) in folder/file names.

3.  **Refactor Codebase to Comply**:
    *   Execute the file moves (Interfaces -> Feature Folders) as previously discussed.
    *   Update namespaces to `Match3.Core.Systems.{FeatureName}`.

**Execution Steps:**
1.  **Update `project_rules.md`**: Rewrite Rule 10 and enhance Rule 3.
2.  **Move Files**: Move interfaces from `Match3.Core/Interfaces` to their respective system folders.
3.  **Fix Namespaces**: Update code references.
4.  **Verify**: Build the project.

This ensures the documentation (Rules) and implementation (Code) are perfectly aligned.
