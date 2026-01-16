# 核心机制：匹配系统 (Matching System)

| 文档状态 | 作者 | 日期 | 对应版本 |
| :--- | :--- | :--- | :--- |
| **Implemented** | AI Assistant | 2026-01-14 | v1.0 |

## 1. 概述 (Overview)

匹配系统是 Match-3 游戏的核心，负责检测消除、生成炸弹、处理级联反应。系统分为两个主要组件：

| 组件 | 职责 | 实现类 |
| :--- | :--- | :--- |
| **MatchFinder** | 检测棋盘上的所有匹配组 | `ClassicMatchFinder` |
| **MatchProcessor** | 处理消除、生成炸弹、计算得分 | `StandardMatchProcessor` |

### 1.1 设计目标

| 目标 | 描述 |
| :--- | :--- |
| **零分配** | 使用对象池避免 GC 压力 |
| **可扩展** | 通过接口解耦，支持不同匹配规则 |
| **级联支持** | 自动处理连锁反应 |
| **炸弹集成** | 与 BombGenerator 协同工作 |

### 1.2 系统流程

```
┌─────────────────────────────────────────────────────────────┐
│                    匹配系统处理流程                          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│   玩家交换                                                   │
│       │                                                     │
│       ▼                                                     │
│   ┌─────────────────┐                                       │
│   │  ClassicMatchFinder.FindMatchGroups()                   │
│   │  - 扫描棋盘                                              │
│   │  - 找出连通分量                                          │
│   │  - 检测形状 (3连/4连/5连/T/L/方块)                       │
│   │  - 确定炸弹生成                                          │
│   └────────┬────────┘                                       │
│            │                                                │
│            ▼                                                │
│   ┌─────────────────┐                                       │
│   │  StandardMatchProcessor.ProcessMatches()                │
│   │  - 计算得分                                              │
│   │  - 生成炸弹                                              │
│   │  - 清除方块                                              │
│   │  - 触发炸弹连锁                                          │
│   └────────┬────────┘                                       │
│            │                                                │
│            ▼                                                │
│   重力下落 → 填充 → 再次检测 (级联)                          │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 2. 核心接口 (Core Interfaces)

### 2.1 IMatchFinder

```csharp
public interface IMatchFinder
{
    /// <summary>
    /// 找出棋盘上所有匹配组
    /// </summary>
    /// <param name="state">当前游戏状态（只读）</param>
    /// <param name="foci">可选的焦点位置（用于确定炸弹生成位置）</param>
    /// <returns>匹配组列表</returns>
    List<MatchGroup> FindMatchGroups(in GameState state, IEnumerable<Position>? foci = null);

    /// <summary>
    /// 检查棋盘是否存在任何匹配
    /// </summary>
    bool HasMatches(in GameState state);

    /// <summary>
    /// 检查指定位置是否属于某个匹配
    /// 优化：无需扫描整个棋盘
    /// </summary>
    bool HasMatchAt(in GameState state, Position p);
}
```

### 2.2 IMatchProcessor

```csharp
public interface IMatchProcessor
{
    /// <summary>
    /// 处理匹配组：消除方块、生成炸弹、计算得分
    /// </summary>
    /// <param name="state">游戏状态（可写）</param>
    /// <param name="groups">要处理的匹配组</param>
    /// <returns>本次处理获得的总分</returns>
    int ProcessMatches(ref GameState state, List<MatchGroup> groups);
}
```

---

## 3. 数据结构 (Data Structures)

### 3.1 MatchGroup

表示一组需要消除的方块。

```csharp
public class MatchGroup
{
    public TileType Type;                    // 方块颜色
    public HashSet<Position> Positions;      // 所有参与消除的位置
    public MatchShape Shape;                 // 匹配形状
    public Position? BombOrigin;             // 炸弹生成位置
    public BombType SpawnBombType;           // 要生成的炸弹类型
}
```

### 3.2 MatchShape

匹配的几何形状，决定是否生成炸弹。

```csharp
public enum MatchShape
{
    Simple3,           // 普通 3 连 - 不生成炸弹
    Line4Horizontal,   // 横向 4 连 - 生成横向火箭
    Line4Vertical,     // 纵向 4 连 - 生成纵向火箭
    Line5,             // 5 连直线 - 生成彩虹球
    Cross,             // T型 或 L型 - 生成方块炸弹
    Square             // 2x2 方块 - 生成 UFO
}
```

---

## 4. 匹配检测算法 (Detection Algorithm)

### 4.1 算法概述

`ClassicMatchFinder` 使用 **连通分量 + 形状检测** 的两阶段算法：

```
阶段 1: 连通分量 (Connected Component)
├── BFS 遍历找出所有相邻同色方块
└── 返回一个 HashSet<Position>

阶段 2: 形状检测 (Shape Detection)
├── 委托给 BombGenerator
├── 检测直线、T型、L型、方块等形状
└── 返回 List<MatchGroup>
```

### 4.2 连通分量算法

```csharp
private HashSet<Position> GetConnectedComponent(in GameState state, Position start, TileType type)
{
    var component = Pools.ObtainHashSet<Position>();
    var queue = Pools.ObtainQueue<Position>();

    queue.Enqueue(start);
    component.Add(start);

    while (queue.Count > 0)
    {
        var curr = queue.Dequeue();
        // 检查四个方向的邻居
        CheckNeighbor(state, curr.X + 1, curr.Y, type, component, queue);  // 右
        CheckNeighbor(state, curr.X - 1, curr.Y, type, component, queue);  // 左
        CheckNeighbor(state, curr.X, curr.Y + 1, type, component, queue);  // 下
        CheckNeighbor(state, curr.X, curr.Y - 1, type, component, queue);  // 上
    }

    return component;
}
```

**复杂度：** O(W × H)，每个格子最多访问一次。

### 4.3 单点匹配检测

`HasMatchAt` 提供快速的单点检测，用于验证交换是否有效。

支持的匹配类型：
- **直线匹配**：横向或纵向 3+ 连续相同颜色
- **2x2 方块匹配**：形成 2x2 正方形（生成 UFO）

```csharp
public bool HasMatchAt(in GameState state, Position p)
{
    var type = state.GetType(p.X, p.Y);
    if (type == TileType.None) return false;

    // 检查横向 3+
    int hCount = 1;
    for (int i = p.X - 1; i >= 0 && state.GetType(i, p.Y) == type; i--) hCount++;
    for (int i = p.X + 1; i < w && state.GetType(i, p.Y) == type; i++) hCount++;
    if (hCount >= 3) return true;

    // 检查纵向 3+
    int vCount = 1;
    for (int i = p.Y - 1; i >= 0 && state.GetType(p.X, i) == type; i--) vCount++;
    for (int i = p.Y + 1; i < h && state.GetType(p.X, i) == type; i++) vCount++;
    if (vCount >= 3) return true;

    // 检查 2x2 方块（位置可能是方块的任意一个角）
    return Has2x2SquareAt(in state, p.X, p.Y, type);
}
```

**2x2 检测算法**：检查目标位置作为 4 个角时，是否能形成 2x2 方块：

```
位置 p 可能是:           检查的 4 种情况:
                        ┌───┬───┐   ┌───┬───┐
  (1) 左上角:  p ■      │ p │   │   │   │ p │  (2) 右上角
               ■ ■      │   │   │   │   │   │
                        └───┴───┘   └───┴───┘
                        ┌───┬───┐   ┌───┬───┐
  (3) 左下角:  ■ ■      │   │   │   │   │   │  (4) 右下角
               p ■      │ p │   │   │   │ p │
                        └───┴───┘   └───┴───┘
```

**复杂度：** O(W + H) 直线扫描 + O(1) 方块检测。

---

## 5. 匹配处理流程 (Processing Flow)

### 5.1 StandardMatchProcessor 处理步骤

```
┌─────────────────────────────────────────────────────────────┐
│                    ProcessMatches 流程                       │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. 收集所有待清除位置                                        │
│     └─ tilesToClear = 所有 MatchGroup 的 Positions          │
│                                                             │
│  2. 处理炸弹生成                                              │
│     ├─ 如果 SpawnBombType != None                           │
│     ├─ 从 tilesToClear 移除 BombOrigin                      │
│     ├─ 加入 protectedTiles                                  │
│     └─ 在 BombOrigin 位置放置新炸弹                          │
│                                                             │
│  3. 消除方块（含炸弹连锁）                                    │
│     ├─ 使用队列处理                                          │
│     ├─ 如果方块是炸弹 → 触发爆炸效果                         │
│     ├─ 爆炸范围加入队列                                      │
│     └─ 清除方块 (SetTile → TileType.None)                   │
│                                                             │
│  4. 计算并返回得分                                            │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 5.2 炸弹连锁处理

当消除的方块带有炸弹时，自动触发连锁：

```csharp
if (t.Bomb != BombType.None)
{
    if (_bombRegistry.TryGetEffect(t.Bomb, out var effect))
    {
        effect.Apply(in state, p, explosionRange);

        foreach (var exP in explosionRange)
        {
            if (!cleared.Contains(exP))
                queue.Enqueue(exP);  // 加入消除队列
        }
    }
}
```

这确保了炸弹爆炸会触发其他炸弹，形成连锁反应。

---

## 6. 级联机制 (Cascade System)

### 6.1 级联流程

级联由 `AsyncGameLoopSystem` 协调，匹配系统仅负责单次检测和处理：

```
AsyncGameLoopSystem.Update():
│
├── 1. RealtimeRefillSystem.Update()    // 填充空位
├── 2. RealtimeGravitySystem.Update()   // 重力下落
└── 3. 匹配检测
        ├── ClassicMatchFinder.FindMatchGroups()
        ├── 过滤：只处理"稳定"的匹配组 (IsGroupStable)
        └── StandardMatchProcessor.ProcessMatches()
            └── 消除后，循环回到步骤 1
```

### 6.2 稳定性检测

只有当匹配组中所有方块都停止移动时，才会处理消除：

```csharp
private bool IsGroupStable(ref GameState state, MatchGroup group)
{
    foreach (var p in group.Positions)
    {
        var tile = state.GetTile(p.X, p.Y);
        if (tile.IsFalling) return false;  // 仍在下落，不稳定
    }
    return true;
}
```

---

## 7. 性能优化 (Performance)

### 7.1 对象池使用

所有集合都使用对象池，避免 GC：

```csharp
var groups = Pools.ObtainList<MatchGroup>();
var visited = Pools.ObtainHashSet<Position>();

try
{
    // 使用集合...
}
finally
{
    Pools.Release(visited);
    // groups 由调用者释放
}
```

### 7.2 释放规范

调用 `FindMatchGroups` 的代码负责释放返回的 `List<MatchGroup>`：

```csharp
var matches = _matchFinder.FindMatchGroups(state);
try
{
    // 处理匹配...
}
finally
{
    ClassicMatchFinder.ReleaseGroups(matches);
}
```

---

## 8. 与其他系统的协作 (Integration)

### 8.1 系统依赖

```
┌─────────────────┐     ┌─────────────────┐
│  ClassicMatch   │────▶│  BombGenerator  │
│     Finder      │     │  (形状检测)      │
└────────┬────────┘     └─────────────────┘
         │
         ▼
┌─────────────────┐     ┌─────────────────┐
│ StandardMatch   │────▶│  BombEffect     │
│   Processor     │     │  Registry       │
└────────┬────────┘     └─────────────────┘
         │
         ▼
┌─────────────────┐
│   ScoreSystem   │
│   (计分)         │
└─────────────────┘
```

### 8.2 调用示例

```csharp
// 在 AsyncGameLoopSystem 中
var allMatches = _matchFinder.FindMatchGroups(state);

if (allMatches.Count > 0)
{
    var stableGroups = FilterStableGroups(allMatches);

    if (stableGroups.Count > 0)
    {
        _matchProcessor.ProcessMatches(ref state, stableGroups);
    }
}
```

---

## 9. 扩展点 (Extension Points)

### 9.1 自定义匹配规则

实现 `IMatchFinder` 接口可以支持不同的匹配规则：

| 场景 | 实现方式 |
| :--- | :--- |
| 斜向匹配 | 在连通分量算法中加入对角方向 |
| 非矩形棋盘 | 修改边界检测逻辑 |
| 特殊方块 | 在 `HasMatchAt` 中加入特殊处理 |

### 9.2 自定义处理逻辑

实现 `IMatchProcessor` 可以自定义消除行为：

| 场景 | 实现方式 |
| :--- | :--- |
| 延迟消除 | 分阶段处理，加入动画等待 |
| 特殊效果 | 在消除前/后触发事件 |
| 分数倍率 | 在计分时加入 Combo 倍数 |

---

## 10. 文件结构 (File Structure)

```
src/Match3.Core/Systems/Matching/
├── IMatchFinder.cs           # 匹配检测接口
├── IMatchProcessor.cs        # 匹配处理接口
├── IBombGenerator.cs         # 炸弹生成接口
├── IShapeRule.cs             # 形状规则接口
├── ClassicMatchFinder.cs     # 经典匹配检测实现
├── StandardMatchProcessor.cs # 标准处理实现
└── Generation/
    ├── BombGenerator.cs      # 炸弹生成实现
    ├── BombDefinitions.cs    # 炸弹定义
    ├── ShapeDetector.cs      # 形状检测
    ├── ShapeFeature.cs       # 形状特征
    └── Rules/
        ├── LineRule.cs           # 直线匹配规则 (3/4/5连)
        ├── IntersectionRule.cs   # 交叉匹配规则 (T/L型)
        └── SquareRule.cs         # 方块匹配规则 (2x2)

src/Match3.Core/Models/Gameplay/
└── MatchGroup.cs             # 匹配组数据结构

src/Match3.Core.Tests/Systems/Matching/
├── ClassicMatchFinderTests.cs
├── StandardMatchProcessorTests.cs
├── BombGeneratorTests.cs
├── BombGeneratorComprehensiveTests.cs
├── BombGeneratorPerformanceTests.cs
└── ExplosionRangeTests.cs
```

---

## 11. 已知限制 (Known Limitations)

| 限制 | 描述 | 改进方向 |
| :--- | :--- | :--- |
| 无 Combo 计数 | 级联不累计连击数 | 在 AsyncGameLoopSystem 中跟踪 |
| 无消除动画 | 方块直接消失 | 在 Processor 中加入动画事件 |
| 单线程 | 大棋盘可能有性能瓶颈 | 考虑分区域并行检测 |

---

## 12. 参考文档 (References)

- [炸弹生成规则](bomb-generation.md) - 详细的炸弹生成逻辑
- [生成点模型](spawn-model.md) - 填充系统
- [重力系统](../mechanics_gravity.md) - 下落机制
