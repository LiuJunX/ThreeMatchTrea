# 编辑器功能：自动播放 (Auto Play)

| 文档状态 | 作者 | 日期 | 对应版本 |
| :--- | :--- | :--- | :--- |
| **Implemented** | AI Assistant | 2026-01-17 | v2.0 |

## 1. 概述 (Overview)

Auto Play 是关卡编辑器中的调试辅助功能，用于自动执行有效的消除操作，帮助开发者快速验证关卡设计和游戏机制。

### 1.1 功能定位

| 用途 | 描述 |
| :--- | :--- |
| **关卡验证** | 快速测试关卡是否可通关 |
| **机制调试** | 观察连锁反应、炸弹生成等机制 |
| **性能测试** | 长时间运行观察内存和性能表现 |
| **演示展示** | 自动演示游戏玩法 |

### 1.2 核心特性

- **智能选择**：只执行能产生消除的有效交换
- **炸弹激活**：自动点击可激活的炸弹（如彩虹球）
- **状态感知**：等待动画和物理稳定后才执行下一步
- **加权随机**：从所有有效操作中按权重随机选择，炸弹组合优先

## 2. 系统架构

### 2.1 v2.0 架构（当前）

v2.0 将移动选择逻辑从 Web 层迁移到 Core 层，通过统一的 `IMoveSelector` 接口实现。

```
┌─────────────────────────────────────────────────────────────┐
│                    Auto Play 系统架构 v2.0                   │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│   GameControls.razor                                        │
│       │                                                     │
│       │ ToggleAutoPlay()                                    │
│       ▼                                                     │
│   ┌─────────────────────────────────────────┐               │
│   │     Match3GameService (Web 层)          │               │
│   │  ┌───────────────────────────────────┐  │               │
│   │  │  _isAutoPlaying: bool             │  │               │
│   │  │  _autoPlaySelector: IMoveSelector │  │  ← 协调者角色 │
│   │  └───────────────────────────────────┘  │               │
│   │                                         │               │
│   │  GameLoopAsync()                        │               │
│   │       │                                 │               │
│   │       ├─ IsStable()?                    │               │
│   │       ├─ HasActiveAnimations?           │               │
│   │       ▼                                 │               │
│   │  TryMakeRandomMove() ──────────────────────────┐        │
│   └─────────────────────────────────────────┘      │        │
│                                                    │        │
│   ═══════════════════════════════════════════════  │        │
│                                                    ▼        │
│   ┌─────────────────────────────────────────────────────┐   │
│   │              Core 层 - IMoveSelector                 │   │
│   │  ┌─────────────────────────────────────────────┐    │   │
│   │  │         WeightedMoveSelector                │    │   │
│   │  │  ┌───────────────────────────────────────┐  │    │   │
│   │  │  │  • 穷举搜索所有有效移动               │  │    │   │
│   │  │  │  • 加权随机选择（炸弹优先）           │  │    │   │
│   │  │  │  • 支持点击炸弹                       │  │    │   │
│   │  │  │  • 缓存机制优化性能                   │  │    │   │
│   │  │  └───────────────────────────────────────┘  │    │   │
│   │  └─────────────────────────────────────────────┘    │   │
│   │                                                     │   │
│   │  共享依赖：                                          │   │
│   │  • GridUtility.IsSwapValid() - 交换有效性验证        │   │
│   │  • GridUtility.SwapTilesForCheck() - 临时交换检测    │   │
│   │  • MoveSelectionConfig - 可配置权重                  │   │
│   └─────────────────────────────────────────────────────┘   │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 相关文件

| 文件 | 层 | 职责 |
| :--- | :--- | :--- |
| `Match3.Core/Systems/Selection/IMoveSelector.cs` | Core | 移动选择器统一接口 |
| `Match3.Core/Systems/Selection/WeightedMoveSelector.cs` | Core | 加权移动选择器（Auto Play 核心逻辑） |
| `Match3.Core/Systems/Selection/MoveAction.cs` | Core | 移动操作数据结构 |
| `Match3.Core/Config/MoveSelectionConfig.cs` | Core | 权重配置 |
| `Match3.Core/Utility/GridUtility.cs` | Core | 共享工具方法 |
| `Match3.Web/Services/Match3GameService.cs` | Web | 协调者，状态感知 |
| `Match3.Web/Components/Game/GameControls.razor` | Web | UI 控制按钮 |

### 2.3 IMoveSelector 接口

```csharp
public interface IMoveSelector
{
    string Name { get; }
    bool TryGetMove(in GameState state, out MoveAction action);
    IReadOnlyList<MoveAction> GetAllCandidates(in GameState state);
    void InvalidateCache();
}
```

| 方法 | 说明 |
| :--- | :--- |
| `TryGetMove` | 获取一个有效移动，返回是否成功 |
| `GetAllCandidates` | 获取所有候选移动（用于调试/分析） |
| `InvalidateCache` | 使缓存失效（棋盘变化后调用） |

## 3. 执行流程

### 3.1 触发条件

Auto Play 在每帧的游戏循环中检查以下条件，全部满足时才执行移动：

```csharp
if (_isAutoPlaying &&                              // 1. Auto Play 已启用
    _simulationEngine.IsStable() &&                // 2. 模拟引擎稳定（无下落/消除）
    !_presentationController.HasActiveAnimations)  // 3. 无正在播放的动画
{
    TryMakeRandomMove();
}
```

| 条件 | 说明 |
| :--- | :--- |
| `_isAutoPlaying` | 用户通过按钮开启了自动播放 |
| `IsStable()` | 棋盘无正在下落的方块、无待处理的消除 |
| `!HasActiveAnimations` | 表现层动画已完成（交换动画、消除动画等） |

### 3.2 Web 层协调代码

`Match3GameService.TryMakeRandomMove()` 现在只是一个简单的协调者：

```csharp
private void TryMakeRandomMove()
{
    if (_simulationEngine == null || _autoPlaySelector == null) return;

    // 使棋盘变化后的缓存失效
    _autoPlaySelector.InvalidateCache();

    // 使用 Core 层的加权移动选择器
    var state = _simulationEngine.State;
    if (_autoPlaySelector.TryGetMove(in state, out var action))
    {
        if (action.ActionType == MoveActionType.Tap)
        {
            _simulationEngine.HandleTap(action.From);
        }
        else
        {
            _simulationEngine.ApplyMove(action.From, action.To);
        }
    }
}
```

### 3.3 移动搜索算法

`WeightedMoveSelector.GetAllCandidatesInternal()` 采用穷举搜索找出所有有效移动：

```
步骤 1: 搜索水平交换
┌───────────────────────────────────┐
│  for y = 0 to Height-1:           │
│    for x = 0 to Width-2:          │
│      检查 (x,y) ↔ (x+1,y)         │
└───────────────────────────────────┘

步骤 2: 搜索垂直交换
┌───────────────────────────────────┐
│  for y = 0 to Height-2:           │
│    for x = 0 to Width-1:          │
│      检查 (x,y) ↔ (x,y+1)         │
└───────────────────────────────────┘

步骤 3: 搜索可点击的炸弹
┌───────────────────────────────────┐
│  for each tile in grid:           │
│    if IsTappableBomb(tile):       │
│      添加到候选操作列表            │
└───────────────────────────────────┘

步骤 4: 加权随机选择并执行
┌───────────────────────────────────┐
│  计算每个候选操作的权重            │
│  按权重进行加权随机选择            │
│  返回 MoveAction                  │
└───────────────────────────────────┘
```

### 3.4 加权随机算法

为了让炸弹组合有更高的触发概率，采用加权随机选择策略。

**基础权重表（可配置）**

| 类型 | 默认权重 | 配置路径 |
| :--- | :--- | :--- |
| 普通 Tile | 10 | `MoveSelectionConfig.Weights.Normal` |
| UFO | 20 | `MoveSelectionConfig.Weights.Ufo` |
| 条形炸弹 (Line) | 20 | `MoveSelectionConfig.Weights.Line` |
| 方形炸弹 (Cross) | 30 | `MoveSelectionConfig.Weights.Cross` |
| 彩球 (Rainbow) | 40 | `MoveSelectionConfig.Weights.Rainbow` |

**权重计算规则**

| 操作类型 | 公式 | 说明 |
| :--- | :--- | :--- |
| 普通消除 | 10 + 新炸弹权重 | 基础权重 + 将生成的炸弹权重 |
| 炸弹 + 非炸弹 | A + B + 新炸弹权重 | 权重相加 + 将生成的炸弹权重 |
| 炸弹 + 炸弹 | A × B | 权重相乘（直接触发组合，不生成新炸弹） |
| 点击炸弹 | A | 炸弹自身权重 |

**新炸弹权重加成**：交换后如果能形成 4 连、5 连、T/L 形等，将生成的炸弹权重会加入计算。

**权重示例**

| 组合 | 计算 | 权重 |
| :--- | :--- | :--- |
| 普通 ↔ 普通（3连） | 10 | 10 |
| 普通 ↔ 普通（4连→条炸弹） | 10 + 20 | 30 |
| 普通 ↔ 普通（5连→彩球） | 10 + 40 | 50 |
| 普通 ↔ 普通（T/L→方形炸弹） | 10 + 30 | 40 |
| 条炸弹 ↔ 普通（3连） | 20 + 10 | 30 |
| 条炸弹 ↔ 普通（4连→条炸弹） | 20 + 10 + 20 | 50 |
| 条炸弹 ↔ 条炸弹 | 20 × 20 | 400 |
| 彩球 ↔ 彩球 | 40 × 40 | 1600 |

通过相乘规则，炸弹组合的权重远高于普通交换，会被优先选中，同时保留随机性。

### 3.5 有效性验证

`GridUtility.IsSwapValid()` 提供统一的交换有效性验证：

```csharp
public static bool IsSwapValid(in GameState state, Position from, Position to)
{
    // 边界检查
    if (from.X < 0 || from.X >= state.Width || from.Y < 0 || from.Y >= state.Height)
        return false;
    if (to.X < 0 || to.X >= state.Width || to.Y < 0 || to.Y >= state.Height)
        return false;

    var tileFrom = state.GetTile(from.X, from.Y);
    var tileTo = state.GetTile(to.X, to.Y);

    // 不能交换空格
    if (tileFrom.Type == TileType.None || tileTo.Type == TileType.None)
        return false;

    // 不能交换正在下落的方块
    if (tileFrom.IsFalling || tileTo.IsFalling)
        return false;

    // 不能交换被覆盖层阻挡的方块
    if (!state.CanInteract(from) || !state.CanInteract(to))
        return false;

    return true;
}
```

| 验证层 | 检查内容 | 失败原因 |
| :--- | :--- | :--- |
| **边界检查** | 坐标在棋盘范围内 | 位置越界 |
| **基础有效性** | `TileType != None` | 位置是空格或障碍 |
| **运动状态** | `!IsFalling` | 方块正在下落动画中 |
| **交互性** | `CanInteract()` | 方块被冰块/铁链等覆盖 |

### 3.6 可点击炸弹检测

所有炸弹类型都支持点击激活：

| 炸弹类型 | 可点击 | 说明 |
| :--- | :--- | :--- |
| **条形炸弹 (Line)** | 是 | 点击后清除整行/列 |
| **方形炸弹 (Cross)** | 是 | 点击后清除 5×5 范围 |
| **UFO** | 是 | 点击后发射导弹 |
| **彩球 (Rainbow)** | 是 | 点击后消除最多颜色 |

### 3.7 临时交换技术

为避免修改真实游戏状态，使用"临时交换"技术进行匹配检测：

```csharp
// Match3.Core/Utility/GridUtility.cs
public static void SwapTilesForCheck(ref GameState state, Position a, Position b)
{
    var idxA = a.Y * state.Width + a.X;
    var idxB = b.Y * state.Width + b.X;
    // 只交换 Grid 数组中的引用，不修改 Tile.Position
    (state.Grid[idxA], state.Grid[idxB]) = (state.Grid[idxB], state.Grid[idxA]);
}
```

**设计要点**：
- 操作的是 `GameState` 的副本（值类型语义）
- 不修改 `Tile.Position`，避免影响动画系统
- 交换两次等于还原，确保状态一致性

### 3.8 缓存机制

`WeightedMoveSelector` 支持缓存机制提升性能：

```csharp
// 配置缓存
var config = new MoveSelectionConfig
{
    WeightedSelector = new MoveSelectionConfig.WeightedSelectorConfig
    {
        EnableCaching = true  // 默认开启
    }
};

// 棋盘变化后使缓存失效
selector.InvalidateCache();
```

| 配置项 | 默认值 | 说明 |
| :--- | :--- | :--- |
| `EnableCaching` | true | 是否启用候选列表缓存 |
| `EnableTapBombs` | true | 是否搜索可点击的炸弹 |
| `UseBombMultiplier` | true | 炸弹+炸弹是否使用乘法权重 |

## 4. 用户界面

### 4.1 控制按钮

位于 `GameControls.razor`，提供开关切换：

```html
<button @onclick="ToggleAutoPlay" class="btn-reset"
        style="background-color: #3b82f6;">
    @(GameService.IsAutoPlaying ? "Stop Auto" : "Auto Play")
</button>
```

| 状态 | 按钮文字 | 行为 |
| :--- | :--- | :--- |
| 关闭 | "Auto Play" | 点击启用自动播放 |
| 开启 | "Stop Auto" | 点击停止自动播放 |

### 4.2 配合使用的控件

| 控件 | 用途 |
| :--- | :--- |
| **Pause** | 暂停游戏，Auto Play 也会暂停 |
| **Game Speed** | 调整游戏速度，加快/减慢 Auto Play |
| **Scenarios** | 加载特定测试场景后使用 Auto Play 验证 |

## 5. 与其他系统的关系

### 5.1 依赖的系统

| 系统 | 依赖方式 |
| :--- | :--- |
| **SimulationEngine** | 调用 `IsStable()`、`ApplyMove()`、`HandleTap()` |
| **Player** | 检查 `HasActiveAnimations` |
| **IMatchFinder** | 调用 `FindMatchGroups()` 验证匹配和获取炸弹信息 |
| **GridUtility** | 调用 `IsSwapValid()`、`SwapTilesForCheck()` |
| **IRandom** | 加权随机选择有效操作 |

### 5.2 不影响的系统

- **得分系统**：Auto Play 的移动会正常计分
- **炸弹生成**：4 连、5 连等仍会正常生成炸弹
- **事件系统**：所有游戏事件正常触发和记录

## 6. 与其他移动选择器的对比

v2.0 架构下，所有移动选择器都实现 `IMoveSelector` 接口：

| 特性 | WeightedMoveSelector | RandomMoveSelector | AIService |
| :--- | :--- | :--- | :--- |
| **用途** | Auto Play | 简单 AI/测试 | 高级 AI 分析 |
| **搜索方式** | 穷举所有有效移动 | 随机尝试 N 次 | 穷举 + 模拟预览 |
| **选择策略** | 加权随机（炸弹优先） | 找到即返回 | 策略模式 |
| **点击炸弹** | 支持 | 不支持 | 不支持 |
| **缓存** | 支持 | 无 | 无 |
| **性能** | 中等 | 高 | 低（完整模拟） |

### 6.1 接口统一的好处

```csharp
// 可以轻松切换不同的选择器
IMoveSelector selector = config.UseAdvancedAI
    ? new WeightedMoveSelector(matchFinder, random, config)
    : new RandomMoveSelector(matchFinder, config);

if (selector.TryGetMove(in state, out var action))
{
    // 执行移动
}
```

## 7. 注意事项

### 7.1 性能考虑

- 每帧最多执行一次有效移动搜索
- `WeightedMoveSelector` 支持缓存，避免重复计算
- 临时交换只操作数组索引，开销很小
- `FindMatchGroups()` 返回池化列表，使用后自动释放

### 7.2 已知限制

| 限制 | 说明 |
| :--- | :--- |
| **无死局处理** | 如果无有效移动，不会触发洗牌 |

### 7.3 调试技巧

1. **配合 Pause 使用**：暂停后可以单步观察每次移动
2. **降低 Game Speed**：放慢速度便于观察连锁反应
3. **加载测试场景**：使用 Scenarios 加载特定布局测试
4. **查看候选列表**：调用 `GetAllCandidates()` 查看所有有效移动

## 8. 待实现 (TODO)

| 优化项 | 优先级 | 说明 |
| :--- | :--- | :--- |
| 掉落中可交换 | P2 | 当前 `IsFalling` 时禁止交换，后续支持掉落中交换 |
| 相邻空格交换 | P2 | tile/炸弹可与无障碍覆盖的相邻空格交换 |
| 死局自动洗牌 | P3 | 无有效移动时自动触发洗牌 |

## 9. 版本历史

| 版本 | 日期 | 变更内容 |
| :--- | :--- | :--- |
| v2.0 | 2026-01-17 | 架构重构：Auto Play 逻辑迁移到 Core 层，引入 `IMoveSelector` 统一接口，`WeightedMoveSelector` 实现加权选择，`Match3GameService` 简化为协调者角色 |
| v1.4 | 2026-01-17 | 重构：提取 `SwapTilesForCheck` 到共享工具类 `GridUtility`，消除与 BotSystem 的重复代码 |
| v1.3 | 2026-01-17 | 修复：`FindMatchGroups()` 返回的池化列表需调用 `ReleaseGroups()` 释放 |
| v1.2 | 2026-01-17 | 优化：新炸弹权重加成，交换后将生成的炸弹权重计入 |
| v1.1 | 2026-01-17 | 新增：点击炸弹触发、加权随机算法、炸弹组合优先 |
| v1.0 | 2026-01-17 | 修复：添加匹配检查，只执行有效交换 |
| v0.9 | - | 初始实现：随机交换（有 bug） |
