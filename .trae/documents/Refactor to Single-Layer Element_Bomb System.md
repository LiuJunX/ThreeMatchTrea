I will implement the "Same Layer" design where Elements and Bombs are mutually exclusive (a cell is either a Gem or a Bomb, not both), ensuring this works in both the Editor and Gameplay.

### Core Data & Logic Refactoring
1.  **Add `TileType.Bomb`**:
    *   Modify `Match3.Core/Models/Enums/TileType.cs` to add a `Bomb` value. This acts as a physical placeholder for bombs so they participate in gravity but are distinct from colored gems.
2.  **Add Bomb Persistence**:
    *   Modify `Match3.Core/Config/LevelConfig.cs` to add `public BombType[] Bombs { get; set; }`. This fixes the missing feature where bomb types weren't being saved to JSON.
3.  **Update Engine Initialization**:
    *   Modify `Match3.Core/Match3Engine.cs` to load the `Bombs` array from `LevelConfig` when initializing the board.
4.  **Prevent Bomb Matching**:
    *   Modify `Match3.Core/Systems/Matching/ClassicMatchFinder.cs` to explicitly ignore `TileType.Bomb` during match detection, so bombs don't "match" with each other and disappear.

### Editor Logic Refactoring
5.  **Implement Mutual Exclusivity in ViewModel**:
    *   Modify `Match3.Editor/ViewModels/LevelEditorViewModel.cs`:
        *   **Tool Switching**: Selecting a Bomb tool will deselect the Color tool (and vice versa).
        *   **Painting Logic**:
            *   **Paint Bomb**: Sets grid to `TileType.Bomb` + `SelectedBomb`.
            *   **Paint Gem**: Sets grid to `SelectedColor` + `BombType.None`.
        *   **Data Sync**: Ensure `ActiveBombs` reads/writes to the new `LevelConfig.Bombs` property.

### UI Polish
6.  **Update Editor Visualization**:
    *   Modify `Match3.Web/Components/Pages/LevelEditor.razor` to ensure `TileType.Bomb` renders with an appropriate background (e.g., neutral gray) so the bomb icon is clearly visible.
