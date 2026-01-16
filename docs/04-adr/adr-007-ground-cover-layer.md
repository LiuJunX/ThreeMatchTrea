# ADR-007: Ground/Cover 层级扩展设计

## 状态
已批准

## 背景
三消游戏需要扩展两个新的层级：
- **Ground（下层）**：地砖、冰块等，位于 Tile 下方
- **Cover（上层）**：笼子、锁链、泡泡等，位于 Tile 上方，可困住 Tile

## 决策

### 1. 数据结构设计

采用**分离数组**方案，通过 GameState 的访问 API 封装底层实现：

```csharp
public struct GameState
{
    // 底层存储
    internal Tile[] _grid;
    internal Ground[] _groundLayer;
    internal Cover[] _coverLayer;

    // 统一访问 API（隔离底层实现）
    public ref Tile GetTile(Position p);
    public ref Ground GetGround(Position p);
    public ref Cover GetCover(Position p);
    // ...
}

public struct Ground
{
    public GroundType Type;  // None = 无
    public byte Health;      // 默认 1，可按类型自定义
}

public struct Cover
{
    public CoverType Type;   // None = 无
    public byte Health;      // 默认 1，可按类型自定义
    public bool IsDynamic;   // Static 固定 / Dynamic 跟随 Tile
}
```

### 2. Cover 规则（通过类型查表）

| 属性 | 说明 | 存储位置 |
|------|------|----------|
| BlocksMatch | 是否阻止 Tile 参与匹配 | 按 CoverType 查表，Static 可配置，Dynamic 一定不阻止 |
| BlocksSwap | 是否阻止玩家手动交换 | 所有 Cover 都阻止 |
| BlocksMovement | 是否阻止 Tile 移动（重力） | Static = 阻止，Dynamic = 不阻止 |
| IsDynamic | 是否跟随 Tile 移动 | 存储在 Cover 结构中 |

### 3. 消除规则

#### 3.1 消除顺序：Cover → Tile → Ground

```
触发消除（匹配/爆炸/UFO/彩球组合）
    │
    ▼
有 Cover？──是──→ Cover.HP--
    │                 │
    │           HP=0？─┬─否─→ 本轮结束（Tile 受保护）
    │                 │
    │                 是
    │                 ↓
    │            移除 Cover
    │                 ↓
    │            本轮结束（Cover 刚移除，Tile 本次也不消除）
    │
    └──否──→ 消除 Tile
                  │
                  ▼
            有 Ground？──是──→ Ground.HP-- → HP=0 则移除
                  │
                  └──否──→ 完成
```

#### 3.2 关键规则

- **Cover 保护 Tile**：Cover 存在时，一次消除只能消除 Cover，Tile 本次不受影响
- **Cover 消除后的下一次**：同一 Tick 内的后续爆炸波可以消除刚失去 Cover 的 Tile
- **炸弹触发**：Cover 下的炸弹被波及时，只消除 Cover，炸弹本次不触发
- **彩球组合**：清除同色时，有 Cover 的 Tile 只消除 Cover，Tile 保留
- **UFO**：可以选择有 Cover 的 Tile 作为目标，命中时消除 Cover，Tile 保留

### 4. 物理规则

| 场景 | 行为 |
|------|------|
| Static Cover 的 Tile | 不能移动（重力无效） |
| Dynamic Cover 的 Tile | 可以下落，Cover 跟随移动 |
| 交换 Dynamic Cover 的 Tile | 不允许（玩家操作被阻止） |
| Ground | 固定不动，不影响 Tile 移动 |

### 5. Ground 规则

- 不影响匹配规则
- 不影响 Tile 移动
- Tile 消除时，Ground.HP--
- HP = 0 时移除 Ground

### 6. 事件系统

| 事件 | 触发时机 |
|------|----------|
| `CoverDestroyedEvent` | Cover HP 归零被移除时 |
| `GroundDestroyedEvent` | Ground HP 归零被移除时 |

不需要 DamagedEvent（仅扣血不发事件）。

## 系统职责

### 新增系统

| 系统 | 接口 | 职责 |
|------|------|------|
| `GroundSystem` | `IGroundSystem` | Ground 受损、消除 |
| `CoverSystem` | `ICoverSystem` | Cover 受损、消除、规则查询 |

### 现有系统修改

| 系统 | 修改内容 |
|------|----------|
| `GameState` | 新增层级数组、访问 API |
| `ClassicMatchFinder` | 匹配时检查 Cover.BlocksMatch |
| `StandardMatchProcessor` | 消除时遵循 Cover → Tile → Ground 顺序 |
| `ExplosionSystem` | 同上 |
| `PowerUpHandler` | 同上 |
| `BombComboHandler` | 同上 |
| `RealtimeGravitySystem` | Static Cover 阻止移动；Dynamic Cover 同步移动 |
| `RealtimeRefillSystem` | 检查生成位置的 Cover |
| `IInputSystem` | 检查 Cover.BlocksSwap |
| `AIService` | 评估时考虑 Cover/Ground |
| `BoardInitializer` | 初始化 Ground/Cover 层 |
| `Editor` | 支持配置 Ground/Cover（简单实现） |

## 影响范围

### P0 - 必须修改

- `GameState.cs` - 核心数据结构
- `ClassicMatchFinder.cs` - 匹配检测
- `StandardMatchProcessor.cs` - 消除逻辑
- `ExplosionSystem.cs` - 爆炸消除
- `PowerUpHandler.cs` - 炸弹激活
- `RealtimeGravitySystem.cs` - 物理移动
- `GravityTargetResolver.cs` - 目标计算

### P1 - 高优先级

- `RealtimeRefillSystem.cs` - 方块生成
- `BoardInitializer.cs` - 棋盘初始化
- `AIService.cs` - AI 评估
- `BombComboHandler.cs` - 炸弹组合
- 各 BombEffect 类 - 效果应用

### P2 - 中优先级

- Editor 相关文件 - 关卡编辑
- 测试文件 - 测试更新

## 实施计划

| 阶段 | 内容 |
|------|------|
| 1 | 定义 Ground、Cover 结构和枚举 |
| 2 | 扩展 GameState（数组 + 访问 API） |
| 3 | 创建 IGroundSystem、ICoverSystem |
| 4 | 修改消除逻辑（MatchProcessor、ExplosionSystem、PowerUpHandler） |
| 5 | 修改匹配检测（ClassicMatchFinder） |
| 6 | 修改物理系统（Gravity、Refill） |
| 7 | 修改输入系统（交换检查） |
| 8 | 修改 AI 系统 |
| 9 | 修改 BoardInitializer |
| 10 | Editor 支持 |
| 11 | 修复测试、集成测试 |

## 风险与注意事项

| 风险 | 应对措施 |
|------|----------|
| 消除逻辑遗漏导致 Ground 被错误清除 | 统一使用消除辅助方法 |
| Cover 匹配检查遗漏 | MatchFinder 入口统一检查 |
| Dynamic Cover 同步移动遗漏 | 物理系统集中处理 |
| 测试大量失败 | 提供 TestHelper 简化初始化 |

## 相关文档

- `docs/01-architecture/core-patterns.md` - 架构规范
- `docs/02-guides/coding-standards.md` - 代码规范
