# 编辑器功能：自动播放 (Auto Play)

| 文档状态 | 作者 | 日期 | 对应版本 |
| :--- | :--- | :--- | :--- |
| **Implemented** | AI Assistant | 2026-01-17 | v1.4 |

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
- **随机性**：从所有有效操作中随机选择，模拟真实玩家行为

## 2. 系统架构

### 2.1 组件关系

```
┌─────────────────────────────────────────────────────────────┐
│                    Auto Play 系统架构                        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│   GameControls.razor                                        │
│       │                                                     │
│       │ ToggleAutoPlay()                                    │
│       ▼                                                     │
│   ┌─────────────────────────────────────────┐               │
│   │         Match3GameService               │               │
│   │  ┌───────────────────────────────────┐  │               │
│   │  │  _isAutoPlaying: bool             │  │               │
│   │  │  _matchFinder: IMatchFinder       │  │               │
│   │  │  _candidateActions: List<Action>  │  │               │
│   │  └───────────────────────────────────┘  │               │
│   │                                         │               │
│   │  GameLoopAsync()                        │               │
│   │       │                                 │               │
│   │       ├─ IsStable()?                    │               │
│   │       ├─ HasActiveAnimations?           │               │
│   │       │                                 │               │
│   │       ▼                                 │               │
│   │  TryMakeRandomMove()                    │               │
│   │       │                                 │               │
│   │       ├─ 扫描所有可能的交换              │               │
│   │       ├─ 验证交换有效性                  │               │
│   │       ├─ 检查是否产生匹配                │               │
│   │       └─ 随机选择并执行                  │               │
│   └─────────────────────────────────────────┘               │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 相关文件

| 文件 | 职责 |
| :--- | :--- |
| `Match3.Web/Services/Match3GameService.cs` | Auto Play 核心逻辑 |
| `Match3.Web/Components/Game/GameControls.razor` | UI 控制按钮 |
| `Match3.Core/Systems/Matching/IMatchFinder.cs` | 匹配检测接口 |
| `Match3.Core/Utility/GridUtility.cs` | 临时交换工具方法（与 BotSystem 共享） |

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

### 3.2 移动搜索算法

`TryMakeRandomMove()` 采用穷举搜索找出所有有效移动：

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
│  执行 ApplyMove() 或 HandleTap()  │
└───────────────────────────────────┘
```

### 3.3 加权随机算法

为了让炸弹组合有更高的触发概率，采用加权随机选择策略。

**基础权重表**

| 类型 | 权重 |
| :--- | :--- |
| 普通 Tile | 10 |
| UFO | 20 |
| 条形炸弹 (Line) | 20 |
| 方形炸弹 (Cross) | 30 |
| 彩球 (Rainbow) | 40 |

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

### 3.4 有效性验证与权重计算

`TryAddSwapAction()` 对每个候选交换进行验证并计算权重：

```csharp
// 第 1 层：基础有效性检查
if (tileA.Type == TileType.None || tileB.Type == TileType.None)
    return;  // 不能交换空格

// 第 2 层：运动状态检查
if (tileA.IsFalling || tileB.IsFalling)
    return;  // 不能交换正在下落的方块

// 第 3 层：交互性检查
if (!state.CanInteract(from) || !state.CanInteract(to))
    return;  // 不能交换被覆盖层阻挡的方块

// 第 4 层：权重计算
if (isBombA && isBombB)
{
    weight = weightA * weightB;  // 炸弹+炸弹：相乘
}
else
{
    // 临时交换并获取匹配信息（使用共享工具类）
    GridUtility.SwapTilesForCheck(ref state, from, to);
    var matchGroups = _matchFinder.FindMatchGroups(in state, foci);
    GridUtility.SwapTilesForCheck(ref state, from, to);  // 交换回来

    if (matchGroups.Count == 0)
    {
        ClassicMatchFinder.ReleaseGroups(matchGroups);  // 释放池化列表
        return;  // 无匹配
    }

    // 基础权重 + 新炸弹权重
    weight = isBombA || isBombB ? weightA + weightB : WeightNormal;
    foreach (var group in matchGroups)
    {
        if (group.SpawnBombType != BombType.None)
            weight += GetBombWeight(group.SpawnBombType);
    }

    ClassicMatchFinder.ReleaseGroups(matchGroups);  // 释放池化列表
}
```

| 验证层 | 检查内容 | 失败原因 |
| :--- | :--- | :--- |
| **基础有效性** | `TileType != None` | 位置是空格或障碍 |
| **运动状态** | `!IsFalling` | 方块正在下落动画中 |
| **交互性** | `CanInteract()` | 方块被冰块/铁链等覆盖 |
| **匹配检查** | `FindMatchGroups()` | 交换后不会产生 3 连以上 |

### 3.5 可点击炸弹检测

`IsTappableBomb()` 检测可通过点击激活的炸弹：

```csharp
private static bool IsTappableBomb(in Tile tile)
{
    return tile.Bomb != BombType.None;
}
```

| 炸弹类型 | 可点击 | 说明 |
| :--- | :--- | :--- |
| **条形炸弹 (Line)** | 是 | 点击后清除整行/列 |
| **方形炸弹 (Cross)** | 是 | 点击后清除 5×5 范围 |
| **UFO** | 是 | 点击后发射导弹 |
| **彩球 (Rainbow)** | 是 | 点击后消除最多颜色 |

所有炸弹类型都支持点击激活。

### 3.6 临时交换技术

为避免修改真实游戏状态，使用"临时交换"技术进行匹配检测。该方法已提取到共享工具类 `GridUtility`，供 Auto Play 和 BotSystem 共同使用：

```csharp
// Match3.Core/Utility/GridUtility.cs
public static class GridUtility
{
    public static void SwapTilesForCheck(ref GameState state, Position a, Position b)
    {
        var idxA = a.Y * state.Width + a.X;
        var idxB = b.Y * state.Width + b.X;
        // 只交换 Grid 数组中的引用，不修改 Tile.Position
        (state.Grid[idxA], state.Grid[idxB]) = (state.Grid[idxB], state.Grid[idxA]);
    }
}
```

**设计要点**：
- 操作的是 `GameState` 的副本（值类型语义）
- 不修改 `Tile.Position`，避免影响动画系统
- 交换两次等于还原，确保状态一致性
- 作为共享工具类，消除了 Auto Play 和 BotSystem 之间的重复代码

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
| **PresentationController** | 检查 `HasActiveAnimations` |
| **IMatchFinder** | 调用 `FindMatchGroups()` 验证匹配和获取炸弹信息，使用 `ReleaseGroups()` 释放 |
| **GridUtility** | 调用 `SwapTilesForCheck()` 进行临时交换检测 |
| **IRandom** | 加权随机选择有效操作 |

### 5.2 不影响的系统

- **得分系统**：Auto Play 的移动会正常计分
- **炸弹生成**：4 连、5 连等仍会正常生成炸弹
- **事件系统**：所有游戏事件正常触发和记录

## 6. 与 BotSystem 的区别

项目中存在两套自动移动实现，用途不同，但共享 `GridUtility.SwapTilesForCheck()` 工具方法：

| 特性 | Auto Play (Match3GameService) | BotSystem |
| :--- | :--- | :--- |
| **所在层** | Web 表现层 | Core 核心层 |
| **用途** | 编辑器调试/演示 | AI 对手/自动测试 |
| **搜索方式** | 穷举所有有效移动 | 随机尝试 20 次 |
| **选择策略** | 加权随机（炸弹优先） | 找到即返回 |
| **点击炸弹** | 支持 | 不支持 |
| **状态检查** | 等待动画完成 | 不考虑动画 |
| **随机源** | UI Random | State Random |
| **共享代码** | `GridUtility.SwapTilesForCheck()` | `GridUtility.SwapTilesForCheck()` |

## 7. 注意事项

### 7.1 性能考虑

- 每帧最多执行一次有效移动搜索
- 使用预分配的 `_candidateActions` 列表避免 GC
- 临时交换只操作数组索引，开销很小
- `FindMatchGroups()` 会有一定开销，但仅在棋盘稳定时调用
- `FindMatchGroups()` 返回池化列表，使用后调用 `ClassicMatchFinder.ReleaseGroups()` 释放

### 7.2 已知限制

| 限制 | 说明 |
| :--- | :--- |
| **无死局处理** | 如果无有效移动，不会触发洗牌 |

### 7.3 调试技巧

1. **配合 Pause 使用**：暂停后可以单步观察每次移动
2. **降低 Game Speed**：放慢速度便于观察连锁反应
3. **加载测试场景**：使用 Scenarios 加载特定布局测试

## 8. 待实现 (TODO)

| 优化项 | 优先级 | 说明 |
| :--- | :--- | :--- |
| 掉落中可交换 | P2 | 当前 `IsFalling` 时禁止交换，后续支持掉落中交换 |
| 相邻空格交换 | P2 | tile/炸弹可与无障碍覆盖的相邻空格交换 |
| 死局自动洗牌 | P3 | 无有效移动时自动触发洗牌 |

## 9. 版本历史

| 版本 | 日期 | 变更内容 |
| :--- | :--- | :--- |
| v1.4 | 2026-01-17 | 重构：提取 `SwapTilesForCheck` 到共享工具类 `GridUtility`，消除与 BotSystem 的重复代码 |
| v1.3 | 2026-01-17 | 修复：`FindMatchGroups()` 返回的池化列表需调用 `ReleaseGroups()` 释放 |
| v1.2 | 2026-01-17 | 优化：新炸弹权重加成，交换后将生成的炸弹权重计入 |
| v1.1 | 2026-01-17 | 新增：点击炸弹触发、加权随机算法、炸弹组合优先 |
| v1.0 | 2026-01-17 | 修复：添加匹配检查，只执行有效交换 |
| v0.9 | - | 初始实现：随机交换（有 bug） |
