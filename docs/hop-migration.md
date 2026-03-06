# HOP 迁移计划 (Super Refactory)

## 目标

将 HOP 线上三消游戏的 77 种元素和全部玩法规则，迁移到 Match3Explore 的纯 C# 架构中。

## 源项目

- **HOP 旧代码（只读）**: `C:\GitWorkSpace\hop-repositories-c\hop.client.main\HOPUnity`
- **分支**: `feature/super-refactory`

### HOP 关键路径

| 内容 | 路径 |
|------|------|
| 元素类型枚举 | `Assets/Scripts/Level/LevelEnums.cs` (ElementType, BombType, SwapType) |
| 元素 Model | `Assets/Scripts/Level/MVC/Models/Element*.cs` (82个) |
| 元素 Component | `Assets/Scripts/Level/MVC/Components/Element*.cs` |
| 元素 View | `Assets/Scripts/Level/MVC/Views/Element*.cs` |
| 元素 Config | `Assets/Scripts/Level/MVC/Configs/Element*.cs` |
| 匹配检测 | `Assets/Scripts/Level/Core/Engine/Components/MatchComponent.cs` |
| 消除组件 | `Assets/Scripts/Level/Core/Engine/Components/EliminateComponent.cs` |
| 移动组件 | `Assets/Scripts/Level/Core/Engine/Components/MoveComponent.cs` |
| 交换组件 | `Assets/Scripts/Level/Core/Engine/Components/SwapComponent.cs` |
| 核心引擎 | `Assets/Scripts/Level/Core/Engine/CoreEngine.cs` |
| 消除命令 | `Assets/Scripts/Level/Commands/Elim*.cs` |
| 炸弹命令 | `Assets/Scripts/Level/Commands/Eliminate*.cs` |
| 关卡模型 | `Assets/Scripts/Level/Core/Model/LevelModel.cs` |
| 网格模型 | `Assets/Scripts/Level/Core/Model/GridModel.cs` |
| 格子模型 | `Assets/Scripts/Level/Core/Model/CellModel.cs` |

## 工作流

每个元素/规则的迁移循环：

```
① 读 HOP 旧代码 → 提取规则（用 Glob/Grep/Read，不需要 Unity）
② 写测试用例 → 确定性棋盘 + 指定操作 + 预期结果
③ 写实现代码 → 在 Match3Explore Core 中实现
④ dotnet test → 通过则提交，失败则修复重跑
```

## 测试规范

### 三层测试体系

**第一层：手写核心规则测试（~50个）**
- 每个子系统的基本正确性
- 确定性棋盘：完全手工指定，无随机生成
- 每步只触发一个无歧义的匹配链
- 验证最终稳定状态，不验证中间过程

**第二层：Property-Based Testing（~1000+自动生成）**
- 程序自动生成随机棋盘和操作
- 验证不变量而非具体结果：
  - 稳定后无悬空元素
  - 稳定后无未处理的3连
  - 引擎最终一定达到稳定状态
  - 炸弹类型与匹配形状一致
  - 消除后元素数量减少

**第三层：AI 自动玩全量关卡**
- 加载所有线上关卡配置
- AI 自动玩，验证不崩溃、不死循环
- 统计通关率、步数分布

### 测试用例设计原则

```
每个测试 = 手工棋盘 + 指定操作 + 确定的预期结果

确定性保证：
  ✅ 棋盘完全手工指定，无随机生成
  ✅ 操作明确指定（交换哪两个格子）
  ✅ 预期结果由规则唯一确定
  ✅ 不依赖帧率、执行顺序、Coroutine

设计约束（避免歧义）：
  ✅ 每步只触发一个匹配组（或明确的级联链）
  ✅ 下落路径无分支（不出现两个元素竞争同一个空格）
  ✅ 验证最终稳定状态，不验证中间过程动画
```

## HOP 核心规则清单

### 匹配生成规则

| 匹配模式 | 生成炸弹 |
|----------|----------|
| 横向3消 | 无 |
| 纵向3消 | 无 |
| 横向4消 | Vertical 条纹炸弹 |
| 纵向4消 | Horizontal 条纹炸弹 |
| 2x2方块 | Square 方块炸弹 |
| 横向5消 | Color 彩色炸弹 |
| 纵向5消 | Color 彩色炸弹 |
| T型/L型 (横≥3且纵≥3) | Cross 十字炸弹 |

### 2x2 方块优先级规则

2x2 方块匹配有**前置条件**（CheckFourSquareCondition）：
- 横纵同时 ≥3 → 拒绝（优先生成 Cross）
- 横或纵 ≥4 → 拒绝（优先生成 Line 炸弹）
- 即：2x2 只在横/纵都不构成更高级匹配时才成立

完整判定（IsFoursquareSelected）：
```
fMatchCount > 0 AND hMatchCount > 1 AND vMatchCount > 1 AND CheckFourSquareCondition(h, v)
```

### 匹配检测算法

**线性匹配** (`GetMatchCellsWithMatchType`):
- 从中心格向两侧扩展，遇到不同色或边界停止
- 最多扩展到 5 格（COLOR_MATCH_COUNT）
- 横向和纵向分别独立检测

**2x2 匹配** (`MatchType.Foursquare`):
- 检查中心格的4个对角方向
- 形成的 2x2 区域内4格全部同色 → 匹配成功

**复合匹配** (T/L形):
- 横纵同时 ≥3 时自动形成
- 不需要显式检测形状，由 GetComplexType 处理

### 炸弹效果

| 炸弹类型 | 效果 |
|----------|------|
| Horizontal | 消除整行 |
| Vertical | 消除整列 |
| Square | 消除 3x3 范围（power=1），可扩展到 5x5/9x9 |
| Cross | 消除整行 + 整列 |
| Color + 普通 | 消除全场同色 |

### 炸弹 Power 等级

| Power | 范围 | 触发条件 |
|-------|------|----------|
| 1 | 3x3 | 普通方块炸弹 |
| 2 | 5x5 | 方块+方块组合 |
| 3 | 9x9 (带角落裁剪) | 特殊情况 |

### 角落裁剪 (cornerSize)

GetAdjacentCells 支持角落裁剪，形成菱形/十字效果：
```
includeCorner=true:    includeCorner=false, cs=1:
  X X X                    . X .
  X X X                    X X X
  X X X                    . X .
```

### 炸弹组合

| 组合 | 效果 | 消除格子数(9x9) |
|------|------|-----------------|
| 条纹 + 条纹 | 十字（整行+整列） | 17 (9+9-1) |
| 方块 + 方块 | 两个 3x3 爆炸 | 18 (2×9) |
| 方块 + 条纹 | 3行×3列条纹效果 | 45 (3×9+3×9-3×3) |
| 十字 + 方块 | 扩大十字 + 3x3 | ~扩展十字 |
| 十字 + 条纹 | 扩展十字 | ~十字扩展 |
| 十字 + 十字 | 双十字（2行+2列） | 45 (同方块+条纹) |
| 彩色 + 方块 | 全场同色变方块炸弹 | 全场 |
| 彩色 + 条纹 | 全场同色变条纹炸弹 | 全场 |
| 彩色 + 十字 | 全场同色变十字炸弹 | 全场 |
| 彩色 + 彩色 | 全场清除 | 全场 |

## HOP 引擎架构详解

### 游戏主循环 (CoreEngine.Update)

```
1. RunUpdateTasks()           — 帧更新
2. Commander.Update(dt)       — 执行排队移动命令
3. RunBeforeMoveElementsTasks()
4. MoveComponent.Update(dt)   — 下落/移动
5. RunBeforeCollapseTask()
6. EliminateComponent.CheckCollapse() — 匹配检测+消除
7. RunPostCollapseTask()
8. 检查命令队列 / 移动完成
9. RunBeforeStableTask()
10. IsStable = true → 可接受输入
```

### 交换流程

```
用户交换 A,B
  → SwapComponent.TrySwapping(A, B)
    → 临时交换位置
    → MatchComponent.SwapMatch(B, A) → 检测匹配
    → 恢复位置
    → 返回 SwapablePair
  → DoSwapping(pair)
    → 永久交换
    → EliminateComponent.EliminateSwap(pair)
    → 触发消除级联
```

### 重力/下落系统

**链式架构** (Chain-based):
- 每个 CellModel 有 `prev`（上游）和 `nextCell`（下游）
- `direction` 向量定义元素流向（支持任意方向）
- `tails`：链尾列表（下落检测起点）
- `heads`：链头列表（新元素生成点）

**Cell 接收状态机**:
| 状态 | 含义 |
|------|------|
| AllowToReceive | 可接收元素 |
| WaitToReceive | 元素正在进入 |
| ForbidToReceive | 不可接收（已占用或阻挡） |

**下落算法** (MoveComponent.Update):
```
FOR each tail:
  FOR each cell in chain (tail → head):
    IF cell.IsAllowReceive:
      element = GetPrevMovableElement(cell)
      IF found: cell.WaitToReceive(element), StartMove()
      ELSE: try side-slide (左右滑入)
      ELSE IF at head: generate new element
```

**侧滑 (Side-Slide)**:
- 直接上游无可用元素时，尝试从垂直方向的相邻格获取
- 方向：链方向的垂直方向（纵向链 → 检查左右）
- 权重限制：`posWeight` 防止逆向移动

### 消除流程

```
CheckCollapse()
  → MatchComponent.Collapse(range)
    → 返回 List<MatchInfo>
  → EliminateMatches(matchInfos)
    → 遍历每个匹配组
    → 创建 MatchPooledCmd
    → EliminateCell() 对每个匹配格子
      → GemWall → WindowBlind → FoldedPaper → BrickWall
      → Obstacle → Backpack → Element → Addition
```

### 炸弹消除命令类层次

| 命令类 | 作用 |
|--------|------|
| BombBaseCmd | 方块类炸弹基类（管理 OneAffectedCellOrBorder） |
| EliminateLineBasedCmd | 条纹类炸弹基类（行/列展开） |
| ElimSquareNoColorCmd | 3x3/5x5/9x9 方块炸弹 |
| EliminateStripedCmd | 水平/垂直/十字条纹 |
| EliminateSquareStripedCmd | 方块+条纹组合 |
| EliminateColorCmd | 彩色炸弹（全场同色消除） |
| ElimPlaneCmd | 飞机炸弹（3x3移动消除） |

## HOP vs Match3Explore 架构对比

| 特性 | HOP | Match3Explore |
|------|-----|---------------|
| 网格结构 | 链式链接 (prev/next) | 1D 数组 (y*W+x) |
| 重力方向 | 每格独立方向向量 | 物理引擎+速度加速度 |
| 下落机制 | 链遍历+侧滑 | 物理模拟+对角滑动 |
| 游戏循环 | deltaTime + Commander | Tick-based simulation |
| 匹配检测 | MatchComponent 线性扫描 | ClassicMatchFinder BFS |
| 炸弹生成 | GetComplexType 规则表 | BombGenerator + ShapeDetector |
| 状态管理 | MVC (Model/Component/View) | ECS-like (GameState + Systems) |
| 消除 | Command 模式 (BombBaseCmd) | ExplosionSystem |
| 事件系统 | 回调+Coroutine | EventCollector + IEventVisitor |

## 元素类型（77种）

按优先级分批迁移：

**P0 - 基础元素（先做）**
- Jelly (1) — 基本可消除元素
- Bomb (2) — 炸弹
- CupCake (3), Waffle (4), Button (11), SquareButton (12), CottonCandy (10)
- Candy (97), Nut (94), Cheese (86), Coin (95)

**P1 - 容器/障碍物**
- Stone (21), Lock (14), Key (34), Door (33)
- IceBox (56), AntiqueBox (18), MiceBox (27)
- RussianDoll (62), Pinata (80), Jar (96)

**P2 - 工具/特殊元素**
- Lawnmower (37), Piano (45), Rabbit (58)
- FireWork (99), DuckUzi (85)
- ColorCopier (22), EndlessWell (24), Alternater (25)
- BombBox (70), StripBombBlocker (113)

**P3 - 家具/主题元素**
- Table (30), Lamp (31), CuckooClock (32), Statue (35), Telephone (36)
- 以及其余元素...

**最新元素**
- Battery (107) — 链式消除（HP系统，连接检测，连锁爆炸）

## HOP 验证工具 (RuleVerifier)

位于 `Assets/Scripts/Level/Editor/RuleVerifier/`，需在 HOP Unity 中运行：

| 验证器 | 菜单 | 测试数 | 验证内容 |
|--------|------|--------|----------|
| MatchRuleVerifier | HOP Test → 1 | 40+ | GetComplexType 匹配→炸弹映射 |
| SwapRuleVerifier | HOP Test → 2 | 26 | SwapType 炸弹组合判定 |
| BombEffectVerifier | HOP Test → 3 | 15 | 炸弹爆炸影响范围 |
| FoursquareConditionVerifier | HOP Test → 4 | 25+ | 2x2方块匹配条件+范围计算 |
| EliminationRangeVerifier | HOP Test → 5 | 30+ | 消除范围（power等级+组合模式） |
| AllRulesVerifier | HOP Test → 0 | 全部 | 一键运行所有验证 |

## 迁移进度

> 在此记录每个元素/规则的迁移状态

| 元素/规则 | HOP验证 | Match3实现 | 测试数 | 备注 |
|-----------|---------|------------|--------|------|
| 基础匹配规则 | ✅ 验证器已写 | 待开始 | 40+ | MatchRuleVerifier |
| 2x2方块条件 | ✅ 验证器已写 | 待开始 | 25+ | FoursquareConditionVerifier |
| 炸弹生成 | ✅ 验证器已写 | 待开始 | 含在匹配规则中 | |
| 炸弹效果 | ✅ 验证器已写 | 待开始 | 15 | BombEffectVerifier |
| 炸弹组合 | ✅ 验证器已写 | 待开始 | 26 | SwapRuleVerifier |
| 消除范围 | ✅ 验证器已写 | 待开始 | 30+ | EliminationRangeVerifier |
| 重力/下落 | 📖 规则已提取 | 待开始 | 0 | 链式架构，需适配 |
| 匹配检测算法 | 📖 规则已提取 | 待开始 | 0 | 线性扫描+2x2+Suck合并 |
| 交换流程 | 📖 规则已提取 | 待开始 | 0 | 临时交换→检测→恢复→执行 |
| Jelly | 待开始 | 待开始 | 0 | |
| ... | ... | ... | ... | |
