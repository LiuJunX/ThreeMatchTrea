# Match3 Core Refactoring Plan (Final)

## 1. Interface Implementation
Define the new contract for grid elements with strict layering.
- Create `Match3.Core/Interfaces/IGridElement.cs`
- Create `Match3.Core/Interfaces/IDamageable.cs`
- Create `Match3.Core/Interfaces/LayerInterfaces.cs`

## 2. Enum Definitions
Ensure all necessary types are defined.
- Update/Create `Match3.Core/Models/Enums/CoverAttachmentMode.cs`
- Update `Match3.Core/Models/Enums/TileType.cs`
- Create `Match3.Core/Models/Enums/BombType.cs`

## 3. Element Implementation (Prefix Naming)
Implement the concrete classes using `{Layer}{Name}Item` convention.
- Create directory `Match3.Core/Models/Elements`
- Create `UnitNormalItem.cs`
- Create `UnitBombItem.cs`
- Create `GroundJellyItem.cs`
- Create `CoverIceItem.cs`

## 4. Grid Architecture
Rebuild the grid storage to support the slot-based system.
- Create `Match3.Core/Models/Grid/Match3Cell.cs`
- Create `Match3.Core/Models/Grid/Match3Grid.cs`
- Remove old `Tile.cs` if obsolete.

## 5. Testing
Verify the new architecture.
- Create `Match3.Tests/Core/GridStructureTests.cs`
