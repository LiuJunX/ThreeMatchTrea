# Handler / Processor / Manager / Factory 后缀分类规范

> 适用于 Match3 项目全部 C# 代码。新增类时根据职责选用正确后缀。

## 后缀定义

| 后缀 | 职责 | 状态 | 典型签名 |
|------|------|------|----------|
| **Processor** | 输入 → 输出转换；实现领域接口 | 无状态 | `Process(in Input) → Output` |
| **Manager** | 管理有生命周期的有状态实体 | 有状态 | `Add / Remove / Update / Reset` |
| **Handler** | 路由/委托到子系统；编排多步骤流程 | 通常无状态 | `Handle*(ref state, ...)` |
| **Factory** | 创建实例；封装构造逻辑 | 无状态 | `Create*() → T` |

## 项目示例

### Processor
- `MatchProcessor` — 将 match 列表转换为消除操作（`IMatchProcessor`）
- `ConfigParser*` — 将原始配置转换为类型化配置对象

### Manager
- `ColorBombSessionManager` — 管理 ColorBomb 动画 session 的生命周期（创建、tick 更新、销毁）
- `ObjectiveManager` — 跟踪关卡目标完成状态

### Handler
- `SimulationMatchHandler` — 编排 match 发现 → 消除 → 目标更新 → 炸弹生成流程
- `ChainReactionHandler` — 委托爆炸/弹射链式反应到子系统
- `BombComboHandler` — 根据炸弹类型组合路由到对应处理逻辑
- `SimulationInputHandler` — 路由玩家输入（tap/swap）到对应操作

### Factory
- `PowerUpHandlerFactory` — 创建/克隆 `BombResolution` 实例
- `ObstaclePresenterFactory` — 根据障碍类型创建对应 Presenter

## 边界情况

- `BombResolution` 实现了 `IPowerUpHandler`，虽然名字没有后缀，但职责是 Handler（路由炸弹激活到效果系统）。历史命名，保留不改。
- `LockScheduler` 管理定时锁生命周期，职责接近 Manager，但使用 Scheduler 后缀更精确地表达其 tick 驱动的调度语义。
