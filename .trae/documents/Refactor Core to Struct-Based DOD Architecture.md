# Grid-ECS Refactoring Plan

## 1. Cleanup Phase
Remove the OOP implementation to make way for the DOD architecture.
- **Delete**: `Match3.Core/Models/Elements/*.cs`
- **Delete**: `Match3.Core/Models/Grid/Match3Cell.cs`
- **Delete**: `Match3.Core/Interfaces/LayerInterfaces.cs`
- **Delete**: `Match3.Core/Interfaces/IGridElement.cs` (Replaced by component data)
- **Delete**: `Match3.Core/Interfaces/IDamageable.cs` (Replaced by HealthComponent)

## 2. Component Definition
Define the "Blittable" structs that hold the actual data.
- **Directory**: `Match3.Core/Structs/Components`
- **File**: `UnitComponent.cs`
  - Fields: `int Type`, `int Color`, `int FeatureFlags`.
- **File**: `HealthComponent.cs`
  - Fields: `int Value`, `int MaxValue`.
- **File**: `CoverComponent.cs`
  - Fields: `int Type`, `CoverAttachmentMode Mode`.

## 3. Grid Data Core
Implement the central data repository (SoA).
- **Directory**: `Match3.Core/Structs/Grid`
- **File**: `Tile.cs`
  - Fields: `TileType Topology`, `int UnitId`, `int CoverId`, `int GroundId`.
- **File**: `GridData.cs`
  - **Storage**: `Tile[] Tiles`, `UnitComponent[] Units`, `HealthComponent[] UnitHealths`, etc.
  - **Logic**: Simple "Free List" or "Stack" based ID allocation for O(1) creation/destruction.
  - **API**: `CreateUnit()`, `DestroyUnit()`, `GetTile(x, y)`.

## 4. Handle Implementation
Implement the user-friendly wrappers.
- **Directory**: `Match3.Core/Structs/Handles`
- **File**: `UnitHandle.cs`
  - Wraps `GridData` and `int Index`.
  - Properties like `Color` read directly from `GridData.Units[Index].Color`.
- **File**: `TileHandle.cs`
  - Wraps `GridData` and `(x, y)`.
  - API: `HasUnit`, `GetUnit()`.

## 5. Verification
Rewrite the test suite.
- **File**: `GridStructureTests.cs`
  - Verify zero-allocation creation.
  - Verify data persistence through Handles.
  - Verify component independence.
