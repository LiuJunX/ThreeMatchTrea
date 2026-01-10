I will optimize the Scenario/Test workflow in the Level Editor.

### 1. ViewModel Refactoring (`LevelEditorViewModel.cs`)
*   **Decouple Selection**: Remove the mutual exclusivity between `SelectedType` and `SelectedBomb` so users can select both a Color and a Bomb type simultaneously (e.g., "Red" + "TNT").
*   **Add Assertion Flags**: Introduce `AssertColor` (default true) and `AssertBomb` (default true) properties.
*   **Granular Assertion Logic**: Update `HandleGridClick` to use these flags.
    *   If `AssertColor` is false, the created assertion will ignore the color layer (`Type = null`).
    *   If `AssertBomb` is false, the created assertion will ignore the bomb layer (`Bomb = null`).

### 2. UI Improvements (`LevelEditor.razor`)
*   **Strict Button States**:
    *   **"Edit Assertions" Button**: Only visible when *NOT* in Assertion Mode. Clicking it enters the mode.
    *   **"Finish Assertions" Button**: Only visible when *IN* Assertion Mode. Clicking it exits the mode.
*   **Assertion Configuration Panel**:
    *   When in Assertion Mode, display a dedicated panel in the sidebar:
        *   **Checkboxes**: `[x] Match Color` and `[x] Match Bomb`.
        *   **Instruction**: "Select properties to verify, then click grid cells."
*   **Visual Distinction**:
    *   Add a **Orange Border** (`border-warning`) to the grid container when in Assertion Mode.
    *   Change the cursor to a "crosshair" or "alias" to indicate targeting.

### 3. Workflow Polish
*   **Auto-Exit**: Ensure that clicking "Start Recording" or switching to "Level Design" automatically exits Assertion Mode (calling `Finish Assertions` logic internally).
