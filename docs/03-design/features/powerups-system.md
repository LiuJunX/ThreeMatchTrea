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

## 炸弹类型 (ElementType bombs)

炸弹是 `ElementType` 的一部分（值 10-14），通过 `ElementType.IsBomb()` 扩展方法判断。

| ElementType 值 | 名称 | 描述 |
|----------------|------|------|
| `HorizontalRocket` (10) | 横向火箭 | 消除整行 |
| `VerticalRocket` (11) | 纵向火箭 | 消除整列 |
| `ColorBomb` (12) | 彩球 | 消除出现最多的颜色 |
| `Ufo` (13) | UFO | 小十字 + 随机消除 1 个方块 |
| `Square5x5` (14) | 方块炸弹 | 消除 5×5 区域 |

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

### 4. 彩球 (ColorBombEffect + ColorBombSession)

**触发条件**: 5 连消生成

**效果**: 消除棋盘上数量最多的颜色

**颜色选择规则**:
- 只统计普通颜色 (Item1-Item6)
- 排除已被其他 ColorBomb 预约的颜色
- 排除已锁定（Targeting）的格子中的 tile
- 数量最多的颜色优先，相同时选先遍历到的
- 如果所有颜色都被预约，彩球空转（不产生效果）

```
示例 (红色 10 个，蓝色 5 个):
红红红红红红红红  ← 全部红色被消除
红红□□□□□□
蓝蓝蓝蓝蓝□□□  ← 蓝色保留
□□□□□□□□
```

#### 多 Tick 会话机制 (ColorBombSessionManager)

单个彩球激活时路由到 `ColorBombSessionManager`，通过多 tick 会话实现延迟消除：

```
Match3.Core.Systems.PowerUps/ColorBomb/
├── IColorBombSessionManager.cs    # 接口
├── ColorBombSessionManager.cs     # 会话管理器
├── ColorBombSession.cs            # 会话状态 + BeamTarget + Phase enum
└── ColorBombConfig.cs             # 配置参数
```

**会话生命周期**:

```
Shooting → WaitingForBeams → BatchDestroy → Done
   ↑
   └── re-scan (最多 MaxReScans 次) ──┘
```

| 阶段 | 行为 |
|------|------|
| **Shooting** | 按固定间隔 (`BeamInterval`=0.12s) 随机顺序发射光束。每次发射锁定目标格子。发完后尝试 re-scan。 |
| **WaitingForBeams** | 等待所有飞行中的光束到达。每 tick 检查外部销毁。 |
| **BatchDestroy** | 彩球 + 所有存活目标同时销毁。释放所有锁、颜色预约。 |

**配置参数** (`ColorBombConfig`):

| 参数 | 默认值 | 含义 |
|------|--------|------|
| `BeamInterval` | 0.12s | 连续光束发射间隔 |
| `BeamSpeed` | 24 格/秒 | 光束飞行速度 |
| `MinFlightDuration` | 0.08s | 最短飞行时间 |
| `MaxReScans` | 3 | 最大重扫次数 |

**颜色预约机制**:
- 会话创建时预约目标颜色（`_reservedColors`）
- 其他彩球触发时跳过已预约颜色
- 重扫停止后立即释放颜色（不等到 BatchDestroy）

**格子锁定**:
- 光束发射时对目标格子施加 `Drop|Swap|Matching|Targeting` 锁
- 正在下落进入的 tile 不受影响（锁不含 Receive）
- 外部销毁时立即释放对应锁
- BatchDestroy 后释放所有剩余锁

**UFO 交互**:
- `UfoEffect.PickRemoteTarget()` 检查 `CellLockType.Targeting`，跳过被锁定格子
- `UfoProjectile.FindBestTarget()` 同样检查，重定向时排除锁定目标

**事件流**:

| 事件 | 时机 | 数据 |
|------|------|------|
| `ColorBombSessionStartEvent` | 会话创建 | TileId, Position, TargetColor |
| `ColorBombBeamLaunchedEvent` | 每条光束发射 | BombTileId, Origin, TargetPosition, FlightDuration, BeamIndex |
| `ColorBombBatchDestroyEvent` | 批量销毁（在 TileDestroyedEvent 之前） | BombTileId, BombPosition, DestroyedPositions, DestroyedTileIds |
| `TileDestroyedEvent` ×N | 每个目标 tile | Reason = BombEffect |

#### 彩球视觉编排 (Choreography)

**会话模式**（单独激活）— 三个事件驱动：

| 事件 | RenderCommand | 视觉表现 |
|------|---------------|----------|
| `ColorBombSessionStartEvent` | ScaleTile + MoveTile + RotateTile + ShowEffect(bomb_flash) | 彩球放大 1.2x、弹跳、加速旋转、持续发光 |
| `ColorBombBeamLaunchedEvent` | SpawnProjectile → MoveProjectile → ImpactProjectile → ShowEffect(color_bomb_hit) | 彩色光束飞向目标，到达后闪光 |
| `ColorBombBatchDestroyEvent` | ScaleTile(→0) + RemoveTile | 彩球缩小消失，目标 tile 同步销毁 |

Choreographer 用 `_activeColorBombSessions` 字典跨批次追踪会话状态。
`_beamHitTimes` 在 `BatchDestroyEvent` 中被统一为最晚光束到达时间，确保所有目标同步销毁。

**组合模式**（交换触发）— 仍使用旧的 `BombActivatedEvent` + `EmitColorBombPerformance` 路径。

### 5. UFO (UfoEffect)

**触发条件**: 2×2 正方形匹配生成

**效果**:
1. 原地产生小十字（中心 + 上下左右，共 5 格）— 由 `UfoEffect.Apply()` 计算
2. 发射 `UfoProjectile` 飞向随机目标，到达后消除 1 个方块

**影响范围**: 5 + 1 = 6 格（边界会裁剪小十字）

```
示例 (UFO 在 (4,4)):
□□□□█□□□
□□□███□□  ← 小十字（即时）
□□□□█□□□
□□□□□□□□
□□□□□□█□  ← 随机目标（投射物延迟到达）
□□□□□□□□
```

#### UFO 投射物飞行系统

UFO 的远程打击通过 `ProjectileSystem` 实现真正的延迟销毁：

| 组件 | 职责 |
|------|------|
| `UfoEffect` | 计算小十字（5 格），提供 `PickRemoteTarget()` 选择远程目标 |
| `PowerUpHandler` | 激活十字爆炸后，创建 `UfoProjectile` 并通过 `ProjectileSystem.Launch()` 发射 |
| `UfoProjectile` | 基于计时器的投射物：`Duration = Overhead + Distance / Speed` |
| `ProjectileSystem` | 每 tick 调用 `Update()`，到达时触发 `ProjectileImpactEvent` |

**共享常量** (`UfoConstants`):

| 常量 | 值 | 含义 |
|------|---|------|
| `LaunchOverhead` | 0.6s | 起飞+着陆固定开销 |
| `FlightSpeed` | 2 格/秒 | 巡航速度 |
| `LockInTime` | 0.3s | 锁定窗口（见下文） |

#### 动态重定向 (Dynamic Retargeting)

飞行中每 tick 检查目标格子：若目标已为空（被其他爆炸消除或掉落走），自动寻找新的有效目标。

**锁定窗口**: 当剩余飞行时间 < `LockInTime`(0.3s) 时，UFO 锁定当前目标不再重定向。
这避免了临近着陆时的突兀转向，并给视觉层足够帧数（~18帧@60fps）播放降落动画。

**重定向后位置连续性**: `_phaseStartTime` 字段在重定向时重置，确保进度从 0 重新计算，
避免从旧时间线计算出错误的 65%+ 进度导致位置跳变。

#### UFO 视觉编排 (Choreography)

| 事件 | RenderCommand | 说明 |
|------|---------------|------|
| `BombActivatedEvent` | 原点 CellLock | 锁定起飞格子 |
| `ProjectileLaunchedEvent` | `UfoLaunchCommand` | 起飞动画（StayFraction=0.35 蓄力阶段） |
| `ProjectileRetargetedEvent` | `UfoRetargetCommand` | Player 替换飞行段（StayFraction=0） |
| `ProjectileImpactEvent` | `RemoveTileCommand` + `ShowEffectCommand` | 移除 UFO tile + 撞击特效 |

Player 使用 smoothstep 插值飞行路径，重定向时从实际视觉位置重新计算飞行时长，
确保速度恒定（`UfoConstants.FlightSpeed`）。

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
    if (tile.Type == ElementType.None) return;  // ElementType

    if (tile.Type.IsBomb())  // ElementType.IsBomb() extension
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
    /// 炸弹的 ElementType 值
    /// </summary>
    ElementType Type { get; }

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
    public bool TryGetEffect(ElementType type, out IBombEffect? effect);
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
    public ElementType Type => ElementType.Custom; // 需要扩展 ElementType 枚举

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
| ColorBombSessionManagerTests | 23 | 会话系统测试（颜色预约、锁定、re-scan、外部销毁、批量销毁） |
| **总计** | **114** | |

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
- ✅ 彩球会话：颜色预约与释放
- ✅ 彩球会话：两个会话并发运行（不同颜色）
- ✅ 彩球会话：格子锁定（Drop/Swap/Matching/Targeting）
- ✅ 彩球会话：外部销毁（飞行中 + 已到达）
- ✅ 彩球会话：re-scan 发现新目标 + 上限限制
- ✅ 彩球会话：所有颜色被预约时空转
- ✅ 彩球会话：Cover 吸收命中
- ✅ 彩球会话：跳过下落中的 tile
- ✅ 彩球会话：BatchDestroy 事件顺序（在 TileDestroyed 之前）

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
| 2.0 | 2026-03 | UFO 投射物系统重构：计时器飞行、动态重定向、锁定窗口、UfoConstants 共享常量 |
| 3.0 | 2026-03 | 彩球多 tick 会话系统：颜色预约、随机顺序固定间隔光束、格子锁定、re-scan、批量销毁 |
