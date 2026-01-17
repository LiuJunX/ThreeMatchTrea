# ADR 0004: Pure Player Architecture for Presentation Layer

## Status
Accepted

## Date
2026-01-17

## Context

原有 Presentation 层架构：
```
Core → GameEvent → Presentation (EventInterpreter + AnimationTimeline) → Web
```

存在以下问题：

1. **跨平台移植成本高**：`EventInterpreter` 和 `AnimationTimeline` 包含复杂的时序计算逻辑，移植到 Unity 等平台需要重写大量代码
2. **回放困难**：动画状态分散在多个类中，无法精确序列化和重放
3. **回退操作复杂**：没有统一的指令流，回退到历史状态需要重新计算所有动画
4. **测试覆盖困难**：动画逻辑与状态耦合，难以对动画序列进行断言
5. **AI 编码受限**：时序计算逻辑在 Presentation 层，AI 无法直接访问

## Decision

采用 **Pure Player Architecture**，将时序计算移到 Core 层：

```
Core → GameEvent → Core.Choreographer → RenderCommand[] → Player → Web
```

### 核心组件

| 组件 | 层 | 职责 |
|------|-----|------|
| `Choreographer` | Core | 实现 `IEventVisitor`，将 GameEvent 转换为 RenderCommand，预计算所有时序 |
| `RenderCommand` | Core | 可序列化的渲染指令（15 种类型），包含 StartTime、Duration、Priority |
| `Player` | Presentation | 消费 RenderCommand 序列，按时间插值更新 VisualState，支持 Seek/Replay |
| `VisualState` | Presentation | 存储插值后的视觉状态（位置、缩放、透明度） |

### RenderCommand 指令集

```csharp
// Tile 指令
SpawnTileCommand, MoveTileCommand, DestroyTileCommand,
SwapTilesCommand, RemoveTileCommand, UpdateTileBombCommand

// Effect 指令
ShowEffectCommand, ShowMatchHighlightCommand

// Projectile 指令
SpawnProjectileCommand, MoveProjectileCommand,
ImpactProjectileCommand, RemoveProjectileCommand

// Layer 指令
DestroyLayerCommand, DamageLayerCommand, ShowLayerEffectCommand
```

### 使用方式

```csharp
// 初始化
var choreographer = new Choreographer();
var player = new Player();
player.SyncFromGameState(state);

// 游戏循环
var events = session.DrainEvents();
if (events.Count > 0)
{
    var commands = choreographer.Choreograph(events, player.CurrentTime);
    player.Append(commands);
}
player.Tick(deltaTime);

// 回放
player.SeekTo(targetTime);
player.SkipToEnd();
```

## Consequences

### Positive

1. **最小化跨平台成本**：只需移植 `Player`（~400 行），`Choreographer` 和时序逻辑在 Core 层共享
2. **精确回放**：`RenderCommand[]` 可序列化，完整保存动画序列
3. **回退操作**：保存 `(GameState, RenderCommand[])` 检查点，回退时重新加载
4. **最大化自动化测试**：可对指令流进行断言，验证动画序列正确性
5. **最大化 AI 编码**：`Choreographer` 在 Core 层，AI 可直接分析和生成动画指令
6. **Seek 支持**：`Player.SeekTo(time)` 支持任意时间点跳转

### Negative

1. **指令对象分配**：每次事件转换都会创建 RenderCommand 对象（可通过对象池优化）
2. **两次遍历**：事件先转指令，指令再执行（vs 原来一次遍历）

### Neutral

1. **代码量相当**：删除 `EventInterpreter` + `AnimationTimeline`，新增 `Choreographer` + `Player`
2. **向后兼容**：`VisualState` 接口保持不变，Web 层无需修改

## Alternatives Considered

### 1. 保持原架构，优化 EventInterpreter
- 拒绝：无法解决跨平台移植和回放问题

### 2. 将所有动画逻辑移到 Web 层
- 拒绝：每个平台都要重写动画逻辑，维护成本高

### 3. 使用第三方动画库
- 拒绝：引入外部依赖，增加复杂度，且不解决回放问题

## Related Files

- `src/Match3.Core/Choreography/Choreographer.cs`
- `src/Match3.Core/Choreography/RenderCommand.cs`
- `src/Match3.Core/Choreography/EasingType.cs`
- `src/Match3.Presentation/Player.cs`
- `src/Match3.Presentation/VisualState.cs`

## Related ADRs

- ADR 0003: Event Sourcing + Tick-Based Simulation Architecture
