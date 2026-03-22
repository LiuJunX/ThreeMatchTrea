# Spawner 架构设计

| 文档状态 | 作者 | 日期 | 前置文档 |
| :--- | :--- | :--- | :--- |
| **设计定稿** | 讨论共识 | 2026-03-21 | `spawn-model.md`（Phase 1 实现）、`difficulty-director.md`（DryRun 部分仍有效） |

> **与旧文档的关系**：
> - `spawn-model.md` 记录 Phase 1 的 RuleBasedSpawnModel 实现细节，仍然有效。
> - `difficulty-director.md` 的 DryRun/Best-of-N 评分系统仍然有效，但以下部分被本文档**取代**：
>   - Challenge 策略（§3.2）→ 删除，不在运行时增加难度
>   - Near-miss 工程（§6 "让玩家差一点过"）→ 删除，不刻意阻止通关
>   - Rubber Banding 反向收紧 → 删除，只做正向帮助

---

## 1. 核心约束（优先级高→低）

| 优先级 | 约束 | 含义 |
| :--- | :--- | :--- |
| 1 | **AI 可制作** | 关卡配置必须是 JSON 可描述的，AI 能直接生成和调参 |
| 2 | **强控难度** | 引擎层面精确控制通关率，未来可接入 AI 模型实时决策 |
| 3 | **配置简洁** | 默认行为足够好，只有特殊关卡需要额外配置 |

---

## 2. 底层设计哲学

### 2.1 Spawner 的本质

Spawner 是整个游戏中唯一的熵源——棋盘未来所有可能性的入口。玩家体验（"有解"、"卡住了"、"终于掉了"）全部由 Spawner 注入的东西决定。

棋盘上所有东西只分两类：
- **手段**：玩家用来达成目标的工具（颜色方块、道具）
- **目的**：玩家需要完成的任务（收集物、要打碎的障碍物、分数）

难度的本质：`目标量 / (手段量 × 步数)`。Spawner 控制手段量，如果也负责掉落收集物则同时控制目标物供给。

### 2.2 只帮不害原则

**行业验证**：Royal Match（$4B+ 收入）的核心策略是"可感知的公平"——没有负面 Spawner，没有主动增难。Toon Blast 因被指控操控掉落导致差评暴增。

**设计规则**：
- SpawnModel **只做正向干预**：颜色保底、收集物保底、Mercy 帮助
- SpawnModel **不做负向干预**：不主动避开有用颜色、不拉平颜色分布增难
- 如果关卡太简单，调 seed 池或关卡参数（减步数、加障碍），不在运行时使绊子
- 帮助最好是可见的（Royal Match 策略：道具放最佳位置，玩家看得见系统在帮忙）

### 2.3 意图驱动配置

配置表达**意图**（"这关收集 8 个 Bird"），不表达**实现**（"第 3 列每 5 步以 0.12 概率掉 Bird"）。引擎负责从意图推导出运行时行为。

---

## 3. 三层架构

```
┌──────────────────────────────────────────────────┐
│  意图层（LevelConfig JSON）                        │
│  objectives + moveLimit + approvedSeeds           │
│  面向设计师/AI，极简                               │
├──────────────────────────────────────────────────┤
│  推导层（引擎启动时）                               │
│  从 objectives 自动生成 SpawnCondition 列表         │
│  面向引擎，一次性计算                               │
├──────────────────────────────────────────────────┤
│  执行层（每帧）                                    │
│  按条件优先级生成 + 安全网兜底                      │
│  RealtimeRefillSystem，纯机械执行                  │
└──────────────────────────────────────────────────┘
```

---

## 4. 难度控制：Seed 选择 + SpawnModel 安全网

### 4.1 双层分工（80/20）

| 层 | 控制什么 | 精度 | 时机 |
| :--- | :--- | :--- | :--- |
| **Seed 选择** | 这一局的"剧本骨架" | 粗调：整局胜率、失败差步数方差 | 关卡设计时（离线） |
| **SpawnModel 安全网** | 极端情况兜底 | 细调：~20% 的生成被覆盖 | 运行时（每帧） |

80%+ 的时间 SpawnModel 直接透传 seed 产生的颜色。只在边界条件下介入：

| 触发条件 | 动作 | 预估频率 |
| :--- | :--- | :--- |
| 某颜色连续 4+ 次未出现 | PRD 保底补充 | ~5% |
| 与列顶同色 | 换一种 | ~12% |
| 失败 3 次 + 剩余步数少 | Mercy：偏向有用色 | ~1% |
| 收集物掉落保底触发 | 强制掉落 | 取决于配置 |

### 4.2 Seed 选择工作流

```
AI 生成关卡配置
    ↓
分析服务: for seed in 0..N:
    模拟 M 次（纯随机 SpawnModel）
    记录: 胜率、失败差步数均值/方差、目标完成分布
    ↓
筛选: 满足所有约束的 seed 集合
    - 胜率 ∈ [目标区间]
    - 失败差步数方差 < 阈值
    - 失败差步数均值 ∈ [甜蜜点]（如 2-4 步）
    ↓
输出: approvedSeeds 写入 LevelConfig
```

运行时：从 `approvedSeeds` 中随机选一个 seed 初始化 RNG。

### 4.3 approvedSeeds 不分难度档

一个 seed 池足够。理由：

- SpawnModel Mercy 的 20% 影响力足以把 35% 胜率的 seed 推到 55%+
- 分档会导致模拟成本翻倍、配置复杂度上升
- 如果需要更强 Mercy（新手/付费挽回），临时放大 SpawnModel 介入权限即可

### 4.4 Seed 与道具的关系

Seed 选择只针对"无道具基准"。玩家使用关前道具后，棋盘初始状态改变，seed 的编排效果偏移——这没问题，道具本身就是"付费 easy mode"。SpawnModel 安全网负责应对道具带来的偏离。

### 4.5 与分析服务的关系

| 层 | 分析服务用什么 |
| :--- | :--- |
| Seed 选择 | 分析服务**就是**产出 approvedSeeds 的系统 |
| SpawnModel | 分析用纯随机 SpawnModel（不用安全网），保持指标稳定 |

分析结果与运行时行为高度一致（80%+ 相同），因为 SpawnModel 只覆盖 ~20% 的生成。

---

## 5. 条件优先级体系

借鉴 Matchington Mansion 的设计，生成条件按优先级排序，高优先级条件先检查，Default 永远兜底。

### 5.1 条件类型

| 优先级 | 条件 | 触发方式 | 来源 |
| :--- | :--- | :--- | :--- |
| 500 | **PresetQueue** | 按固定序列循环掉落 | 手动配置（极少用） |
| 400 | **ObjectiveDrop** | PRD 概率 + 保底 | 从 objectives 自动推导 |
| 300 | **EventDrop** | 消除触发掉落 | 手动配置 |
| 100 | **Default** | seed 驱动的颜色生成 | 始终存在 |

### 5.2 自动推导示例

```json
{
  "objectives": [{ "type": "CollectBird", "count": 8 }],
  "moveLimit": 25
}
```

引擎启动时自动生成：

```
ObjectiveDropCondition(priority=400):
  elementType: Bird
  baseProbability: 推算自 count/moveLimit
  prd: true（递增概率保底）
  maxOnBoard: 3（防堆积）
  relyOn: Sink 可达（场上没有可达 Sink 时暂停掉落）
  reductionAfterGoal: 0.1（目标完成后大幅降权）

DefaultCondition(priority=100):
  seed 驱动颜色生成
  连续同色限制 ≤ 4
```

### 5.3 执行流程

```
对每个空位:
  遍历条件（按优先级降序）:
    if condition.IsConditionMet():
      element = condition.Generate()
      更新计数器
      break
  // Default 的 IsConditionMet() 始终返回 true，保证兜底
```

---

## 6. 保底机制

### 6.1 颜色保底：PRD（伪随机分布）

取代简单均匀随机。核心公式：`P(N) = C × N`，N 为连续未出现次数。

效果：标准差显著低于真随机，消除"某种颜色消失 10+ 步"的极端情况。玩家感知仍然是随机的。

参考：Dota 2 暴击/闪避系统、原神 Gacha 软保底。

### 6.2 收集物保底：三维计数器

借鉴 MM 的多维度限制：

| 维度 | 控制什么 | 例子 |
| :--- | :--- | :--- |
| maxPerRound | 每步最多生成几个 | 每步最多掉 1 个 Bird |
| maxTotal | 整局最多生成几个 | 全局最多掉 10 个 Bird |
| maxOnBoard | 场上最多同时存在几个 | 场上最多 3 个 Bird |

### 6.3 依赖关系（RelyOn）

自动推断的依赖约束：

- Bird 依赖 Sink：场上没有可达 Sink 时暂停 Bird 掉落
- Key 依赖 Door：场上没有门时暂停 Key 掉落

防止无意义的掉落浪费生成位。

### 6.4 目标满足后衰减

目标完成后，对应元素掉落权重按 `reductionRate` 大幅衰减（不完全停止，保留少量）。

### 6.5 通关后简化（JammingTime）

关卡目标全部完成后，切换到只掉不重复色方块——纯粹为了让剩余步数的消除动画好看。

---

## 7. 关卡配置 Schema

### 7.1 最小配置（90% 的关卡）

```json
{
  "width": 8,
  "height": 8,
  "moveLimit": 25,
  "targetDifficulty": 0.5,
  "objectives": [
    { "type": "Score", "count": 5000 },
    { "type": "CollectBird", "count": 8 }
  ],
  "cells": ["..."],
  "grid": ["..."],
  "approvedSeeds": [42, 891, 2033, 4517]
}
```

引擎从 objectives 自动推导所有掉落行为。`approvedSeeds` 由分析服务离线生成。

### 7.2 可选覆盖（特殊关卡）

```json
{
  "spawnConfig": {
    "columnConstraints": {
      "0-3": { "colors": ["Red", "Blue", "Green"] },
      "4-7": { "colors": ["Yellow", "Purple", "Orange"] }
    },
    "overrides": {
      "bird": {
        "baseProbability": 0.15,
        "startAfterMove": 5,
        "maxOnBoard": 2
      }
    }
  }
}
```

大多数关卡不需要 `spawnConfig`。

---

## 8. RNG 流隔离

参考 Slay the Spire 的多流 Seed 系统（以及其相关性 bug 教训），不同决策使用独立 RNG 流：

| Domain | 用途 |
| :--- | :--- |
| ColorRng | 颜色方块生成 |
| DropRng | 收集物掉落决策 |
| EffectRng | 特效/道具结果 |

各 domain 的 seed 通过 hash 派生（`seed ^ domainId`），避免初始状态相同导致的幽灵关联。

项目已有 `RandomDomain` 概念，方向正确。

---

## 9. 演进路径

| Phase | 内容 | 前置 |
| :--- | :--- | :--- |
| **当前** | RuleBasedSpawnModel（Help/Balance），纯颜色生成 | 已实现 |
| **Phase 2** | SpawnContext 补齐 GoalProgress/FailedAttempts | ObjectiveSystem 集成 |
| **Phase 3** | 条件优先级体系 + 收集物掉落（objectives 驱动） | Phase 2 |
| **Phase 4** | Seed 选择：分析服务产出 approvedSeeds | 分析服务扩展 |
| **Phase 5** | SpawnModel 退化为安全网（80/20），删除 Challenge 策略 | Phase 4 |
| **Phase 6** | 可选 PredictBatch 接口 + AI 模型替换 RuleBasedSpawnModel | ML 基础设施 |

Phase 3-5 是本文档的核心交付。Phase 6 是远期方向。

---

## 10. 行业参考

| 来源 | 关键洞察 | 对我们的影响 |
| :--- | :--- | :--- |
| King GDC 2024 | 同一关不同 seed 胜率差 15%-75% | Seed 选择是最强难度旋钮 |
| Gamigion 行业报告 | 颜色概率偏移 20-30% 玩家无感知 | SpawnModel 安全网的干预力度上限 |
| Royal Match ($4B+) | 无负面 Spawner，显性帮助 | "只帮不害"原则的商业验证 |
| Tetris 7-Bag | 有界随机消除极端值 | PRD/N-Bag 替代简单均匀随机 |
| Slay the Spire | 多 RNG 流的相关性 bug | RNG 流隔离 + hash 派生 seed |
| Dota 2 PRD | P(N)=C×N 消除连续不触发 | 颜色保底和收集物保底算法 |
| MM（Matchington Mansion） | 条件优先级体系 + 三维计数器 | 借鉴其条件架构，避免其三套难度系统叠加 |
| EA DDA 专利 | Seed 值是 "Knob" 之一 | Seed 选择方向的专利验证 |

详细研究报告：`docs/research/spawner-systems-research.md`

---

## 11. 实施进度 TODO

### 已完成（临时方案）
- [x] `RealtimeRefillSystem.FailedAttempts` 属性注入，SpawnContext 传递链路打通
- [x] `LevelConfig.ApprovedSeeds` 字段 + DeepCopy + JSON 自动映射
- [x] `GameServiceFactory.CreateGameSession` 中 ApprovedSeeds → SeedManager.SetOverride(Refill/Drop)
- [x] Mercy 集成测试（3 个场景：触发/FailedAttempts=0/GoalProgress≥0.9）

### 待实现
- [ ] **ISessionContext 接口**：替换 `RealtimeRefillSystem.FailedAttempts` 属性。管理跨局 FailedAttempts（通关重置、失败+1）、InFlowState 检测。生命周期：关卡选择 → 通关/退出。
- [ ] **离线分析 Pipeline**：批量种子模拟（for seed in 0..N → 模拟 M 次，纯随机 SpawnModel）→ 胜率/失败差步数方差筛选 → 输出 approvedSeeds 到 LevelConfig JSON。见 §4。
- [ ] **SpawnConditionFactory.CreatePureRandom**：分析服务专用便捷方法（无安全网、无 Mercy）
- [ ] **InFlowState 检测**：连续成功时降低安全网干预力度（SpawnContext.InFlowState）
- [ ] **Level JSON 工具链**：分析完成后自动回写 approvedSeeds 到关卡 JSON

---

## 12. 关键决策记录

| # | 决策 | 理由 | 替代方案及否决原因 |
| :--- | :--- | :--- | :--- |
| D1 | 只帮不害 | Royal Match 商业验证；Toon Blast 反面教训 | Challenge 策略（运行时增难）→ 玩家感知"被操控" |
| D2 | Seed 粗调 + SpawnModel 细调 | 两者互补，分别处理宏观和微观 | 纯 seed 控制 → 无法应对道具偏离；纯 SpawnModel → 分析结果不稳定 |
| D3 | 80/20 分权 | SpawnModel 干预太多会稀释 seed 筛选效果 | 50/50 → 分析服务预测不准 |
| D4 | objectives 驱动掉落 | 配置简洁，AI 可制作 | 手工配权重/间隔 → 配置爆炸（MM 的教训） |
| D5 | 条件优先级体系 | 可扩展，新元素 = 新 Condition | 硬编码特殊元素 → 每加一种改主流程 |
| D6 | approvedSeeds 单池不分档 | SpawnModel Mercy 足够弥补；分档增加配置复杂度 | 多档 seed 池 → 模拟成本翻倍 |
| D7 | PRD 替代均匀随机 | 消除极端值，保持随机感 | 7-Bag → 周期性可预测；均匀随机 → 极端旱灾 |
