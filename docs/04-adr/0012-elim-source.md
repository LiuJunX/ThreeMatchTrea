# 0012 — ElimSource (Elimination Source Enum)

* **Status**: Accepted
* **Deciders**: LiuJun
* **Date**: 2026-03-14

## Context and Problem Statement

`DestroyReason` 只有 4 个值（Match、BombEffect、Projectile、ChainReaction），粒度不足以区分不同消除来源。参考 Royal Match 的设计，动画系统需根据来源播放不同特效，数据分析需追踪各来源的贡献，未来 Guard 机制需按来源决定是否允许消除。

## Decision Drivers

* 特效粒度 — 颜色炸弹、道具、普通炸弹需要不同的视觉反馈
* 数据分析 — 追踪各消除来源对关卡完成的贡献
* 可扩展性 — 预留道具类（SideItem）等新来源
* 命名一致性 — `DestroyReason` 名称偏向实现细节，`ElimSource` 更贴近领域语言

## Decision Outcome

将 `DestroyReason` 重命名为 `ElimSource`，扩展枚举值：

| 值 | 含义 | 原值 |
|----|------|------|
| `Match` | 三消匹配 | `Match` |
| `Bomb` | 炸弹爆炸（行/列/范围） | `BombEffect` |
| `Projectile` | 弹射物（UFO/导弹） | `Projectile` |
| `ChainReaction` | 连锁反应传播 | `ChainReaction` |
| `ColorBomb` | 颜色炸弹激活 | *新增* |
| `SideItem` | 道具/助推器 | *新增* |

### Positive Consequences

* 颜色炸弹消除现在用 `ElimSource.ColorBomb`（原来混用 `BombEffect`），Choreographer 可区分特效
* `SideItem` 预留位供未来道具系统使用
* 全局替换，无遗留的 `DestroyReason` 引用
* 全量测试 1655 通过、0 失败

### Negative Consequences

* 一次性全局替换，涉及 19 个文件，无向后兼容的类型别名（C# enum 不支持 `[Obsolete]` 别名）

## Validation

* `dotnet build` — 0 错误
* `dotnet test --filter "Category!=Performance&Category!=Slow"` — 全量通过

## Affected Files

| 类型 | 文件 |
|------|------|
| 枚举定义 | `Events/Enums/EventEnums.cs` |
| 事件 | `Events/TileEvents.cs` |
| 接口 | `Systems/Elimination/ICellEliminator.cs` |
| 实现 | `Systems/Elimination/CellEliminator.cs` |
| 编排 | `Choreography/TileCommands.cs`, `TileChoreographer.cs` |
| 调用方 | `StandardMatchProcessor`, `PowerUpHandler`, `WavePropagation`, `ColorBombBatchProcessor`, `SimulationMatchHandler` |
| 测试 | `CellEliminatorTests`, `ChoreographerTests`, `ChoreographerBombEffectTests`, `ChoreographerGoalTests`, `MergeToBombChoreographerTests`, `EventCollectorTests`, `SimulationOrchestratorChainTests`, `ColorBombSessionManagerTests`, `PlayerDestroyAnimationTests` |
