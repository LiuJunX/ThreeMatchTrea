# PowerUps 系统文档

## 概述

PowerUps 系统负责处理 Match-3 游戏中的炸弹效果和组合爆炸。系统采用策略模式和注册表模式，支持灵活扩展新的炸弹类型。

## 架构设计

```
Match3.Core.Systems.PowerUps/
├── IPowerUpHandler.cs          # 接口定义
├── PowerUpHandler.cs           # 主处理器（整合入口）
├── BombComboHandler.cs         # 组合炸弹处理器
├── BombEffectRegistry.cs       # 炸弹效果注册表
└── Effects/
    ├── IBombEffect.cs          # 炸弹效果接口
    ├── HorizontalRocketEffect.cs
    ├── VerticalRocketEffect.cs
    ├── SquareBombEffect.cs
    ├── ColorBombEffect.cs
    └── UfoEffect.cs
```

## 炸弹类型 (BombType)

| 枚举值 | 名称 | 描述 |
|--------|------|------|
| `None` | 无 | 普通方块，不是炸弹 |
| `Horizontal` | 横向火箭 | 消除整行 |
| `Vertical` | 纵向火箭 | 消除整列 |
| `Square5x5` | 方块炸弹 | 消除 5×5 区域 |
| `Color` | 彩球 | 消除出现最多的颜色 |
| `Ufo` | UFO | 小十字 + 随机消除 1 个方块 |

---

## 单个炸弹效果

### 1. 横向火箭 (HorizontalRocketEffect)

**触发条件**: 4 连消（横向匹配）生成

**效果**: 消除炸弹所在的整行

**影响范围**: 宽度 = 棋盘宽度，高度 = 1

```
示例 (8×8 棋盘，火箭在 y=3):
□□□□□□□□
□□□□□□□□
□□□□□□□□
████████  ← 消除整行
□□□□□□□□
□□□□□□□□
□□□□□□□□
□□□□□□□□
```

### 2. 纵向火箭 (VerticalRocketEffect)

**触发条件**: 4 连消（纵向匹配）生成

**效果**: 消除炸弹所在的整列

**影响范围**: 宽度 = 1，高度 = 棋盘高度

```
示例 (8×8 棋盘，火箭在 x=3):
□□□█□□□□
□□□█□□□□
□□□█□□□□
□□□█□□□□  ← 消除整列
□□□█□□□□
□□□█□□□□
□□□█□□□□
□□□█□□□□
```

### 3. 方块炸弹 (SquareBombEffect)

**触发条件**: L 形或 T 形匹配生成

**效果**: 消除以炸弹为中心的 5×5 区域

**影响范围**: 中心点 ± 2（共 25 格，边界会裁剪）

```
示例 (中心在 (4,4)):
□□████████□□
□□████████□□
████████████
████████████
████████████  ← 5×5 区域
████████████
████████████
□□████████□□

边角情况 (中心在 (0,0)):
███□□□□□
███□□□□□
███□□□□□  ← 只有 3×3 在边界内
□□□□□□□□
```

### 4. 彩球 (ColorBombEffect)

**触发条件**: 5 连消生成

**效果**: 消除棋盘上数量最多的颜色

**颜色选择规则**:
- 只统计普通颜色 (Red, Blue, Green, Yellow, Purple, Orange)
- 忽略 Rainbow、Bomb、None 类型
- 数量相同时选择先遍历到的颜色

```
示例 (红色 10 个，蓝色 5 个):
红红红红红红红红  ← 全部红色被消除
红红□□□□□□
蓝蓝蓝蓝蓝□□□  ← 蓝色保留
□□□□□□□□
```

### 5. UFO (UfoEffect)

**触发条件**: 2×2 正方形匹配生成

**效果**:
1. 原地产生小十字（中心 + 上下左右，共 5 格）
2. 飞向棋盘随机位置，消除 1 个方块

**影响范围**: 5 + 1 = 6 格（边界会裁剪小十字）

```
示例 (UFO 在 (4,4)):
□□□□█□□□
□□□███□□  ← 小十字
□□□□█□□□
□□□□□□□□
□□□□□□█□  ← 随机目标
□□□□□□□□
```

---

## 组合炸弹效果

当两个炸弹相邻交换时触发组合效果。

### 组合规则总览

| 组合 | 效果 |
|------|------|
| 火箭 + 火箭 | 十字（1 行 + 1 列） |
| 火箭 + 方块炸弹 | 3 行 + 3 列 |
| 火箭 + UFO | 小十字 + 随机位置一行/列 |
| 火箭 + 彩球 | 最多颜色全变火箭并爆炸 |
| 方块炸弹 + 方块炸弹 | 9×9 区域 |
| 方块炸弹 + UFO | 小十字 + 随机位置 5×5 |
| 方块炸弹 + 彩球 | 最多颜色全变 3×3 炸弹并爆炸 |
| UFO + UFO | 两个小十字 + 飞出 3 个 UFO |
| UFO + 彩球 | 最多颜色全变 UFO 并起飞 |
| 彩球 + 彩球 | 全屏消除 |

### 详细说明

#### 1. 火箭 + 火箭 = 十字

无论是 H+H、V+V 还是 H+V，都产生十字效果。

**影响范围**: 整行 + 整列 - 1 交点 = 15 格 (8×8 棋盘)

```
□□□□█□□□
□□□□█□□□
□□□□█□□□
████████  ← 十字
□□□□█□□□
□□□□█□□□
□□□□█□□□
□□□□█□□□
```

#### 2. 火箭 + 方块炸弹 = 3 行 + 3 列

**影响范围**: 3 行 × 宽度 + 3 列 × 高度 - 3×3 重叠 = 39 格

```
□□□███□□
□□□███□□
□□□███□□
████████  ← 中心行
████████
████████
□□□███□□
□□□███□□
```

#### 3. 火箭 + UFO = 小十字 + 一行/列

1. UFO 原地产生小十字
2. UFO 飞到随机位置
3. 根据火箭类型决定消除行或列（横向火箭=消除行，纵向火箭=消除列）

#### 4. 火箭 + 彩球 = 颜色变火箭

1. 找出棋盘上数量最多的颜色
2. 该颜色的所有方块变成火箭
3. 所有火箭同时爆炸

**影响**: 如果最多颜色有 N 个，横向火箭组合会消除 N 行

#### 5. 方块炸弹 + 方块炸弹 = 9×9

**影响范围**: 中心 ± 4 = 9×9 = 81 格（边界裁剪）

```
9×9 完整覆盖需要至少 9×9 的棋盘
在 8×8 棋盘上，中心(4,4) 会消除全部 64 格
```

#### 6. 方块炸弹 + UFO = 小十字 + 5×5

1. UFO 原地产生小十字
2. UFO 飞到随机位置
3. 在目标位置产生 5×5 爆炸

#### 7. 方块炸弹 + 彩球 = 颜色变 3×3 炸弹

1. 找出最多颜色
2. 该颜色的所有方块变成 3×3 炸弹
3. 所有炸弹同时爆炸

#### 8. UFO + UFO = 双小十字 + 3 UFO

1. 两个 UFO 原地各产生小十字
2. 飞出 3 个 UFO，各击中 1 个随机目标

**影响**: 2 × 5 + 3 = 13 格（可能有重叠）

#### 9. UFO + 彩球 = 颜色变 UFO

1. 找出最多颜色
2. 该颜色的所有方块变成 UFO
3. 所有 UFO 同时起飞（各自小十字 + 随机目标）

#### 10. 彩球 + 彩球 = 全屏消除

消除棋盘上的所有方块。

**影响范围**: 宽度 × 高度（如 8×8 = 64 格）

---

## 彩球颜色选择规则

### 规则区分

| 场景 | 颜色选择 |
|------|----------|
| 手动交换彩球 + 普通方块 | **指定颜色**（被交换方块的颜色） |
| 彩球 + 其他炸弹组合 | **最多颜色**（棋盘上数量最多的颜色） |
| 单独激活彩球 | **最多颜色** |

### 最多颜色算法

```csharp
// 伪代码
TileType FindMostFrequentColor(GameState state)
{
    // 1. 遍历棋盘，统计每种颜色数量
    // 2. 跳过 None、Rainbow、Bomb 类型
    // 3. 返回数量最多的颜色
    // 4. 如果数量相同，返回先遍历到的
}
```

---

## 连锁爆炸机制

当炸弹被其他爆炸波及时，会触发**递归连锁爆炸**：

1. 收集所有受影响的位置
2. 对于每个受影响位置：
   - 如果已经是空的，跳过
   - 如果是炸弹：先清除炸弹本身，再触发其效果，递归处理连锁
   - 如果是普通方块：直接清除

**为什么不会无限递归？**
- 炸弹在触发效果**之前**就被清除（设为 None）
- 当连锁效果再次击中该位置时，该位置已经是 None，会被跳过

```
示例：三级连锁爆炸
H火箭(2,3) → V火箭(5,3) → 方块炸弹(5,6)

1. 激活 H火箭(2,3)
   - 清除 H火箭 → 状态变为 None
   - 消除整行 y=3，包括位置 (5,3)

2. 处理位置 (5,3) 发现是 V火箭
   - 清除 V火箭 → 状态变为 None
   - 消除整列 x=5，包括位置 (5,6)

3. 处理位置 (5,6) 发现是方块炸弹
   - 清除方块炸弹 → 状态变为 None
   - 消除 5×5 区域
```

```csharp
// PowerUpHandler.ClearTileWithChain 递归连锁逻辑
private void ClearTileWithChain(ref GameState state, Position pos)
{
    var tile = state.GetTile(pos);

    // 已经是空的，跳过（防止重复处理）
    if (tile.Type == TileType.None) return;

    if (tile.Bomb != BombType.None)
    {
        // 先清除炸弹本身（防止重复触发）
        state.SetTile(pos, None);
        // 触发炸弹效果
        effect.Apply(state, pos, chainAffected);
        // 递归处理连锁
        foreach (var chainPos in chainAffected)
            ClearTileWithChain(ref state, chainPos);
    }
    else
    {
        state.SetTile(pos, None);
    }
}
```

---

## API 参考

### IPowerUpHandler

```csharp
public interface IPowerUpHandler
{
    /// <summary>
    /// 处理特殊移动（两个位置的炸弹组合）
    /// </summary>
    void ProcessSpecialMove(ref GameState state, Position p1, Position p2, out int points);

    /// <summary>
    /// 激活单个炸弹
    /// </summary>
    void ActivateBomb(ref GameState state, Position p);
}
```

### IBombEffect

```csharp
public interface IBombEffect
{
    /// <summary>
    /// 炸弹类型
    /// </summary>
    BombType Type { get; }

    /// <summary>
    /// 应用炸弹效果
    /// </summary>
    /// <param name="state">游戏状态（只读）</param>
    /// <param name="origin">炸弹位置</param>
    /// <param name="affectedTiles">受影响的位置集合（输出）</param>
    void Apply(in GameState state, Position origin, HashSet<Position> affectedTiles);
}
```

### BombComboHandler

```csharp
public class BombComboHandler
{
    /// <summary>
    /// 尝试应用组合效果
    /// </summary>
    /// <returns>是否触发了组合</returns>
    public bool TryApplyCombo(ref GameState state, Position p1, Position p2, HashSet<Position> affected);

    /// <summary>
    /// 应用组合效果（假设已确认是有效组合）
    /// </summary>
    public void ApplyCombo(ref GameState state, Position p1, Position p2, HashSet<Position> affected);
}
```

### BombEffectRegistry

```csharp
public class BombEffectRegistry
{
    /// <summary>
    /// 创建包含所有默认效果的注册表
    /// </summary>
    public static BombEffectRegistry CreateDefault();

    /// <summary>
    /// 注册炸弹效果
    /// </summary>
    public void Register(IBombEffect effect);

    /// <summary>
    /// 获取指定类型的炸弹效果
    /// </summary>
    public bool TryGetEffect(BombType type, out IBombEffect? effect);
}
```

---

## 使用示例

### 创建 PowerUpHandler

```csharp
// 使用默认配置
var handler = new PowerUpHandler(scoreSystem);

// 自定义配置
var comboHandler = new BombComboHandler();
var registry = BombEffectRegistry.CreateDefault();
var handler = new PowerUpHandler(scoreSystem, comboHandler, registry);
```

### 处理炸弹组合

```csharp
// 玩家交换两个相邻的炸弹
handler.ProcessSpecialMove(ref state, pos1, pos2, out int points);
```

### 激活单个炸弹

```csharp
// 炸弹被消除时触发
handler.ActivateBomb(ref state, bombPosition);
```

### 自定义炸弹效果

```csharp
public class CustomBombEffect : IBombEffect
{
    public BombType Type => BombType.Custom; // 需要扩展 BombType 枚举

    public void Apply(in GameState state, Position origin, HashSet<Position> affectedTiles)
    {
        // 自定义消除逻辑
    }
}

// 注册自定义效果
registry.Register(new CustomBombEffect());
```

---

## 测试覆盖

### 单元测试统计

| 测试类 | 测试数量 | 描述 |
|--------|----------|------|
| BombEffectTests | 48 | 单个炸弹效果测试 |
| BombComboTests | 25 | 组合炸弹测试 |
| PowerUpHandlerTests | 18 | 集成测试（含连锁爆炸） |
| **总计** | **91** | |

### 测试场景覆盖

- ✅ 所有 5 种炸弹的基本效果
- ✅ 所有 10 种组合效果
- ✅ 边界裁剪（四个角落、四条边）
- ✅ 不同棋盘尺寸（3×3, 5×5, 8×8, 10×10, 12×12）
- ✅ 空棋盘处理
- ✅ 彩球颜色选择规则（指定颜色 vs 最多颜色）
- ✅ 效果确定性（多次调用相同结果）
- ✅ 注册表功能
- ✅ 递归连锁爆炸（二级、三级连锁）

---

## 性能考虑

### 对象池使用

系统使用 `Pools` 工具类来复用 `HashSet<Position>` 和 `List<Position>`，减少 GC 压力：

```csharp
var affected = Pools.ObtainHashSet<Position>();
try
{
    effect.Apply(in state, origin, affected);
    // 使用 affected...
}
finally
{
    Pools.Release(affected);
}
```

### 只读状态参数

`IBombEffect.Apply` 方法使用 `in GameState` 参数，确保效果计算不会修改游戏状态：

```csharp
void Apply(in GameState state, Position origin, HashSet<Position> affectedTiles);
```

实际的状态修改由 `PowerUpHandler` 统一处理。

---

## 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| 1.0 | 2024-01 | 初始实现：5 种单炸弹效果 |
| 1.1 | 2024-01 | 添加 BombComboHandler：10 种组合效果 |
| 1.2 | 2024-01 | 彩球规则修正：单独激活消除最多颜色，手动交换消除指定颜色 |
