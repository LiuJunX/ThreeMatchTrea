# 0011 — Unified CellEliminator

* **Status**: Accepted
* **Deciders**: LiuJun
* **Date**: 2026-03-14

## Context and Problem Statement

"消除一个格子"的逻辑散落在 5 条路径中，每条路径各自实现 Cover → Event → Objective → SetTile → Ground 的仪式，且步骤不一致：

| 路径 | 缺少的步骤 |
|------|-----------|
| `StandardMatchProcessor` | CanDestroy、IsGoal、Objective |
| `PowerUpHandler.ClearAffectedTiles` | CanDestroy、IsGoal、Objective |
| `WavePropagation.ProcessWave` | （完整） |
| `SimulationMatchHandler.ProcessProjectileImpacts` | Cover、Ground |
| `PowerUpHandler.ActivateBomb` fallback | CanDestroy、IsGoal、Objective |

新增消除路径时必须手动复制全部仪式步骤，遗漏风险高。

## Decision Drivers

* 行为一致性 — 所有消除路径必须执行相同的仪式
* 可审计性 — 单一入口便于 code review 发现遗漏
* 最小改动 — 保留旧构造函数重载，避免大范围测试文件改动
* Clone 安全 — 新类型必须可安全共享于并行模拟分支

## Considered Options

1. **提取 CellEliminator 类** — 所有路径调用 `Eliminate()` 方法
2. **GameState 扩展方法** — `state.EliminateCell(pos, ...)` 静态方法
3. **保持现状，写文档约束** — 依赖 code review

## Decision Outcome

选择 **Option 1：提取 CellEliminator 类**。

引入 `ICellEliminator` 接口和 `CellEliminator` 实现，封装完整仪式：

```
Guard(empty) → Cover → Indestructible → Event(+IsGoal) → Objective → Mutate → Ground
```

返回 `EliminateResult { Eliminated, Absorbed, Blocked }` 供调用方根据结果决定后续行为（如炸弹连锁、Receive Lock）。

### Positive Consequences

* 5 条路径行为统一，修复了 3 处遗漏（StandardMatchProcessor 缺 CanDestroy/IsGoal/Objective、ClearAffectedTiles 同理、ProcessProjectileImpacts 缺 Cover/Ground）
* 新增消除路径只需调用 `Eliminate()`，无需复制仪式
* CellEliminator 无可变字段，Clone() 安全共享
* 旧构造函数重载保证现有 1477 个测试零改动通过

### Negative Consequences

* 炸弹连锁判断仍由调用方负责（需在 `Eliminate()` 之后根据返回值和原始 tile type 决定是否触发）
* 旧构造函数内部创建的 CellEliminator 无 `ILevelObjectiveSystem`，fallback 路径不追踪目标（仅影响无 ExplosionSystem 的测试场景）

## Validation

* `CellEliminatorTests` — 覆盖 Empty/Cover/Indestructible/Normal/IsGoal/NullCollector 7 个用例
* 全量测试 `dotnet test --filter "Category!=Performance&Category!=Slow"` — 1477 通过、0 失败

## Key Design Details

### 调用方职责分离

CellEliminator 只负责"消除一个格子"。以下逻辑保留在调用方：

* 炸弹连锁检测（需原始 tile type + BombEffectRegistry）
* Receive Lock / Drop Lock 管理（时长因场景而异）
* BombActivatedEvent 发射（ActivateBomb 专有）

### 文件清单

| 类型 | 文件 |
|------|------|
| 新建 | `Systems/Elimination/EliminateResult.cs`, `ICellEliminator.cs`, `CellEliminator.cs` |
| 新建测试 | `Tests/Systems/Elimination/CellEliminatorTests.cs` |
| 修改 | `StandardMatchProcessor`, `PowerUpHandler`, `WavePropagation`, `ExplosionSystem`, `SimulationMatchHandler`, `SimulationOrchestrator`, `SimulationEngine`, `GameServiceFactory`, `TestEngineFactory` |
